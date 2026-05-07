using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class PlayerStatisticsReaderTests
{
    [Fact]
    public void ReadDeaths_ShouldReturnZeroWhenSourceIsNull()
    {
        var result = PlayerStatisticsReader.ReadDeaths(null, "deathsPVE");

        Assert.Equal(0, result);
    }

    [Fact]
    public void ReadDeaths_ShouldReturnZeroWhenFieldDoesNotExist()
    {
        var result = PlayerStatisticsReader.ReadDeaths(new FakeStatsSource(), "missingField");

        Assert.Equal(0, result);
    }

    [Fact]
    public void ReadDeaths_ShouldReadPrivateIntegerProperty()
    {
        var result = PlayerStatisticsReader.ReadDeaths(new FakeStatsSource(), "deathsPVE");

        Assert.Equal(7, result);
    }

    [Fact]
    public void ReadDeaths_ShouldReturnFreshValueOnRepeatedCalls_WhenSourceMutates()
    {
        // The reflection metadata is cached but property.GetValue(source) must
        // still read the live value on every call. A naive implementation that
        // cached the value (instead of the PropertyInfo) would return a stale
        // 0 here.
        var source = new MutableStatsSource();
        Assert.Equal(0, PlayerStatisticsReader.ReadDeaths(source, "DeathsPVE"));

        source.DeathsPVE = 42;
        Assert.Equal(42, PlayerStatisticsReader.ReadDeaths(source, "DeathsPVE"));

        source.DeathsPVE = 99;
        Assert.Equal(99, PlayerStatisticsReader.ReadDeaths(source, "DeathsPVE"));
    }

    [Fact]
    public void ReadDeaths_ShouldHandleManyRepeatedCallsWithoutThrowing()
    {
        // A lightweight smoke test that the cache stays consistent across many
        // calls of the same (Type, fieldName). With the reflection cache, the
        // PropertyInfo lookup runs once and subsequent calls hit the dictionary.
        var source = new FakeStatsSource();
        for (var i = 0; i < 10_000; i++)
        {
            Assert.Equal(7, PlayerStatisticsReader.ReadDeaths(source, "deathsPVE"));
        }
    }

    private sealed class FakeStatsSource
    {
        private int deathsPVE { get; } = 7;
    }

    private sealed class MutableStatsSource
    {
        public int DeathsPVE { get; set; }
    }
}
