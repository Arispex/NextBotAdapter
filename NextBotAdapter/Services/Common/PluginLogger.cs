using System;
using System.Text.RegularExpressions;
using TShockAPI;

namespace NextBotAdapter.Services;

public static class PluginLogger
{
    private const int MaxMessageLength = 300;
    // Single-pass collapse of any whitespace run (\r, \n, \t, space, etc.)
    // into a single space. Replaces the previous Replace+while loop, which
    // both allocated a fresh string per pass and re-scanned the buffer.
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

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
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        var normalized = WhitespaceRegex.Replace(message, " ").Trim();

        return normalized.Length <= MaxMessageLength
            ? normalized
            : normalized[..(MaxMessageLength - 3)] + "...";
    }
}
