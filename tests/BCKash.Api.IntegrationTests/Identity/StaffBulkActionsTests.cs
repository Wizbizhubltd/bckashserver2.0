using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Identity;

/// <summary>The staff directory's bulk actions: forced password reset and office transfer.</summary>
public class StaffBulkActionsTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public StaffBulkActionsTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private RecordingEmailSender Emails => (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();

    [Fact]
    public async Task Forced_reset_emails_an_8_character_password_with_the_staff_portal_link_and_skips_blocked_staff()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "bulk-reset-admin@bckash.test", "users.manage");
        int activeId, blockedId;
        string oldHash;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            var active = await TestDataSeeder.SeedTypedUserAsync(db, "bulk-reset-active@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, office.Id);
            var blocked = await TestDataSeeder.SeedTypedUserAsync(db, "bulk-reset-blocked@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, office.Id);
            blocked.Blocked = true;
            await db.SaveChangesAsync();
            (activeId, blockedId, oldHash) = (active.Id, blocked.Id, active.PasswordHash!);
        }

        var response = await client.PostAsJsonAsync("/api/v1/users/bulk/reset-password", new { userIds = new[] { activeId, blockedId } }, TestJson.Options);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkActionResponse>(TestJson.Options);
        Assert.Equal(1, result!.Succeeded);
        Assert.Equal(blockedId, Assert.Single(result.Skipped).Id);

        var email = Emails.Sent.Last(e => e.ToAddress == "bulk-reset-active@bckash.test");
        Assert.Contains("https://office.bckash.test", email.Body);
        var password = email.Body.Split('\n').Single(l => l.StartsWith("Temporary password: ")).Replace("Temporary password: ", string.Empty).Trim();
        Assert.Equal(8, password.Length);
        Assert.DoesNotContain(Emails.Sent, e => e.ToAddress == "bulk-reset-blocked@bckash.test");

        using var verify = _factory.Services.CreateScope();
        var user = await verify.ServiceProvider.GetRequiredService<BCKashDbContext>().Users.SingleAsync(u => u.Id == activeId);
        Assert.NotEqual(oldHash, user.PasswordHash);
        Assert.True(user.MustChangePassword);
    }

    [Fact]
    public async Task Forced_reset_for_all_staff_uses_the_list_filters()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "bulk-reset-all-admin@bckash.test", "users.manage");
        int officeId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            var otherOffice = await TestDataSeeder.SeedOfficeAsync(db);
            await TestDataSeeder.SeedTypedUserAsync(db, "bulk-all-1@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, office.Id);
            await TestDataSeeder.SeedTypedUserAsync(db, "bulk-all-2@bckash.test", "Correct-Password1!", UserTypeSlugs.Manager, office.Id);
            await TestDataSeeder.SeedTypedUserAsync(db, "bulk-all-elsewhere@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, otherOffice.Id);
            officeId = office.Id;
        }

        var response = await client.PostAsJsonAsync("/api/v1/users/bulk/reset-password", new { all = true, officeId }, TestJson.Options);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkActionResponse>(TestJson.Options);
        Assert.Equal(2, result!.Succeeded);
        Assert.Empty(result.Skipped);
        Assert.DoesNotContain(Emails.Sent, e => e.ToAddress == "bulk-all-elsewhere@bckash.test");
    }

    [Fact]
    public async Task Transfer_moves_active_staff_emails_them_and_keeps_staff_whose_clients_have_active_loans()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "bulk-transfer-admin@bckash.test", "users.manage");
        int freeId, withLoanId, blockedId, targetOfficeId, fromOfficeId;
        string targetOfficeName;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var from = await TestDataSeeder.SeedOfficeAsync(db);
            var target = await TestDataSeeder.SeedOfficeAsync(db);
            var free = await TestDataSeeder.SeedTypedUserAsync(db, "bulk-transfer-free@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, from.Id);
            var withLoan = await TestDataSeeder.SeedTypedUserAsync(db, "bulk-transfer-loan@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, from.Id);
            var blocked = await TestDataSeeder.SeedTypedUserAsync(db, "bulk-transfer-blocked@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, from.Id);
            blocked.Blocked = true;

            // A closed loan on the free staff member's client doesn't hold them back; a running one does.
            var freeClient = new Client { FirstName = "Closed", LastName = "Loan", OfficeId = from.Id, StaffId = free.Id, CreatedAt = DateTime.UtcNow };
            var loanClient = new Client { FirstName = "Running", LastName = "Loan", OfficeId = from.Id, StaffId = withLoan.Id, CreatedAt = DateTime.UtcNow };
            db.Clients.AddRange(freeClient, loanClient);
            await db.SaveChangesAsync();
            db.Loans.AddRange(
                new Loan { ClientId = freeClient.Id, Status = LoanStatus.Closed, AccountNumber = "BULK-CLOSED" },
                new Loan { ClientId = loanClient.Id, Status = LoanStatus.Disbursed, AccountNumber = "BULK-RUNNING" });
            await db.SaveChangesAsync();

            (freeId, withLoanId, blockedId) = (free.Id, withLoan.Id, blocked.Id);
            (targetOfficeId, targetOfficeName, fromOfficeId) = (target.Id, target.Name!, from.Id);
        }

        var response = await client.PostAsJsonAsync("/api/v1/users/bulk/transfer", new { userIds = new[] { freeId, withLoanId, blockedId }, officeId = targetOfficeId }, TestJson.Options);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkActionResponse>(TestJson.Options);
        Assert.Equal(1, result!.Succeeded);
        Assert.Equal(new[] { withLoanId, blockedId }.Order(), result.Skipped.Select(s => s.Id).Order());
        Assert.Equal("Has clients with active loans.", result.Skipped.Single(s => s.Id == withLoanId).Reason);

        using var verify = _factory.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<BCKashDbContext>();
        Assert.Equal(targetOfficeId, (await db2.Users.SingleAsync(u => u.Id == freeId)).OfficeId);
        Assert.Equal(fromOfficeId, (await db2.Users.SingleAsync(u => u.Id == withLoanId)).OfficeId);
        Assert.Equal(fromOfficeId, (await db2.Users.SingleAsync(u => u.Id == blockedId)).OfficeId);

        var email = Emails.Sent.Last(e => e.ToAddress == "bulk-transfer-free@bckash.test");
        Assert.Contains(targetOfficeName, email.Subject);
        Assert.Contains("https://office.bckash.test", email.Body);
        Assert.DoesNotContain(Emails.Sent, e => e.ToAddress == "bulk-transfer-loan@bckash.test");
    }

    [Fact]
    public async Task Disable_blocks_active_staff()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "bulk-disable-admin@bckash.test", "users.manage");
        int staffId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            staffId = (await TestDataSeeder.SeedTypedUserAsync(db, "bulk-disable-staff@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, office.Id)).Id;
        }

        var response = await client.PostAsJsonAsync("/api/v1/users/bulk/disable", new { userIds = new[] { staffId } }, TestJson.Options);

        response.EnsureSuccessStatusCode();
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<BulkActionResponse>(TestJson.Options))!.Succeeded);
        using var verify = _factory.Services.CreateScope();
        Assert.True((await verify.ServiceProvider.GetRequiredService<BCKashDbContext>().Users.SingleAsync(u => u.Id == staffId)).Blocked);
    }

    [Theory]
    [InlineData("reset-password")]
    [InlineData("transfer")]
    [InlineData("disable")]
    public async Task Super_admins_are_exempt_from_every_bulk_action(string action)
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, $"bulk-exempt-admin-{action}@bckash.test", "users.manage");
        int superAdminId, targetOfficeId;
        string hash;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            targetOfficeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            var superAdmin = await TestDataSeeder.SeedTypedUserAsync(db, $"bulk-exempt-{action}@bckash.test", "Correct-Password1!", UserTypeSlugs.SuperAdmin, office.Id);
            (superAdminId, hash) = (superAdmin.Id, superAdmin.PasswordHash!);
        }

        var response = await client.PostAsJsonAsync($"/api/v1/users/bulk/{action}", new { userIds = new[] { superAdminId }, officeId = targetOfficeId }, TestJson.Options);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkActionResponse>(TestJson.Options);
        Assert.Equal(0, result!.Succeeded);
        Assert.Equal("Super admins are exempt from bulk actions.", Assert.Single(result.Skipped).Reason);

        using var verify = _factory.Services.CreateScope();
        var user = await verify.ServiceProvider.GetRequiredService<BCKashDbContext>().Users.SingleAsync(u => u.Id == superAdminId);
        Assert.False(user.Blocked);
        Assert.Equal(hash, user.PasswordHash);
        Assert.NotEqual(targetOfficeId, user.OfficeId);
    }

    [Fact]
    public async Task Transfer_to_an_unknown_office_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "bulk-transfer-404@bckash.test", "users.manage");

        var response = await client.PostAsJsonAsync("/api/v1/users/bulk/transfer", new { userIds = new[] { 1 }, officeId = 999999 }, TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
