namespace BCKash.Domain.Loans;

/// <summary>
/// Min ≤ default ≤ max validation (FR-LN-2, for a product's own principal/term/interest-rate
/// triads) and value-within-range validation (FR-LN-4, for a loan application's amount/term
/// against its product's configured range). A null bound is treated as unconstrained on that
/// side — matching how every other range field in this schema is optional.
/// </summary>
public static class LoanProductValidationRules
{
    public static bool IsValidMinDefaultMax(decimal? min, decimal? @default, decimal? max)
    {
        if (min.HasValue && @default.HasValue && min > @default)
        {
            return false;
        }

        if (@default.HasValue && max.HasValue && @default > max)
        {
            return false;
        }

        if (min.HasValue && max.HasValue && min > max)
        {
            return false;
        }

        return true;
    }

    public static bool IsValidMinDefaultMax(int? min, int? @default, int? max)
    {
        if (min.HasValue && @default.HasValue && min > @default)
        {
            return false;
        }

        if (@default.HasValue && max.HasValue && @default > max)
        {
            return false;
        }

        if (min.HasValue && max.HasValue && min > max)
        {
            return false;
        }

        return true;
    }

    public static bool IsWithinRange(decimal value, decimal? min, decimal? max) =>
        (!min.HasValue || value >= min) && (!max.HasValue || value <= max);

    public static bool IsWithinRange(int value, int? min, int? max) =>
        (!min.HasValue || value >= min) && (!max.HasValue || value <= max);
}
