using NextBotAdapter.Models.Responses;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class PlayerInventoryMapperTests
{
    [Fact]
    public void CreateResponse_ShouldMapInventoryItemResponseCollection()
    {
        var playerData = new FakeInventoryContainer(
            [
                new InventoryItemResponse(5, 10, 20, 30),
                new InventoryItemResponse(8, 40, 50, 60)
            ]);

        var response = PlayerInventoryMapper.CreateResponse(playerData);

        Assert.Collection(
            response.Items,
            item =>
            {
                Assert.Equal(0, item.Slot);
                Assert.Equal(10, item.NetId);
                Assert.Equal(20, item.Stack);
                Assert.Equal(30, item.PrefixId);
            },
            item =>
            {
                Assert.Equal(1, item.Slot);
                Assert.Equal(40, item.NetId);
                Assert.Equal(50, item.Stack);
                Assert.Equal(60, item.PrefixId);
            });
    }

    [Fact]
    public void CreateResponse_ShouldReturnEmptyCollectionWhenInventoryIsMissing()
    {
        var response = PlayerInventoryMapper.CreateResponse(new object());

        Assert.Empty(response.Items);
    }

    private sealed class FakeInventoryContainer(InventoryItemResponse[] items)
    {
        public InventoryItemResponse[] inventory { get; } = items;
    }
}
