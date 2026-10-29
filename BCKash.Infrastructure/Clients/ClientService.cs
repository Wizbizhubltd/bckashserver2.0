using BCKash.Application.Clients;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Clients;

public class ClientService : IClientService
{
    // Read-generate-insert isn't transactionally safe against a concurrent create picking
    // the same sequence number — the unique index on AccountNo (ClientConfiguration) is the
    // real safety net; this just bounds how many times we retry the whole cycle before giving up.
    private const int MaxAccountNumberRetries = 5;

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClientAccess _access;

    public ClientService(BCKashDbContext db, ICurrentUserContext currentUser, IClientAccess access)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
    }

    public async Task<ClientWriteResult> CreateAsync(Client client, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxAccountNumberRetries; attempt++)
        {
            client.AccountNo = await NextAccountNumberAsync(cancellationToken);

            try
            {
                _db.Clients.Add(client);
                await _db.SaveChangesAsync(cancellationToken);
                return new ClientWriteResult(ClientWriteOutcome.Success, client);
            }
            catch (DbUpdateException)
            {
                _db.Entry(client).State = EntityState.Detached;
            }
        }

        return new ClientWriteResult(ClientWriteOutcome.AccountNumberGenerationFailed);
    }

    private async Task<string> NextAccountNumberAsync(CancellationToken cancellationToken)
    {
        var accountNumbers = await _db.Clients
            .Select(c => c.AccountNo)
            .ToListAsync(cancellationToken);

        long maxSequence = 0;
        foreach (var accountNo in accountNumbers)
        {
            if (ClientAccountNumberFormat.TryParseSequence(accountNo, out var sequence) && sequence > maxSequence)
            {
                maxSequence = sequence;
            }
        }

        return ClientAccountNumberFormat.Format(maxSequence + 1);
    }

    public async Task<ClientWriteResult> UpdateAsync(int id, Client updated, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        client.Bvn = updated.Bvn;
        client.CountryId = updated.CountryId;
        client.OfficeId = updated.OfficeId;
        client.StaffId = updated.StaffId;
        client.ReferredById = updated.ReferredById;
        client.ExternalId = updated.ExternalId;
        client.Title = updated.Title;
        client.FirstName = updated.FirstName;
        client.MiddleName = updated.MiddleName;
        client.LastName = updated.LastName;
        client.FullName = updated.FullName;
        client.IncorporationNumber = updated.IncorporationNumber;
        client.DisplayName = updated.DisplayName;

        // Picture is deliberately not copied: the profile picture is the enrolled face capture
        // (ClientBiometricsService) and only a new enrollment changes it — an edit of the client's
        // details, which never carries it, must not wipe it.
        client.Mobile = updated.Mobile;
        client.Phone = updated.Phone;
        client.Email = updated.Email;
        client.Gender = updated.Gender;
        client.ClientType = updated.ClientType;
        client.MaritalStatus = updated.MaritalStatus;
        client.Dob = updated.Dob;
        client.Street = updated.Street;
        client.Ward = updated.Ward;
        client.District = updated.District;
        client.Region = updated.Region;
        client.Address = updated.Address;
        client.JoinedDate = updated.JoinedDate;
        client.Occupation = updated.Occupation;
        client.PostalCode = updated.PostalCode;
        client.Country = updated.Country;
        client.State = updated.State;
        client.City = updated.City;
        client.BusinessAddress = updated.BusinessAddress;
        client.Nationality = updated.Nationality;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> ActivateAsync(int id, DateOnly? activatedDate, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!ClientStatusTransitionRules.CanActivate(client.Status))
        {
            return new ClientWriteResult(ClientWriteOutcome.InvalidTransition);
        }

        client.Status = ClientStatus.Active;
        client.ActivatedDate = activatedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        client.ActivatedById = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> DeactivateAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ClientWriteResult(ClientWriteOutcome.ReasonRequired);
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!ClientStatusTransitionRules.CanDeactivate(client.Status))
        {
            return new ClientWriteResult(ClientWriteOutcome.InvalidTransition);
        }

        client.Status = ClientStatus.Inactive;
        client.InactiveDate = DateOnly.FromDateTime(DateTime.UtcNow);
        client.InactiveById = _currentUser.UserId;
        client.InactiveReason = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> ReactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!ClientStatusTransitionRules.CanReactivate(client.Status))
        {
            return new ClientWriteResult(ClientWriteOutcome.InvalidTransition);
        }

        client.Status = ClientStatus.Active;
        client.ReactivatedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        client.ReactivatedById = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ClientWriteResult(ClientWriteOutcome.ReasonRequired);
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!ClientStatusTransitionRules.CanDecline(client.Status))
        {
            return new ClientWriteResult(ClientWriteOutcome.InvalidTransition);
        }

        client.Status = ClientStatus.Declined;
        client.DeclinedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        client.DeclinedById = _currentUser.UserId;
        client.DeclinedReason = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> CloseAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ClientWriteResult(ClientWriteOutcome.ReasonRequired);
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!ClientStatusTransitionRules.CanClose(client.Status))
        {
            return new ClientWriteResult(ClientWriteOutcome.InvalidTransition);
        }

        client.Status = ClientStatus.Closed;
        client.ClosedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        client.ClosedById = _currentUser.UserId;
        client.ClosedReason = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> TransferOfficeAsync(int id, int officeId, int marketerId, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!await _db.Offices.AnyAsync(o => o.Id == officeId && o.Active && o.DeletedAt == null, cancellationToken))
        {
            return new ClientWriteResult(ClientWriteOutcome.OfficeNotFound);
        }

        var marketer = await _db.Users
            .Where(u => u.Id == marketerId && !u.Blocked && _db.RoleUsers.Any(ru => ru.UserId == u.Id && ru.Role.Slug == UserTypeSlugs.Marketer))
            .Select(u => new { u.Id, u.OfficeId })
            .FirstOrDefaultAsync(cancellationToken);
        if (marketer is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.MarketerNotFound);
        }

        if (marketer.OfficeId != officeId)
        {
            return new ClientWriteResult(ClientWriteOutcome.MarketerInDifferentOffice);
        }

        if (client.OfficeId == officeId)
        {
            return new ClientWriteResult(ClientWriteOutcome.AlreadyInOffice);
        }

        if (await IsDefaulterAsync(id, cancellationToken))
        {
            return new ClientWriteResult(ClientWriteOutcome.Defaulter);
        }

        if (await _access.ActiveLoanAsync(id, cancellationToken) is not null)
        {
            return new ClientWriteResult(ClientWriteOutcome.HasActiveLoan);
        }

        client.OfficeId = officeId;
        client.StaffId = marketerId;

        // Groups belong to one office, so the client leaves theirs; the new office can put them in one of its own.
        var now = DateTime.UtcNow;
        foreach (var membership in await _db.GroupClients.Where(gc => gc.ClientId == id && gc.RemovedAt == null).ToListAsync(cancellationToken))
        {
            membership.RemovedAt = now;
            membership.RemovedById = _currentUser.UserId;
        }

        // Open savings accounts move with the client; closed, declined and withdrawn ones stay as history.
        var openSavings = await _db.Savings
            .Where(s => s.ClientId == id && (s.Status == SavingsAccountStatus.Pending || s.Status == SavingsAccountStatus.Approved))
            .ToListAsync(cancellationToken);
        foreach (var account in openSavings)
        {
            account.OfficeId = officeId;
            account.FieldOfficerId = marketerId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    /// <summary>A loan of theirs, or their share of a group loan, is non-performing or written off.</summary>
    private Task<bool> IsDefaulterAsync(int clientId, CancellationToken cancellationToken)
    {
        var allocatedLoanIds = _db.GroupLoanAllocations.Where(a => a.ClientId == clientId && a.LoanId != null).Select(a => a.LoanId!.Value);
        return _db.Loans.AnyAsync(
            l => l.DeletedAt == null
                && (l.ClientId == clientId || allocatedLoanIds.Contains(l.Id))
                && (l.IsNpa || l.Status == LoanStatus.WrittenOff),
            cancellationToken);
    }
}
