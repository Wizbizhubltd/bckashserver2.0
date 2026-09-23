using BCKash.Domain.Payroll;
using Xunit;

namespace BCKash.Domain.Tests.Payroll;

/// <summary>
/// Net-pay computation from a template with mixed fixed/percentage/tax line items — Phase 8's
/// first acceptance criterion. See PayrollComputationRules' doc comment for the computation
/// order this locks in.
/// </summary>
public class PayrollComputationRulesTests
{
    [Fact]
    public void No_line_items_returns_gross_as_net()
    {
        var result = PayrollComputationRules.Compute(100_000m, []);
        Assert.Equal(100_000m, result.NetPay);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Fixed_addition_and_fixed_deduction_apply_directly()
    {
        var lines = new List<PayrollLineInput>
        {
            new(1, PayrollTemplateMetaType.Addition, IsTax: false, IsPercentage: false, TaxOn: null, Value: 10_000m), // transport allowance
            new(2, PayrollTemplateMetaType.Deduction, IsTax: false, IsPercentage: false, TaxOn: null, Value: 5_000m), // loan deduction
        };

        var result = PayrollComputationRules.Compute(100_000m, lines);

        Assert.Equal(105_000m, result.NetPay); // 100,000 + 10,000 - 5,000
        Assert.Equal(10_000m, result.Lines.Single(l => l.PayrollTemplateMetaId == 1).Amount);
        Assert.Equal(-5_000m, result.Lines.Single(l => l.PayrollTemplateMetaId == 2).Amount);
    }

    [Fact]
    public void Percentage_addition_is_computed_against_gross()
    {
        var lines = new List<PayrollLineInput>
        {
            new(1, PayrollTemplateMetaType.Addition, IsTax: false, IsPercentage: true, TaxOn: null, Value: 10m), // 10% housing allowance
        };

        var result = PayrollComputationRules.Compute(200_000m, lines);

        Assert.Equal(220_000m, result.NetPay); // 200,000 + 10% of 200,000
        Assert.Equal(20_000m, result.Lines.Single().Amount);
    }

    [Fact]
    public void Percentage_tax_on_gross_is_computed_against_the_original_gross()
    {
        var lines = new List<PayrollLineInput>
        {
            new(1, PayrollTemplateMetaType.Addition, IsTax: false, IsPercentage: false, TaxOn: null, Value: 20_000m), // allowance first
            new(2, PayrollTemplateMetaType.Deduction, IsTax: true, IsPercentage: true, TaxOn: PayrollTemplateMetaTaxOn.Gross, Value: 5m), // 5% tax on gross
        };

        var result = PayrollComputationRules.Compute(100_000m, lines);

        // Intermediate net after the addition: 120,000. Tax is 5% of the *original* gross (100,000) = 5,000.
        Assert.Equal(115_000m, result.NetPay);
        Assert.Equal(-5_000m, result.Lines.Single(l => l.PayrollTemplateMetaId == 2).Amount);
    }

    [Fact]
    public void Percentage_tax_on_net_is_computed_against_the_intermediate_net_after_non_tax_lines()
    {
        var lines = new List<PayrollLineInput>
        {
            new(1, PayrollTemplateMetaType.Addition, IsTax: false, IsPercentage: false, TaxOn: null, Value: 20_000m), // allowance first -> intermediate net 120,000
            new(2, PayrollTemplateMetaType.Deduction, IsTax: true, IsPercentage: true, TaxOn: PayrollTemplateMetaTaxOn.Net, Value: 10m), // 10% tax on net
        };

        var result = PayrollComputationRules.Compute(100_000m, lines);

        // Tax = 10% of 120,000 = 12,000. Net = 120,000 - 12,000 = 108,000.
        Assert.Equal(108_000m, result.NetPay);
        Assert.Equal(-12_000m, result.Lines.Single(l => l.PayrollTemplateMetaId == 2).Amount);
    }

    [Fact]
    public void Mixed_fixed_percentage_and_tax_lines_compute_correct_net_pay()
    {
        var lines = new List<PayrollLineInput>
        {
            new(1, PayrollTemplateMetaType.Addition, IsTax: false, IsPercentage: false, TaxOn: null, Value: 15_000m), // fixed housing allowance
            new(2, PayrollTemplateMetaType.Addition, IsTax: false, IsPercentage: true, TaxOn: null, Value: 5m), // 5% transport allowance of gross
            new(3, PayrollTemplateMetaType.Deduction, IsTax: false, IsPercentage: false, TaxOn: null, Value: 2_000m), // fixed pension contribution
            new(4, PayrollTemplateMetaType.Deduction, IsTax: true, IsPercentage: true, TaxOn: PayrollTemplateMetaTaxOn.Net, Value: 7.5m), // 7.5% tax on net
        };

        var result = PayrollComputationRules.Compute(100_000m, lines);

        // Non-tax: 100,000 + 15,000 + 5,000 (5% of 100,000) - 2,000 = 118,000 (intermediate net)
        // Tax: 7.5% of 118,000 = 8,850 -> final net = 109,150
        Assert.Equal(109_150m, result.NetPay);
        Assert.Equal(4, result.Lines.Count);
    }
}
