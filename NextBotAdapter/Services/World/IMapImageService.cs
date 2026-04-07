namespace NextBotAdapter.Services;

public interface IMapImageService
{
    (string FileName, byte[] Content) Generate();
}
