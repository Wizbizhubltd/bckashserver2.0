using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Identity;

/// <summary>The data behind the control portal's staff record tabs.</summary>
public class StaffRecordTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public StaffRecordTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Activity_lists_only_that_staff_members_audit_entries_newest_first()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "staff-activity-admin@bckash.test", "users.manage");
        int staffId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var staff = await TestDataSeeder.SeedUserAsync(db, "staff-activity@bckash.test", "Correct-Password1!", permissionSlug: "users.manage");
            staffId = staff.Id;
            db.AuditTrail.AddRange(
                new AuditTrailEntry { UserId = staffId, Module = "Client", Action = "Create" },
                new AuditTrailEntry { UserId = staffId, Module = "Loan", Action = "Update" },
                new AuditTrailEntry { UserId = staffId + 1000, Module = "Loan", Action = "Delete" });
            await db.SaveChangesAsync();
        }

        var page = await client.GetFromJsonAsync<PagedResult<UserActivityResponse>>($"/api/v1/users/{staffId}/activity", TestJson.Options);

        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(["Update", "Create"], page.Items.Select(i => i.Action));
    }

    [Fact]
    public async Task Activity_for_an_unknown_staff_member_is_not_found()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "staff-activity-404@bckash.test", "users.manage");

        var response = await client.GetAsync("/api/v1/users/999999/activity");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Loans_can_be_filtered_by_loan_officer()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "staff-loans@bckash.test", "loan-applications.manage");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            db.Loans.AddRange(
                new Loan { LoanOfficerId = 4242, AccountNumber = "OFFICER-1" },
                new Loan { LoanOfficerId = 4242, AccountNumber = "OFFICER-2" },
                new Loan { LoanOfficerId = 4343, AccountNumber = "OTHER-1" });
            await db.SaveChangesAsync();
        }

        var page = await client.GetFromJsonAsync<PagedResult<LoanListItemResponse>>("/api/v1/loans?loanOfficerId=4242", TestJson.Options);

        Assert.Equal(2, page!.TotalCount);
        Assert.All(page.Items, l => Assert.StartsWith("OFFICER-", l.AccountNumber));
    }

    [Fact]
    public async Task Staff_record_includes_profile_details_and_who_created_it()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "staff-profile-admin@bckash.test", "users.manage");
        int staffId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var creator = await db.Users.SingleAsync(u => u.Email == "staff-profile-admin@bckash.test");
            creator.FirstName = "Ada";
            creator.LastName = "Obi";
            var staff = await TestDataSeeder.SeedUserAsync(db, "staff-profile@bckash.test", "Correct-Password1!", permissionSlug: "users.manage");
            staff.Address = "12 Allen Avenue, Ikeja";
            staff.Gender = Gender.Female;
            staff.CreatedById = creator.Id;
            await db.SaveChangesAsync();
            staffId = staff.Id;
        }

        var staffRecord = await client.GetFromJsonAsync<UserResponse>($"/api/v1/users/{staffId}", TestJson.Options);

        Assert.Equal("12 Allen Avenue, Ikeja", staffRecord!.Address);
        Assert.Equal(Gender.Female, staffRecord.Gender);
        Assert.Equal("Ada Obi", staffRecord.CreatedByName);
    }

    [Fact]
    public async Task A_staff_management_action_records_who_updated_the_record()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "staff-updater@bckash.test", "users.manage");
        int staffId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var admin = await db.Users.SingleAsync(u => u.Email == "staff-updater@bckash.test");
            admin.FirstName = "Tunde";
            admin.LastName = "Bello";
            var staff = await TestDataSeeder.SeedUserAsync(db, "staff-updated@bckash.test", "Correct-Password1!", permissionSlug: "users.manage");
            await db.SaveChangesAsync();
            staffId = staff.Id;
        }

        var before = await client.GetFromJsonAsync<UserResponse>($"/api/v1/users/{staffId}", TestJson.Options);
        Assert.Null(before!.UpdatedByName);

        var block = await client.PostAsync($"/api/v1/users/{staffId}/block", null);
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        var after = await block.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
        Assert.Equal("Tunde Bello", after!.UpdatedByName);
        Assert.NotNull(after.UpdatedAt);
    }
}
