using BCKash.Application.Expenses;
using BCKash.Domain.Expenses;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Expenses;

public class OtherIncomeService : IOtherIncomeService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOtherIncomeGlPostingService _glPostingService;

    public OtherIncomeService(BCKashDbContext db, ICurrentUserContext currentUser, IOtherIncomeGlPostingService glPostingService)
    {
        _db = db;
        _currentUser = currentUser;
        _glPostingService = glPostingService;
    }

    public async Task<OtherIncomeWriteResult> CreateAsync(OtherIncome income, CancellationToken cancellationToken = default)
    {
        if (income.OtherIncomeTypeId is int typeId && !await _db.OtherIncomeTypes.AnyAsync(t => t.Id == typeId, cancellationToken))
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.TypeNotFound);
        }

        income.CreatedById = _currentUser.UserId;
        income.Status = ApprovalStatus.Pending;

        _db.OtherIncomes.Add(income);
        await _db.SaveChangesAsync(cancellationToken);
        return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.Success, income);
    }

    public async Task<OtherIncomeWriteResult> UpdateAsync(int id, OtherIncome updated, CancellationToken cancellationToken = default)
    {
        var income = await _db.OtherIncomes.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (income is null)
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.NotFound);
        }

        if (income.Status != ApprovalStatus.Pending)
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.InvalidTransition);
        }

        if (updated.OtherIncomeTypeId is int typeId && !await _db.OtherIncomeTypes.AnyAsync(t => t.Id == typeId, cancellationToken))
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.TypeNotFound);
        }

        income.OfficeId = updated.OfficeId;
        income.OtherIncomeTypeId = updated.OtherIncomeTypeId;
        income.Name = updated.Name;
        income.Amount = updated.Amount;
        income.Date = updated.Date;
        income.Year = updated.Year;
        income.Month = updated.Month;
        income.Notes = updated.Notes;
        income.Files = updated.Files;

        await _db.SaveChangesAsync(cancellationToken);
        return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.Success, income);
    }

    public async Task<OtherIncomeWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var income = await _db.OtherIncomes.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (income is null)
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.NotFound);
        }

        if (income.Status != ApprovalStatus.Pending)
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.InvalidTransition);
        }

        _db.OtherIncomes.Remove(income);
        await _db.SaveChangesAsync(cancellationToken);
        return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.Success);
    }

    public async Task<OtherIncomeWriteResult> ApproveAsync(int id, string? notes, CancellationToken cancellationToken = default)
    {
        var income = await _db.OtherIncomes.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (income is null)
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.NotFound);
        }

        if (income.Status != ApprovalStatus.Pending)
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.InvalidTransition);
        }

        income.Status = ApprovalStatus.Approved;
        income.ApprovedById = _currentUser.UserId;
        income.ApprovedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (notes is not null)
        {
            income.Notes = notes;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _glPostingService.PostApprovalAsync(income, cancellationToken);

        return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.Success, income);
    }

    public async Task<OtherIncomeWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.ReasonRequired);
        }

        var income = await _db.OtherIncomes.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (income is null)
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.NotFound);
        }

        if (income.Status != ApprovalStatus.Pending)
        {
            return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.InvalidTransition);
        }

        income.Status = ApprovalStatus.Declined;
        income.DeclinedById = _currentUser.UserId;
        income.DeclinedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        income.Notes = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new OtherIncomeWriteResult(OtherIncomeWriteOutcome.Success, income);
    }
}
