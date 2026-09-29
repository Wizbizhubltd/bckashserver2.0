using BCKash.Application.Clients;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Clients;

/// <summary>See <see cref="IClientSavingsService"/>.</summary>
public class ClientSavingsService : IClientSavingsService
{
    // Loans still being repaid. A List so EF can translate Contains.
    private static readonly List<LoanStatus> RunningStatuses = [LoanStatus.Disbursed, LoanStatus.PendingReschedule, LoanStatus.Rescheduled];

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClientSavingsSettingsProvider _settings;

    public ClientSavingsService(BCKashDbContext db, ICurrentUserContext currentUser, IClientSavingsSettingsProvider settings)
    {
        _db = db;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<ClientSavingsSummary> GetAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var entries = await _db.ClientSavingsEntries
            .Where(e => e.ClientId == clientId)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken);
        var balance = Math.Max(0, entries.Sum(e => e.Amount));
        var running = await _db.Loans.AnyAsync(l => l.ClientId == clientId && l.DeletedAt == null && RunningStatuses.Contains(l.Status), cancellationToken);
        var feeRate = (await _settings.GetAsync(cancellationToken)).EarlyWithdrawalFeeRate;
        var fee = running ? ClientSavingsRules.EarlyWithdrawalFee(balance, feeRate) : 0m;
        return new ClientSavingsSummary(balance, running, feeRate, fee, balance - fee, entries);
    }

    public async Task<ClientSavingsWithdrawalResult> WithdrawAsync(int clientId, string? notes, CancellationToken cancellationToken = default)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId && c.DeletedAt == null, cancellationToken))
        {
            return new ClientSavingsWithdrawalResult(ClientSavingsWithdrawalOutcome.NotFound);
        }

        var summary = await GetAsync(clientId, cancellationToken);
        if (summary.Balance <= 0)
        {
            return new ClientSavingsWithdrawalResult(ClientSavingsWithdrawalOutcome.NothingToWithdraw, Summary: summary);
        }

        var note = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Add(clientId, ClientSavingsEntryType.Withdrawal, -summary.WithdrawalPayout, note ?? (summary.HasRunningLoan ? "Early cash-out" : "Withdrawn in full"));
        if (summary.WithdrawalFee > 0)
        {
            Add(clientId, ClientSavingsEntryType.EarlyWithdrawalFee, -summary.WithdrawalFee, $"{summary.EarlyWithdrawalFeeRate * 100:0.##}% kept for cashing out while a loan is running");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientSavingsWithdrawalResult(ClientSavingsWithdrawalOutcome.Success, summary.WithdrawalPayout, summary.WithdrawalFee, await GetAsync(clientId, cancellationToken));
    }

    private void Add(int clientId, ClientSavingsEntryType type, decimal amount, string notes) =>
        _db.ClientSavingsEntries.Add(new ClientSavingsEntry
        {
            ClientId = clientId,
            Type = type,
            Amount = amount,
            Notes = notes,
            CreatedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
}
