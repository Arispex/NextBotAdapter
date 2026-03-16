using System;
using TShockAPI;

namespace NextBotAdapter.Services;

public static class PluginLogger
{
    private const int MaxMessageLength = 300;

    public static string Format(string level, string message)
        => $"[{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}] [{level}] [NextBotAdapter] {Normalize(message)}";

    public static void Info(string message)
    {
        TShock.Log?.ConsoleInfo(Format("INFO", message));
    }

    public static void Warn(string message)
    {
        TShock.Log?.ConsoleWarn(Format("WARN", message));
    }

    public static void Error(string message)
    {
        TShock.Log?.ConsoleError(Format("ERROR", message));
    }

    private static string Normalize(string message)
    {
        var normalized = message
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ");

        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        normalized = normalized.Trim();

        return normalized.Length <= MaxMessageLength
            ? normalized
            : normalized[..(MaxMessageLength - 3)] + "...";
    }
}
