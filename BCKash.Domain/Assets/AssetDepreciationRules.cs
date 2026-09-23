namespace BCKash.Domain.Assets;

/// <summary>One year's row in a depreciation schedule — mirrors <see cref="AssetDepreciation"/>'s columns.</summary>
public record AssetDepreciationYear(int YearNumber, decimal BeginningValue, decimal DepreciationValue, decimal Rate, decimal Accumulated, decimal EndingValue);

/// <summary>
/// Pure straight-line depreciation rules (BR-AST-2, FR-AST-2) — confirmed straight-line, not
/// declining-balance, per business sign-off (the FRD flagged this as needing confirmation
/// since the legacy schema's rate/cost/accumulated/ending_value columns didn't rule out
/// declining-balance). Straight-line: equal annual depreciation of
/// (cost - salvage value) / useful life, until the asset reaches its salvage value.
/// </summary>
public static class AssetDepreciationRules
{
    /// <summary>
    /// The full life-of-asset schedule, useful for previewing/validating an asset's setup.
    /// Depreciation stops once the ending value would go below the salvage value — the final
    /// year is truncated to land exactly on it rather than going negative.
    /// </summary>
    public static IReadOnlyList<AssetDepreciationYear> ComputeSchedule(decimal cost, decimal salvageValue, int usefulLifeYears)
    {
        if (usefulLifeYears <= 0 || cost <= salvageValue)
        {
            return [];
        }

        var annualDepreciation = Math.Round((cost - salvageValue) / usefulLifeYears, 2);
        var rate = Math.Round(100m / usefulLifeYears, 2);

        var years = new List<AssetDepreciationYear>();
        var beginning = cost;
        var accumulated = 0m;

        for (var year = 1; year <= usefulLifeYears; year++)
        {
            var isLastYear = year == usefulLifeYears;
            var depreciation = isLastYear ? beginning - salvageValue : annualDepreciation;
            if (depreciation < 0)
            {
                depreciation = 0;
            }

            accumulated += depreciation;
            var ending = beginning - depreciation;

            years.Add(new AssetDepreciationYear(year, beginning, depreciation, rate, accumulated, ending));
            beginning = ending;
        }

        return years;
    }

    /// <summary>
    /// Computes just the next due year's row, given how much has already been depreciated —
    /// what the annual depreciation job actually needs when running one asset for one year at a
    /// time rather than materializing the whole schedule up front. Returns null once the asset
    /// is already fully depreciated down to its salvage value.
    /// </summary>
    public static AssetDepreciationYear? ComputeNextYear(decimal cost, decimal salvageValue, int usefulLifeYears, decimal accumulatedSoFar, int yearNumber)
    {
        if (usefulLifeYears <= 0 || cost <= salvageValue)
        {
            return null;
        }

        var beginning = cost - accumulatedSoFar;
        if (beginning <= salvageValue)
        {
            return null;
        }

        var annualDepreciation = Math.Round((cost - salvageValue) / usefulLifeYears, 2);
        var isLastYear = yearNumber >= usefulLifeYears || beginning - annualDepreciation < salvageValue;
        var depreciation = isLastYear ? beginning - salvageValue : annualDepreciation;
        var rate = Math.Round(100m / usefulLifeYears, 2);
        var accumulated = accumulatedSoFar + depreciation;
        var ending = beginning - depreciation;

        return new AssetDepreciationYear(yearNumber, beginning, depreciation, rate, accumulated, ending);
    }
}
