using System.Text.RegularExpressions;
using NextBotAdapter.Services;

namespace NextBotAdapter.Tests;

public sealed class PluginLoggerTests
{
    [Theory]
    [InlineData("INFO", "加载白名单配置成功。")]
    [InlineData("WARN", "处理配置热重载请求失败，原因：timeout")]
    [InlineData("ERROR", "处理配置热重载请求失败，原因：boom")]
    public void Format_ShouldIncludeTimestampLevelAndPluginPrefix(string level, string message)
    {
        var formatted = PluginLogger.Format(level, message);

        Assert.Matches(
            new Regex($@"^\[\d{{4}}-\d{{2}}-\d{{2}}T\d{{2}}:\d{{2}}:\d{{2}}\.\d{{3}}(Z|[+-]\d{{2}}:\d{{2}})\] \[{level}\] \[NextBotAdapter\] {Regex.Escape(message)}$"),
            formatted);
    }

    [Fact]
    public void Format_ShouldNormalizeControlCharactersIntoSingleLine()
    {
        var formatted = PluginLogger.Format("WARN", "处理配置热重载请求失败，原因：line1\nline2\tline3");

        Assert.DoesNotContain('\n', formatted);
        Assert.DoesNotContain('\r', formatted);
        Assert.DoesNotContain('\t', formatted);
        Assert.Contains("[WARN] [NextBotAdapter]", formatted);
        Assert.Contains("line1 line2 line3", formatted);
    }

    [Fact]
    public void Format_ShouldTruncateOverlongDynamicContent()
    {
        var formatted = PluginLogger.Format("ERROR", new string('x', 600));
        var prefixMatch = Regex.Match(formatted, @"^\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}(Z|[+-]\d{2}:\d{2})\] \[ERROR\] \[NextBotAdapter\] ");

        Assert.True(prefixMatch.Success);

        var messageBody = formatted[prefixMatch.Length..];
        Assert.Equal(300, messageBody.Length);
        Assert.EndsWith("...", messageBody);
        Assert.Equal(new string('x', 297) + "...", messageBody);
    }
}
