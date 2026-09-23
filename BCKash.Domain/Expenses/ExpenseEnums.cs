namespace BCKash.Domain.Expenses;

/// <summary>Legacy values of `expenses.recur_type` (singular forms, unlike `payroll.recur_type`'s plural forms).</summary>
public enum ExpenseRecurType
{
    Day,
    Week,
    Month,
    Year,
}

/// <summary>Shared legacy approval-status values of `expenses.status`, `expense_budgets.status` and `other_income.status`.</summary>
public enum ApprovalStatus
{
    Pending,
    Approved,
    Declined,
}
