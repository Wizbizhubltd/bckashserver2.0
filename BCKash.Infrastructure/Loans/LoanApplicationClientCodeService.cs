using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BCKash.Application.Auth;
using BCKash.Application.Loans;
using BCKash.Application.Organization;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

public class LoanApplicationClientCodeService : ILoanApplicationClientCodeService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOtpDispatcher _otpDispatcher;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly ICurrencyDisplayProvider _currency;

    public LoanApplicationClientCodeService(BCKashDbContext db, ICurrentUserContext currentUser, IOtpDispatcher otpDispatcher, ICompanyProfileProvider companyProfile, ICurrencyDisplayProvider currency)
    {
        _currency = currency;
        _db = db;
        _currentUser = currentUser;
        _otpDispatcher = otpDispatcher;
        _companyProfile = companyProfile;
    }

    public async Task<bool> IsRequiredAsync(CancellationToken cancellationToken = default)
    {
        var value = await _db.Settings
            .Where(s => s.SettingKey == ILoanApplicationClientCodeService.SettingKey)
            .OrderByDescending(s => s.Id)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
        return value?.Trim() is "1" || string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ClientCodeRequestResult> RequestAsync(int clientId, int loanProductId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (!await IsRequiredAsync(cancellationToken))
        {
            return new ClientCodeRequestResult(ClientCodeRequestOutcome.NotRequired);
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client is null)
        {
            return new ClientCodeRequestResult(ClientCodeRequestOutcome.ClientNotFound);
        }

        var product = await _db.LoanProducts.Where(p => p.Id == loanProductId).Select(p => new { p.Name }).FirstOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return new ClientCodeRequestResult(ClientCodeRequestOutcome.ProductNotFound);
        }

        var phone = FirstNonBlank(client.Mobile, client.Phone);
        var email = string.IsNullOrWhiteSpace(client.Email) ? null : client.Email.Trim();
        if (phone is null && email is null)
        {
            return new ClientCodeRequestResult(ClientCodeRequestOutcome.NoContact);
        }

        // Throttle per client: one code a minute, a handful an hour — enough for a genuine retry,
        // not enough to spam a client's phone.
        var now = DateTime.UtcNow;
        var recent = await _db.LoanApplicationClientCodes
            .Where(c => c.ClientId == clientId && c.CreatedAtUtc > now.AddHours(-1))
            .Select(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var latest = recent.Count > 0 ? recent.Max() : (DateTime?)null;
        if (latest.HasValue && latest.Value > now.AddSeconds(-ILoanApplicationClientCodeService.ResendAfterSeconds))
        {
            var wait = (int)Math.Ceiling((latest.Value.AddSeconds(ILoanApplicationClientCodeService.ResendAfterSeconds) - now).TotalSeconds);
            return new ClientCodeRequestResult(ClientCodeRequestOutcome.TooSoon, RetryAfterSeconds: Math.Max(wait, 1));
        }

        if (recent.Count >= ILoanApplicationClientCodeService.MaxCodesPerHour)
        {
            return new ClientCodeRequestResult(ClientCodeRequestOutcome.TooMany);
        }

        // A new code replaces any this staff member still has open for the client.
        var open = await _db.LoanApplicationClientCodes
            .Where(c => c.ClientId == clientId && c.RequestedById == _currentUser.UserId && c.ConsumedAtUtc == null && c.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        foreach (var previous in open)
        {
            previous.ExpiresAtUtc = now;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var sentTo = string.Join(", ", new[] { MaskPhone(phone), MaskEmail(email) }.Where(v => v is not null));
        var record = new LoanApplicationClientCode
        {
            ClientId = clientId,
            LoanProductId = loanProductId,
            Amount = amount,
            RequestedById = _currentUser.UserId,
            CodeHash = Hash(code),
            SentTo = sentTo,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(ILoanApplicationClientCodeService.CodeLifetimeMinutes),
        };
        _db.LoanApplicationClientCodes.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        var company = (await _companyProfile.GetAsync(cancellationToken)).Name;
        var staffName = await StaffNameAsync(cancellationToken);
        var amountText = (await _currency.GetAsync(cancellationToken)).Format(amount);
        var body =
            $"{staffName} at {company} is applying for a {amountText} {(string.IsNullOrWhiteSpace(product.Name) ? "" : product.Name + " ")}loan on your behalf. " +
            $"Your confirmation code is {code}. Only share it with them if you agreed to this loan. " +
            $"It expires in {ILoanApplicationClientCodeService.CodeLifetimeMinutes} minutes.";
        await _otpDispatcher.DispatchAsync(new OtpMessage(email, phone, $"Confirm your {company} loan application", body), cancellationToken);

        return new ClientCodeRequestResult(ClientCodeRequestOutcome.Sent, record.Id, sentTo, record.ExpiresAtUtc);
    }

    public async Task<ClientCodeCheckResult> CheckAsync(LoanApplication application, ClientCodeSubmission? submission, CancellationToken cancellationToken = default)
    {
        // Group applications aren't gated: there's no single client to confirm with.
        if (application.ClientType != LoanClientType.Client || !await IsRequiredAsync(cancellationToken))
        {
            return new ClientCodeCheckResult(ClientCodeCheckOutcome.NotRequired);
        }

        if (submission?.CodeId is not { } codeId || string.IsNullOrWhiteSpace(submission.Code))
        {
            return new ClientCodeCheckResult(ClientCodeCheckOutcome.Missing);
        }

        var record = await _db.LoanApplicationClientCodes.FirstOrDefaultAsync(c => c.Id == codeId, cancellationToken);
        if (record is null
            || record.ClientId != application.ClientId
            || record.LoanProductId != application.LoanProductId
            || record.Amount != application.Amount
            || record.RequestedById != _currentUser.UserId)
        {
            return new ClientCodeCheckResult(ClientCodeCheckOutcome.Mismatch);
        }

        if (record.ConsumedAtUtc is not null || record.ExpiresAtUtc <= DateTime.UtcNow || record.Attempts >= ILoanApplicationClientCodeService.MaxAttempts)
        {
            return new ClientCodeCheckResult(ClientCodeCheckOutcome.Expired);
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(record.CodeHash), Encoding.UTF8.GetBytes(Hash(submission.Code.Trim()))))
        {
            record.Attempts++;
            await _db.SaveChangesAsync(cancellationToken);
            return new ClientCodeCheckResult(ClientCodeCheckOutcome.Incorrect, record, ILoanApplicationClientCodeService.MaxAttempts - record.Attempts);
        }

        return new ClientCodeCheckResult(ClientCodeCheckOutcome.Valid, record);
    }

    private async Task<string> StaffNameAsync(CancellationToken cancellationToken)
    {
        var staff = await _db.Users
            .Where(u => u.Id == _currentUser.UserId)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstOrDefaultAsync(cancellationToken);
        var name = $"{staff?.FirstName} {staff?.LastName}".Trim();
        return name.Length > 0 ? name : "A staff member";
    }

    private static string? FirstNonBlank(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? MaskPhone(string? phone) =>
        phone is null ? null : phone.Length <= 6 ? new string('•', phone.Length) : $"{phone[..4]}{new string('•', phone.Length - 8 > 0 ? phone.Length - 8 : 2)}{phone[^4..]}";

    private static string? MaskEmail(string? email)
    {
        if (email is null) return null;
        var at = email.IndexOf('@');
        return at <= 1 ? $"•••{email[Math.Max(at, 0)..]}" : $"{email[0]}•••{email[at..]}";
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
