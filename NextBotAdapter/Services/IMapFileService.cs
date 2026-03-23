namespace NextBotAdapter.Services;

public interface IMapFileService
{
    (string FileName, byte[] Content) GetMapFile();
}
