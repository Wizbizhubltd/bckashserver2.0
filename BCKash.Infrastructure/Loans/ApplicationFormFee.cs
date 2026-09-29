using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// The non-refundable application form fee every loan applicant pays — the active
/// <see cref="ChargeType.ApplicationFormFee"/> charge, edited in Settings → Fees &amp; Payments.
/// Null when it has been switched off (or deleted).
/// </summary>
public static class ApplicationFormFee
{
    public static Task<Charge?> CurrentAsync(BCKashDbContext db, CancellationToken cancellationToken = default) =>
        db.Charges
            .Where(c => c.ChargeType == ChargeType.ApplicationFormFee && c.Product == ChargeProduct.Loan && c.Active && c.Amount > 0)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
