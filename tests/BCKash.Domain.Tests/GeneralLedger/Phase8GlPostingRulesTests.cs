using BCKash.Domain.Assets;
using BCKash.Domain.Expenses;
using BCKash.Domain.GeneralLedger;
using Xunit;

namespace BCKash.Domain.Tests.GeneralLedger;

/// <summary>
/// Phase 8's GL posting rules for depreciation/expenses/other income/payroll — every method
/// must return balanced lines (sum debit == sum credit) or an empty list when the type/run
/// isn't fully configured for GL, mirroring LoanGlPostingRulesTests' shape.
/// </summary>
public class Phase8GlPostingRulesTests
{
    private static void AssertBalanced(IReadOnlyList<GlPostingLine> lines)
    {
        Assert.NotEmpty(lines);
        Assert.Equal(lines.Sum(l => l.Debit ?? 0m), lines.Sum(l => l.Credit ?? 0m));
    }

    [Fact]
    public void Asset_depreciation_debits_expense_credits_contra_asset()
    {
        var type = new AssetType { GlAccountExpenseId = 10, GlAccountContraAssetId = 20 };
        var lines = AssetGlPostingRules.ForDepreciation(type, 3_000m);

        AssertBalanced(lines);
        Assert.Equal(3_000m, lines.Single(l => l.GlAccountId == 10).Debit);
        Assert.Equal(3_000m, lines.Single(l => l.GlAccountId == 20).Credit);
    }

    [Fact]
    public void Asset_depreciation_with_unmapped_account_produces_no_lines()
    {
        var type = new AssetType { GlAccountExpenseId = 10 }; // contra-asset missing
        Assert.Empty(AssetGlPostingRules.ForDepreciation(type, 3_000m));
    }

    [Fact]
    public void Expense_approval_debits_expense_credits_asset()
    {
        var type = new ExpenseType { GlAccountExpenseId = 10, GlAccountAssetId = 20 };
        var lines = ExpenseGlPostingRules.ForApproval(type, 500m);

        AssertBalanced(lines);
        Assert.Equal(500m, lines.Single(l => l.GlAccountId == 10).Debit);
        Assert.Equal(500m, lines.Single(l => l.GlAccountId == 20).Credit);
    }

    [Fact]
    public void OtherIncome_approval_debits_asset_credits_income()
    {
        var type = new OtherIncomeType { GlAccountAssetId = 20, GlAccountIncomeId = 30 };
        var lines = OtherIncomeGlPostingRules.ForApproval(type, 750m);

        AssertBalanced(lines);
        Assert.Equal(750m, lines.Single(l => l.GlAccountId == 20).Debit);
        Assert.Equal(750m, lines.Single(l => l.GlAccountId == 30).Credit);
    }

    [Fact]
    public void Payroll_run_debits_expense_credits_asset()
    {
        var payroll = new BCKash.Domain.Payroll.Payroll { GlAccountExpenseId = 10, GlAccountAssetId = 20 };
        var lines = PayrollGlPostingRules.ForPayrollRun(payroll, 108_150m);

        AssertBalanced(lines);
        Assert.Equal(108_150m, lines.Single(l => l.GlAccountId == 10).Debit);
        Assert.Equal(108_150m, lines.Single(l => l.GlAccountId == 20).Credit);
    }

    [Fact]
    public void Zero_or_negative_amounts_produce_no_lines()
    {
        var assetType = new AssetType { GlAccountExpenseId = 1, GlAccountContraAssetId = 2 };
        var expenseType = new ExpenseType { GlAccountExpenseId = 1, GlAccountAssetId = 2 };
        var incomeType = new OtherIncomeType { GlAccountAssetId = 1, GlAccountIncomeId = 2 };
        var payroll = new BCKash.Domain.Payroll.Payroll { GlAccountExpenseId = 1, GlAccountAssetId = 2 };

        Assert.Empty(AssetGlPostingRules.ForDepreciation(assetType, 0m));
        Assert.Empty(ExpenseGlPostingRules.ForApproval(expenseType, -10m));
        Assert.Empty(OtherIncomeGlPostingRules.ForApproval(incomeType, 0m));
        Assert.Empty(PayrollGlPostingRules.ForPayrollRun(payroll, 0m));
    }
}
