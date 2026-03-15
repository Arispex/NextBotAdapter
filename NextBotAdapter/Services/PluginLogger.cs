using TShockAPI;

namespace NextBotAdapter.Services;

public static class PluginLogger
{
    public static string Format(string category, string message)
        => $"[NextBotAdapter][{category}] {message}";

    public static void Info(string category, string message)
    {
        TShock.Log?.ConsoleInfo(Format(category, message));
    }

    public static void Warn(string category, string message)
    {
        TShock.Log?.ConsoleWarn(Format(category, message));
    }

    public static void Error(string category, string message)
    {
        TShock.Log?.ConsoleError(Format(category, message));
    }
}
