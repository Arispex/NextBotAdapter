using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class UserInfoMapperTests
{
    [Fact]
    public void CreateResponse_ShouldMapPlayerDataFieldsToStableKeys()
    {
        var fakeData = new FakePlayerData();

        var response = UserInfoMapper.CreateResponse(fakeData);

        Assert.Equal(120, response.Health);
        Assert.Equal(400, response.MaxHealth);
        Assert.Equal(50, response.Mana);
        Assert.Equal(200, response.MaxMana);
        Assert.Equal(8, response.QuestsCompleted);
        Assert.Equal(6, response.DeathsPve);
        Assert.Equal(2, response.DeathsPvp);
    }

    private sealed class FakePlayerData
    {
        private int health { get; } = 120;
        private int maxHealth { get; } = 400;
        private int mana { get; } = 50;
        private int maxMana { get; } = 200;
        private int questsCompleted { get; } = 8;
        private int deathsPVE { get; } = 6;
        private int deathsPVP { get; } = 2;
    }
}
