using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Organization;
using Xunit;

namespace BCKash.Api.IntegrationTests.Organization;

public class ChargesControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ChargesControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task A_loan_scoped_charge_with_a_loan_only_option_is_accepted()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "charge-valid@bckash.test", "organization.manage");

        var response = await client.PostAsJsonAsync("/api/charges", new SaveChargeRequest(
            "Disbursement Fee", null, ChargeProduct.Loan, ChargeType.Disbursement, ChargeOption.Flat,
            0, ChargeFrequencyType.Days, 0, 500m, null, null, ChargePaymentMode.Regular, false, false, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_savings_charge_type_used_with_product_loan_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "charge-mismatched-type@bckash.test", "organization.manage");

        var response = await client.PostAsJsonAsync("/api/charges", new SaveChargeRequest(
            "Mismatched Charge", null, ChargeProduct.Loan, ChargeType.SavingsActivation, ChargeOption.Flat,
            0, ChargeFrequencyType.Days, 0, 500m, null, null, ChargePaymentMode.Regular, false, false, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_loan_only_charge_option_used_on_a_savings_charge_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "charge-mismatched-option@bckash.test", "organization.manage");

        var response = await client.PostAsJsonAsync("/api/charges", new SaveChargeRequest(
            "Mismatched Option", null, ChargeProduct.Savings, ChargeType.WithdrawalFee, ChargeOption.InstallmentPrincipalDue,
            0, ChargeFrequencyType.Days, 0, 500m, null, null, ChargePaymentMode.Regular, false, false, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
