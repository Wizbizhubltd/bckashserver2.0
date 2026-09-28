using BCKash.Domain.Organization;
using Xunit;

namespace BCKash.Domain.Tests.Organization;

public class ChargeRulesTests
{
    private static ChargeRules.Definition Late(
        ChargeOption option = ChargeOption.InstallmentTotalDue, decimal amount = 5,
        int? grace = 3, int? repeat = null, decimal? cap = null) =>
        new("Late repayment fee", ChargeProduct.Loan, ChargeType.OverdueInstallmentFee, option, amount, null, null, grace, repeat, cap, null);

    [Fact]
    public void A_well_formed_late_repayment_fee_is_valid() =>
        Assert.Null(ChargeRules.Validate(Late(repeat: 30, cap: 10)));

    [Fact]
    public void A_late_fee_cannot_be_a_percentage_of_the_whole_loan() =>
        Assert.NotNull(ChargeRules.Validate(Late(option: ChargeOption.OriginalPrincipal)));

    [Fact]
    public void A_repeating_penalty_needs_a_total_cap() =>
        Assert.NotNull(ChargeRules.Validate(Late(repeat: 7, cap: null)));

    [Fact]
    public void A_percentage_cannot_exceed_100() =>
        Assert.NotNull(ChargeRules.Validate(Late(amount: 150)));

    [Fact]
    public void Penalty_controls_are_rejected_on_an_ordinary_fee() =>
        Assert.NotNull(ChargeRules.Validate(new("Processing fee", ChargeProduct.Loan, ChargeType.Disbursement, ChargeOption.OriginalPrincipal, 2, null, null, 3, null, null, null)));

    [Fact]
    public void Min_and_max_only_apply_to_percentages() =>
        Assert.NotNull(ChargeRules.Validate(new("Flat fee", ChargeProduct.Loan, ChargeType.Disbursement, ChargeOption.Flat, 500, 100, 1000, null, null, null, null)));

    [Fact]
    public void An_early_closure_fee_can_be_waived_after_some_instalments() =>
        Assert.Null(ChargeRules.Validate(new("Early closure", ChargeProduct.Loan, ChargeType.EarlyRepayment, ChargeOption.PrincipalDue, 2, 1000, 50000, null, null, null, 6)));

    [Theory]
    [InlineData(ChargeProduct.Group, ChargeType.Activation, true)]
    [InlineData(ChargeProduct.Group, ChargeType.MonthlyFee, true)]
    [InlineData(ChargeProduct.Client, ChargeType.AnnualFee, true)]
    [InlineData(ChargeProduct.Group, ChargeType.Disbursement, false)]
    public void Client_and_group_fees_are_flat_dues_and_registration(ChargeProduct product, ChargeType type, bool valid) =>
        Assert.Equal(valid, ChargeRules.Validate(new("Dues", product, type, ChargeOption.Flat, 1000, null, null, null, null, null, null)) is null);

    [Theory]
    [InlineData(ChargeType.OverdueInstallmentFee, true)]
    [InlineData(ChargeType.OverdueMaturity, true)]
    [InlineData(ChargeType.EarlyRepayment, true)]
    [InlineData(ChargeType.Disbursement, false)]
    public void Penalty_status_follows_from_the_type(ChargeType type, bool penalty) =>
        Assert.Equal(penalty, ChargeRules.IsPenalty(type));
}
