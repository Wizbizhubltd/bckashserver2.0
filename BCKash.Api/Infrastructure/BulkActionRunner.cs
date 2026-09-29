using BCKash.Api.Contracts;

namespace BCKash.Api.Infrastructure;

/// <summary>Runs a list page's bulk action one record at a time, counting successes and collecting why the rest were skipped.</summary>
public static class BulkActionRunner
{
    /// <param name="records">The ticked records the caller can see, in the order to process them.</param>
    /// <param name="requestedIds">Every id that was ticked; any not among <paramref name="records"/> is reported as not found.</param>
    /// <param name="action">Returns null on success, otherwise why the record was skipped.</param>
    public static async Task<BulkActionResponse> RunAsync<T>(
        IReadOnlyList<T> records,
        IEnumerable<int> requestedIds,
        Func<T, int> idOf,
        Func<T, string> nameOf,
        Func<T, Task<string?>> action,
        string noun)
    {
        var succeeded = 0;
        var skipped = new List<BulkActionSkip>();
        foreach (var record in records)
        {
            if (await action(record) is { } reason)
            {
                skipped.Add(new BulkActionSkip(idOf(record), nameOf(record), reason));
            }
            else
            {
                succeeded++;
            }
        }

        var foundIds = records.Select(idOf).ToHashSet();
        skipped.AddRange(requestedIds.Distinct().Where(id => !foundIds.Contains(id)).Select(id => new BulkActionSkip(id, $"{noun} #{id}", "Not found.")));
        return new BulkActionResponse(succeeded, skipped);
    }
}
