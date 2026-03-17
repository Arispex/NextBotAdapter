namespace NextBotAdapter.Services;

public interface IMapImageService
{
    (string FileName, string FilePath, byte[] Content) GenerateAndCache();
}
