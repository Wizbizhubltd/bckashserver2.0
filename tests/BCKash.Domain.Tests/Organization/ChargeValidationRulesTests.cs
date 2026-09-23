using BCKash.Domain.Organization;
using Xunit;

namespace BCKash.Domain.Tests.Organization;

public class ChargeValidationRulesTests
{
    [Theory]
    [InlineData(ChargeType.Disbursement, ChargeProduct.Loan, true)]
    [InlineData(ChargeType.Disbursement, ChargeProduct.Savings, false)]
    [InlineData(ChargeType.SavingsActivation, ChargeProduct.Savings, true)]
    [InlineData(ChargeType.SavingsActivation, ChargeProduct.Loan, false)]
    [InlineData(ChargeType.SharesPurchase, ChargeProduct.Shares, true)]
    [InlineData(ChargeType.Activation, ChargeProduct.Client, true)]
    [InlineData(ChargeType.Activation, ChargeProduct.Savings, false)]
    public void ChargeType_is_scoped_to_its_legal_product(ChargeType chargeType, ChargeProduct product, bool expected)
    {
        Assert.Equal(expected, ChargeValidationRules.IsValidChargeTypeForProduct(chargeType, product));
    }

    [Theory]
    [InlineData(ChargeOption.Flat, ChargeProduct.Savings, true)]
    [InlineData(ChargeOption.Percentage, ChargeProduct.Client, true)]
    [InlineData(ChargeOption.InstallmentPrincipalDue, ChargeProduct.Loan, true)]
    [InlineData(ChargeOption.InstallmentPrincipalDue, ChargeProduct.Savings, false)]
    [InlineData(ChargeOption.TotalOutstanding, ChargeProduct.Shares, false)]
    [InlineData(ChargeOption.OriginalPrincipal, ChargeProduct.Client, false)]
    public void ChargeOption_installment_and_schedule_variants_are_loan_only(ChargeOption option, ChargeProduct product, bool expected)
    {
        Assert.Equal(expected, ChargeValidationRules.IsValidChargeOptionForProduct(option, product));
    }
}
