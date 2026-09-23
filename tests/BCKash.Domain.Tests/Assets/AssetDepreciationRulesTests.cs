using BCKash.Domain.Assets;
using Xunit;

namespace BCKash.Domain.Tests.Assets;

/// <summary>
/// Straight-line depreciation schedule correctness — Phase 8's third acceptance criterion
/// (confirmed straight-line per business sign-off; see AssetDepreciationRules' doc comment).
/// </summary>
public class AssetDepreciationRulesTests
{
    [Fact]
    public void Full_schedule_depreciates_evenly_to_salvage_value()
    {
        // Cost 10,000, salvage 1,000, 3-year life -> 3,000/year straight-line.
        var schedule = AssetDepreciationRules.ComputeSchedule(cost: 10_000m, salvageValue: 1_000m, usefulLifeYears: 3);

        Assert.Equal(3, schedule.Count);

        Assert.Equal(10_000m, schedule[0].BeginningValue);
        Assert.Equal(3_000m, schedule[0].DepreciationValue);
        Assert.Equal(3_000m, schedule[0].Accumulated);
        Assert.Equal(7_000m, schedule[0].EndingValue);

        Assert.Equal(7_000m, schedule[1].BeginningValue);
        Assert.Equal(3_000m, schedule[1].DepreciationValue);
        Assert.Equal(6_000m, schedule[1].Accumulated);
        Assert.Equal(4_000m, schedule[1].EndingValue);

        Assert.Equal(4_000m, schedule[2].BeginningValue);
        Assert.Equal(3_000m, schedule[2].DepreciationValue);
        Assert.Equal(9_000m, schedule[2].Accumulated);
        Assert.Equal(1_000m, schedule[2].EndingValue); // lands exactly on salvage value
    }

    [Fact]
    public void Schedule_rate_is_one_over_useful_life_as_a_percentage()
    {
        var schedule = AssetDepreciationRules.ComputeSchedule(cost: 10_000m, salvageValue: 0m, usefulLifeYears: 4);
        Assert.All(schedule, year => Assert.Equal(25m, year.Rate)); // 100/4
    }

    [Fact]
    public void Invalid_configuration_produces_no_schedule()
    {
        Assert.Empty(AssetDepreciationRules.ComputeSchedule(cost: 10_000m, salvageValue: 10_000m, usefulLifeYears: 5)); // cost == salvage
        Assert.Empty(AssetDepreciationRules.ComputeSchedule(cost: 10_000m, salvageValue: 1_000m, usefulLifeYears: 0)); // no life span
    }

    [Fact]
    public void ComputeNextYear_matches_the_full_schedule_year_by_year()
    {
        var fullSchedule = AssetDepreciationRules.ComputeSchedule(cost: 9_000m, salvageValue: 900m, usefulLifeYears: 3);

        var accumulated = 0m;
        for (var i = 0; i < fullSchedule.Count; i++)
        {
            var next = AssetDepreciationRules.ComputeNextYear(9_000m, 900m, 3, accumulated, i + 1);
            Assert.NotNull(next);
            Assert.Equal(fullSchedule[i], next);
            accumulated = next!.Accumulated;
        }

        // Fully depreciated — no further year to run.
        Assert.Null(AssetDepreciationRules.ComputeNextYear(9_000m, 900m, 3, accumulated, fullSchedule.Count + 1));
    }
}
