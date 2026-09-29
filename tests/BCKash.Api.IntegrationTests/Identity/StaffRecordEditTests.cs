using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Identity;

/// <summary>A super admin editing a staff member's whole record from the control portal.</summary>
public class StaffRecordEditTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public StaffRecordEditTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static object Edit(string email, string? bankAccountNumber = "0123456789") => new
    {
        email,
        firstName = "Ngozi",
        lastName = "Eze",
        phone = "08031234567",
        gender = "Female",
        address = "4 Broad Street, Lagos",
        notes = "Moved desks",
        dateOfBirth = "1990-05-01",
        nextOfKinName = "Obi Eze",
        nextOfKinPhone = "08037654321",
        nextOfKinRelationship = "Brother",
        bankName = "Access Bank",
        bankAccountNumber,
        bankAccountName = "Ngozi Eze",
    };

    private async Task<int> SeedStaffAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var office = await TestDataSeeder.SeedOfficeAsync(db);
        return (await TestDataSeeder.SeedTypedUserAsync(db, email, "Correct-Password1!", UserTypeSlugs.Marketer, office.Id)).Id;
    }

    [Fact]
    public async Task A_super_admin_can_edit_every_detail_of_a_staff_record()
    {
        var client = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "record-edit-super@bckash.test");
        var staffId = await SeedStaffAsync("record-edit-before@bckash.test");

        var response = await client.PutAsJsonAsync($"/api/v1/users/{staffId}/record", Edit("record-edit-after@bckash.test"), TestJson.Options);

        response.EnsureSuccessStatusCode();
        var staff = await response.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
        Assert.Equal("record-edit-after@bckash.test", staff!.Email);
        Assert.Equal("Ngozi", staff.FirstName);
        Assert.Equal("+2348031234567", staff.Phone);
        Assert.Equal("+2348037654321", staff.NextOfKinPhone);
        Assert.Equal(new DateOnly(1990, 5, 1), staff.DateOfBirth);
        Assert.Equal("0123456789", staff.BankAccountNumber);
        Assert.Equal("Moved desks", staff.Notes);
        Assert.NotNull(staff.UpdatedByName);
    }

    [Fact]
    public async Task Editing_to_an_email_another_staff_member_uses_is_a_conflict()
    {
        var client = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "record-edit-dupe-super@bckash.test");
        var staffId = await SeedStaffAsync("record-edit-dupe@bckash.test");
        await SeedStaffAsync("record-edit-taken@bckash.test");

        var response = await client.PutAsJsonAsync($"/api/v1/users/{staffId}/record", Edit("Record-Edit-Taken@bckash.test"), TestJson.Options);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task An_invalid_bank_account_number_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "record-edit-bank-super@bckash.test");
        var staffId = await SeedStaffAsync("record-edit-bank@bckash.test");

        var response = await client.PutAsJsonAsync($"/api/v1/users/{staffId}/record", Edit("record-edit-bank@bckash.test", "12345"), TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_a_super_admin_can_edit_a_staff_record()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "record-edit-manager@bckash.test", "users.manage");
        var staffId = await SeedStaffAsync("record-edit-forbidden@bckash.test");

        var response = await client.PutAsJsonAsync($"/api/v1/users/{staffId}/record", Edit("record-edit-forbidden@bckash.test"), TestJson.Options);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
