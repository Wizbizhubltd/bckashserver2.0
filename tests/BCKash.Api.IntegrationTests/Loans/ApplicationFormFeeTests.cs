using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static BCKash.Api.IntegrationTests.Loans.LoanApplicationsControllerTests;

namespace BCKash.Api.IntegrationTests.Loans;

/// <summary>The application form fee: charged on every loan application, carried onto the loan, and printed on the client's loan form.</summary>
public class ApplicationFormFeeTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ApplicationFormFeeTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> SetFormFeeAsync(decimal amount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        foreach (var existing in await db.Charges.Where(c => c.ChargeType == ChargeType.ApplicationFormFee).ToListAsync())
        {
            existing.Active = false;
        }

        var charge = new Charge
        {
            Name = "Loan application form fee",
            Product = ChargeProduct.Loan,
            ChargeType = ChargeType.ApplicationFormFee,
            ChargeOption = ChargeOption.Flat,
            Amount = amount,
            Active = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Charges.Add(charge);
        await db.SaveChangesAsync();
        return charge.Id;
    }

    [Fact]
    public async Task The_form_fee_is_charged_on_the_application_and_carried_onto_the_approved_loan()
    {
        var chargeId = await SetFormFeeAsync(2000);
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "form-fee-approve@bckash.test", ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "Form Fee Product");
        var clientId = await CreateClientAsync(client, "FormFeeApplicant");

        var created = await (await client.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, clientId))).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        Assert.Equal(2000, created!.FormFee);

        // A later change to the fee doesn't alter what this applicant was charged.
        await SetFormFeeAsync(3500);

        var approved = await (await client.PostAsJsonAsync($"/api/v1/loan-applications/{created.Id}/approve", new ApproveLoanApplicationRequest(5000, null))).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var charge = await db.LoanCharges.SingleAsync(c => c.LoanId == approved!.LoanId);
        Assert.Equal(2000, charge.Amount);
        Assert.Equal(chargeId, charge.ChargeId);
        Assert.Equal(LoanChargeType.Disbursement, charge.ChargeType);
    }

    [Fact]
    public async Task No_fee_is_charged_while_it_is_switched_off()
    {
        await SetFormFeeAsync(2000);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            await db.Charges.Where(c => c.ChargeType == ChargeType.ApplicationFormFee).ExecuteUpdateAsync(s => s.SetProperty(c => c.Active, false));
        }

        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "form-fee-off@bckash.test", ["loan-applications.manage", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "No Fee Product");
        var clientId = await CreateClientAsync(client, "NoFeeApplicant");

        var created = await (await client.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, clientId))).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);

        Assert.Null(created!.FormFee);
    }

    [Fact]
    public async Task The_loan_form_shows_the_proposed_loan_and_the_current_fee()
    {
        await SetFormFeeAsync(2500);
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "form-fee-print@bckash.test", ["loan-applications.manage", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "Loan Form Product");
        var clientId = await CreateClientAsync(client, "LoanFormApplicant");
        await client.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, clientId, amount: 8000, term: 20));

        var response = await client.GetAsync($"/api/v1/clients/{clientId}/loan-form");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var form = await response.Content.ReadFromJsonAsync<ClientLoanFormResponse>(TestJson.Options);
        Assert.Equal(8000, form!.ProposedLoanAmount);
        Assert.Equal(20, form.LoanTerm);
        Assert.Equal("Loan Form Product", form.LoanProductName);
        Assert.Equal(2500, form.FormFee);
        Assert.Empty(form.GroupMembers);
    }
}
