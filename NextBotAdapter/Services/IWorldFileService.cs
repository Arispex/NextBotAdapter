namespace NextBotAdapter.Services;

public interface IWorldFileService
{
    (string FileName, byte[] Content) GetWorldFile();
}
