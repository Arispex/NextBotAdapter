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

    private sealed class FakeStatsSource
    {
        private int deathsPVE { get; } = 7;
    }
}
