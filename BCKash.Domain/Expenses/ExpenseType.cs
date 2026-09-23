using BCKash.SharedKernel;

namespace BCKash.Domain.Expenses;

/// <summary>Maps the legacy `expense_types` table (BRD §6.10).</summary>
public class ExpenseType : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? GlAccountAssetId { get; set; }
    public int? GlAccountExpenseId { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<ExpenseBudget> ExpenseBudgets { get; set; } = new List<ExpenseBudget>();
}
