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

        var response = await client.PostAsJsonAsync("/api/v1/charges", new SaveChargeRequest(
            "Disbursement Fee", null, ChargeProduct.Loan, ChargeType.Disbursement, ChargeOption.Flat,
            0, ChargeFrequencyType.Days, 0, 500m, null, null, ChargePaymentMode.Regular, false, false, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_savings_charge_type_used_with_product_loan_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "charge-mismatched-type@bckash.test", "organization.manage");

        var response = await client.PostAsJsonAsync("/api/v1/charges", new SaveChargeRequest(
            "Mismatched Charge", null, ChargeProduct.Loan, ChargeType.SavingsActivation, ChargeOption.Flat,
            0, ChargeFrequencyType.Days, 0, 500m, null, null, ChargePaymentMode.Regular, false, false, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_loan_only_charge_option_used_on_a_savings_charge_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "charge-mismatched-option@bckash.test", "organization.manage");

        var response = await client.PostAsJsonAsync("/api/v1/charges", new SaveChargeRequest(
            "Mismatched Option", null, ChargeProduct.Savings, ChargeType.WithdrawalFee, ChargeOption.InstallmentPrincipalDue,
            0, ChargeFrequencyType.Days, 0, 500m, null, null, ChargePaymentMode.Regular, false, false, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static SaveChargeRequest Penalty(string name, ChargeType type, ChargeOption option, decimal amount,
        int? grace = null, int? repeat = null, decimal? cap = null, int? freeAfter = null) =>
        new(name, null, ChargeProduct.Loan, type, option, 0, ChargeFrequencyType.Days, 0, amount, null, null,
            ChargePaymentMode.Regular, Penalty: false, Override: false, GlAccountIncomeId: null, grace, repeat, cap, freeAfter);

    [Fact]
    public async Task A_late_repayment_fee_is_always_saved_as_a_penalty_with_its_controls()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "charge-late@bckash.test", "organization.manage");

        // Sent with Penalty: false — the type decides.
        var response = await client.PostAsJsonAsync("/api/v1/charges",
            Penalty("Late repayment fee", ChargeType.OverdueInstallmentFee, ChargeOption.InstallmentTotalDue, 5, grace: 3, repeat: 30, cap: 10));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ChargeResponse>(TestJson.Options);
        Assert.True(created!.Penalty);
        Assert.Equal(3, created.GraceDays);
        Assert.Equal(30, created.RepeatEveryDays);
        Assert.Equal(10m, created.MaxTotalPercent);
    }

    [Fact]
    public async Task A_repeating_penalty_without_a_cap_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "charge-uncapped@bckash.test", "organization.manage");

        var response = await client.PostAsJsonAsync("/api/v1/charges",
            Penalty("Uncapped default penalty", ChargeType.OverdueMaturity, ChargeOption.TotalOutstanding, 2, grace: 30, repeat: 30));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Active_fee_names_are_unique_per_level_and_a_deactivated_fee_can_be_reactivated()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "charge-unique@bckash.test", "organization.manage");
        var groupFee = new SaveChargeRequest("Group registration", null, ChargeProduct.Group, ChargeType.Activation, ChargeOption.Flat,
            0, ChargeFrequencyType.Days, 0, 2000m, null, null, ChargePaymentMode.Regular, false, false, null);

        var first = await client.PostAsJsonAsync("/api/v1/charges", groupFee);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<ChargeResponse>(TestJson.Options);
        Assert.Equal(ChargeProduct.Group, created!.Product);

        var duplicate = await client.PostAsJsonAsync("/api/v1/charges", groupFee with { Name = " group REGISTRATION " });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        // The same name is fine at another level.
        var clientLevel = await client.PostAsJsonAsync("/api/v1/charges", groupFee with { Product = ChargeProduct.Client });
        Assert.Equal(HttpStatusCode.Created, clientLevel.StatusCode);

        await client.DeleteAsync($"/api/v1/charges/{created.Id}");
        var reactivated = await client.PostAsync($"/api/v1/charges/{created.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.True((await reactivated.Content.ReadFromJsonAsync<ChargeResponse>(TestJson.Options))!.Active);
    }
}
