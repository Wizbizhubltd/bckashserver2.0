using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Expenses;
using Xunit;

namespace BCKash.Api.IntegrationTests.Expenses;

/// <summary>
/// Phase 8's second acceptance criterion, at the service/API level (the pure "advance to next
/// occurrence" math is covered by ExpenseRecurrenceRulesTests) — a due recurring expense
/// generates a new Pending occurrence and moves its own RecurNextDate forward.
/// </summary>
public class ExpenseRecurringGenerationTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ExpenseRecurringGenerationTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Due_recurring_expense_generates_a_new_pending_occurrence_and_advances_its_own_next_date()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "expense-recurring@bckash.test", ["expenses.manage"]);

        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1); // already due
        var created = await (await client.PostAsJsonAsync("/api/expenses", new SaveExpenseRequest(
            null, null, "Monthly rent", 10_000m, dueDate, true, "1", dueDate, null, ExpenseRecurType.Month, null, null)))
            .Content.ReadFromJsonAsync<ExpenseResponse>(TestJson.Options);

        var beforeCount = (await client.GetFromJsonAsync<PagedResult<ExpenseResponse>>("/api/expenses?pageSize=100", TestJson.Options))!.TotalCount;

        var runResponse = await client.PostAsync("/api/expenses/run-recurring", content: null);
        Assert.True(runResponse.IsSuccessStatusCode);

        var afterList = await client.GetFromJsonAsync<PagedResult<ExpenseResponse>>("/api/expenses?pageSize=100", TestJson.Options);
        Assert.Equal(beforeCount + 1, afterList!.TotalCount);

        var occurrence = afterList.Items.Single(e => e.Id != created!.Id);
        Assert.Equal(dueDate, occurrence.Date);
        Assert.Equal(ApprovalStatus.Pending, occurrence.Status); // recurrence doesn't bypass approval
        Assert.False(occurrence.Recurring); // the clone itself doesn't recur further

        var template = afterList.Items.Single(e => e.Id == created!.Id);
        Assert.Equal(dueDate.AddMonths(1), template.RecurNextDate);

        // Running again immediately produces no further occurrences — nothing else is due yet.
        await client.PostAsync("/api/expenses/run-recurring", content: null);
        var unchanged = await client.GetFromJsonAsync<PagedResult<ExpenseResponse>>("/api/expenses?pageSize=100", TestJson.Options);
        Assert.Equal(afterList.TotalCount, unchanged!.TotalCount);
    }
}
