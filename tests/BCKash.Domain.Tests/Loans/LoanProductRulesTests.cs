using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Domain.Tests.Loans;

public class LoanProductRulesTests
{
    private static LoanProduct Valid() => new()
    {
        Name = "Salary Advance", MinimumPrincipal = 10_000, DefaultPrincipal = 50_000, MaximumPrincipal = 200_000,
        MinimumLoanTerm = 1, DefaultLoanTerm = 3, MaximumLoanTerm = 6, RepaymentFrequency = 1,
        MinimumInterestRate = 3, DefaultInterestRate = 5, MaximumInterestRate = 7,
    };

    [Fact]
    public void A_complete_product_is_valid() => Assert.Null(LoanProductRules.Validate(Valid()));

    [Fact]
    public void A_zero_minimum_means_no_minimum()
    {
        var product = Valid();
        product.MinimumPrincipal = 0;
        Assert.Null(LoanProductRules.Validate(product));
    }

    public static TheoryData<string, Action<LoanProduct>> Invalid => new()
    {
        { "no name", p => p.Name = " " },
        { "zero maximum amount", p => p.MaximumPrincipal = 0 },
        { "negative amount", p => p.MinimumPrincipal = -1 },
        { "zero term", p => p.MinimumLoanTerm = 0 },
        { "no repayment interval", p => p.RepaymentFrequency = 0 },
        { "interest over 100%", p => p.MaximumInterestRate = 120 },
        { "default above maximum", p => p.DefaultPrincipal = 500_000 },
        { "principal grace as long as the loan", p => p.GraceOnPrincipal = 6 },
        { "negative arrears days", p => p.ArrearsGraceDays = -1 },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Invalid_products_are_rejected(string _, Action<LoanProduct> breakIt)
    {
        var product = Valid();
        breakIt(product);
        Assert.NotNull(LoanProductRules.Validate(product));
    }
}
