using BCKash.Application.Communications;
using BCKash.Domain.Clients;
using BCKash.Domain.Communications;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Communications;

/// <summary>
/// Recipient targeting (BR-COM-1, FR-COM-1). FR-COM-1 names the categories and filters but
/// doesn't define their predicates — the exact meaning locked in here (documented in
/// docs/phase9-communications-reporting-spec.md):
/// - AllClients/ActiveClients/ProspectiveClients: Client.Status Pending/Active/(any), respectively.
/// - ActiveLoans: clients with at least one Disbursed loan.
/// - LoansInArrears: clients with at least one loan carrying an unpaid, past-due repayment
///   schedule row (any lateness) — the broader, "any days late" set.
/// - OverdueLoans: clients with at least one loan already flagged NPA (Loan.IsNpa) — the
///   stricter subset past the product's configured NpaDays threshold. This is what
///   distinguishes "arrears" from "overdue" in this codebase's terms (see LoanNpaService).
/// - HappyBirthday: Client.Dob's day-of-year falls within [FromDay, ToDay] (both parsed as
///   day-of-year integers, 1-366); if either is unset, defaults to today's day-of-year.
/// The "date range" filter FR-COM-1 mentions has no backing column on CommunicationCampaign
/// (FromDay/ToDay are specifically the birthday window) — not implemented, since inventing a
/// new column wasn't in scope.
/// </summary>
public class CampaignRecipientService : ICampaignRecipientService
{
    private readonly BCKashDbContext _db;

    public CampaignRecipientService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CampaignRecipient>> ResolveAsync(CommunicationCampaign campaign, CancellationToken cancellationToken = default)
    {
        var officeId = ParseInt(campaign.OfficeId);
        var loanOfficerId = ParseInt(campaign.LoanOfficerId);
        var loanProductId = ParseInt(campaign.LoanProductId);
        var loanStatus = Enum.TryParse<LoanStatus>(campaign.LoanStatus, out var status) ? status : (LoanStatus?)null;

        IQueryable<Client> clients = _db.Clients.AsQueryable();
        if (officeId.HasValue)
        {
            clients = clients.Where(c => c.OfficeId == officeId);
        }

        switch (campaign.RecipientsCategory)
        {
            case CampaignRecipientsCategory.ActiveClients:
                clients = clients.Where(c => c.Status == ClientStatus.Active);
                break;

            case CampaignRecipientsCategory.ProspectiveClients:
                clients = clients.Where(c => c.Status == ClientStatus.Pending);
                break;

            case CampaignRecipientsCategory.HappyBirthday:
                // DateOnly.DayOfYear isn't reliably translatable by every EF Core provider, so
                // the birthday-window comparison happens in memory after materializing.
                var candidates = await clients
                    .Where(c => c.Dob != null)
                    .Select(c => new BirthdayCandidate(c.Id, c.FullName ?? c.DisplayName, c.Mobile, c.Email, c.Dob!.Value))
                    .ToListAsync(cancellationToken);
                return FilterByBirthdayWindow(candidates, campaign.FromDay, campaign.ToDay);

            case CampaignRecipientsCategory.ActiveLoans:
            case CampaignRecipientsCategory.LoansInArrears:
            case CampaignRecipientsCategory.OverdueLoans:
                var loanClientIds = await MatchingLoanClientIdsAsync(campaign.RecipientsCategory.Value, loanOfficerId, loanProductId, loanStatus, cancellationToken);
                clients = clients.Where(c => loanClientIds.Contains(c.Id));
                break;

            case CampaignRecipientsCategory.AllClients:
            case null:
            default:
                break;
        }

        var results = await clients
            .Select(c => new CampaignRecipient(c.Id, c.FullName ?? c.DisplayName, c.Mobile, c.Email))
            .ToListAsync(cancellationToken);

        return results;
    }

    private async Task<HashSet<int>> MatchingLoanClientIdsAsync(
        CampaignRecipientsCategory category, int? loanOfficerId, int? loanProductId, LoanStatus? loanStatus, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        IQueryable<Loan> loans = _db.Loans.AsQueryable();

        if (loanOfficerId.HasValue)
        {
            loans = loans.Where(l => l.LoanOfficerId == loanOfficerId);
        }

        if (loanProductId.HasValue)
        {
            loans = loans.Where(l => l.LoanProductId == loanProductId);
        }

        if (loanStatus.HasValue)
        {
            loans = loans.Where(l => l.Status == loanStatus);
        }

        switch (category)
        {
            case CampaignRecipientsCategory.ActiveLoans:
                if (!loanStatus.HasValue)
                {
                    loans = loans.Where(l => l.Status == LoanStatus.Disbursed);
                }

                break;

            case CampaignRecipientsCategory.OverdueLoans:
                loans = loans.Where(l => l.IsNpa);
                break;

            case CampaignRecipientsCategory.LoansInArrears:
                var arrearsLoanIds = await _db.LoanRepaymentSchedules
                    .Where(s => !s.Paid && s.DueDate < today)
                    .Select(s => s.LoanId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                loans = loans.Where(l => arrearsLoanIds.Contains(l.Id));
                break;
        }

        return (await loans.Select(l => l.ClientId).Distinct().ToListAsync(cancellationToken))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
    }

    private static IReadOnlyList<CampaignRecipient> FilterByBirthdayWindow(IReadOnlyList<BirthdayCandidate> candidates, string? fromDay, string? toDay)
    {
        var today = DateTime.UtcNow.DayOfYear;
        var from = int.TryParse(fromDay, out var f) ? f : today;
        var to = int.TryParse(toDay, out var t) ? t : today;

        bool InWindow(int dayOfYear) => from <= to
            ? dayOfYear >= from && dayOfYear <= to
            : dayOfYear >= from || dayOfYear <= to; // window wraps the new year (e.g. Dec 20 -> Jan 10)

        return candidates
            .Where(c => InWindow(c.Dob.DayOfYear))
            .Select(c => new CampaignRecipient(c.ClientId, c.Name, c.Mobile, c.Email))
            .ToList();
    }

    private static int? ParseInt(string? value) => int.TryParse(value, out var parsed) ? parsed : null;

    private record BirthdayCandidate(int ClientId, string? Name, string? Mobile, string? Email, DateOnly Dob);
}
