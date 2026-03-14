using NextBotAdapter.Infrastructure;
using NextBotAdapter.Models;
using NextBotAdapter.Models.Responses;
using NextBotAdapter.Rest;

namespace NextBotAdapter.Tests;

public sealed class ModelAndPluginCoverageTests
{
    [Fact]
    public void InventoryItemResponse_ShouldStoreConstructorValues()
    {
        var item = new InventoryItemResponse(4, 99, 20, 7);

        Assert.Equal(4, item.Slot);
        Assert.Equal(99, item.NetId);
        Assert.Equal(20, item.Stack);
        Assert.Equal(7, item.PrefixId);
    }

    [Fact]
    public void UserInventoryResponse_ShouldExposeItemsCollection()
    {
        var items = new[] { new InventoryItemResponse(0, 1, 2, 3) };
        var response = new UserInventoryResponse(items);

        Assert.Same(items, response.Items);
    }

    [Fact]
    public void ApiEnvelope_ShouldStoreDataAndErrorValues()
    {
        var error = new ApiError("message");
        var envelope = new ApiEnvelope<string>(null, error);

        Assert.Null(envelope.Data);
        Assert.Equal(error, envelope.Error);
    }

    [Fact]
    public void UserLookupError_ShouldStoreCodeAndMessage()
    {
        var error = new UserLookupError(ErrorCodes.UserNotFound, "User was not found.");

        Assert.Equal(ErrorCodes.UserNotFound, error.Code);
        Assert.Equal("User was not found.", error.Message);
    }

    [Fact]
    public void EndpointRegistrar_CreateCommands_ShouldUseSecureRestCommands()
    {
        var commands = EndpointRegistrar.CreateCommands();

        Assert.All(commands, command => Assert.Equal("Rests.SecureRestCommand", command.GetType().FullName));
    }
}
