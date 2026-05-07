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

    [Fact]
    public void CreateResponse_ShouldReadFreshInventoryOnRepeatedCalls()
    {
        // The reflection metadata is cached per Type but property.GetValue must
        // still read the live inventory snapshot on every call. A naive impl
        // that cached the value (instead of the PropertyInfo) would return the
        // stale collection from the first call.
        var container = new MutableInventoryContainer();

        container.inventory = [new InventoryItemResponse(0, 1, 1, 0)];
        var first = PlayerInventoryMapper.CreateResponse(container);
        Assert.Single(first.Items);
        Assert.Equal(1, first.Items[0].NetId);

        container.inventory = [new InventoryItemResponse(0, 99, 5, 0)];
        var second = PlayerInventoryMapper.CreateResponse(container);
        Assert.Single(second.Items);
        Assert.Equal(99, second.Items[0].NetId);
        Assert.Equal(5, second.Items[0].Stack);
    }

    private sealed class FakeInventoryContainer(InventoryItemResponse[] items)
    {
        public InventoryItemResponse[] inventory { get; } = items;
    }

    private sealed class MutableInventoryContainer
    {
        public InventoryItemResponse[] inventory { get; set; } = [];
    }
}
