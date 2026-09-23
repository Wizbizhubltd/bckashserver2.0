namespace BCKash.Domain.Loans;

/// <summary>FR-LN-25's NPA classification. Pure predicates — the caller (LoanNpaService) computes days-in-arrears from the schedule and persists the result.</summary>
public static class LoanNpaRules
{
    public static bool IsNpa(int daysInArrears, int? npaDays) => npaDays.HasValue && daysInArrears > npaDays.Value;

    public static bool ShouldSuspendIncome(bool isNpa, bool npaSuspendIncome) => isNpa && npaSuspendIncome;
}
