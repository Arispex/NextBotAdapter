using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NextBotAdapter.Models;

namespace NextBotAdapter.Services;

public sealed class PluginConfigService
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented
    };
    private readonly string _configDirectoryPath;

    public PluginConfigService()
        : this(Path.Combine(TShockAPI.TShock.SavePath, "NextBotAdapter"))
    {
    }

    public PluginConfigService(string configDirectoryPath)
    {
        _configDirectoryPath = configDirectoryPath;
    }

    public string ConfigDirectoryPath => _configDirectoryPath;
    public string ConfigFilePath => Path.Combine(ConfigDirectoryPath, "NextBotAdapter.json");

    public void EnsureConfigComplete()
    {
        EnsureDirectory();
        if (!File.Exists(ConfigFilePath))
        {
            WriteConfigFile(NextBotAdapterConfig.Default);
            PluginLogger.Info("默认插件配置文件已创建。");
            return;
        }

        try
        {
            var originalText = File.ReadAllText(ConfigFilePath);
            var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(originalText, JsonSettings);
            var complete = (config ?? NextBotAdapterConfig.Default).WithDefaults();
            var completeText = JsonConvert.SerializeObject(complete, JsonSettings);

            if (originalText != completeText)
            {
                File.WriteAllText(ConfigFilePath, completeText);
                PluginLogger.Info("配置文件已自动补全缺失字段。");
            }
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"配置文件补全检查失败，原因：{ex.Message}");
        }
    }

    public WhitelistSettings LoadWhitelistSettings()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return WhitelistSettings.Default;
        }

        try
        {
            var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(File.ReadAllText(ConfigFilePath), JsonSettings);
            return config?.Whitelist ?? WhitelistSettings.Default;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"白名单配置加载失败，已回退到默认配置，原因：{ex.Message}");
            return WhitelistSettings.Default;
        }
    }

    public LoginConfirmationSettings LoadLoginConfirmationSettings()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return LoginConfirmationSettings.Default;
        }

        try
        {
            var config = JsonConvert.DeserializeObject<NextBotAdapterConfig>(File.ReadAllText(ConfigFilePath), JsonSettings);
            return config?.LoginConfirmation ?? LoginConfirmationSettings.Default;
        }
        catch (Exception ex)
        {
            PluginLogger.Warn($"登入确认配置加载失败，已回退到默认配置，原因：{ex.Message}");
            return LoginConfirmationSettings.Default;
        }
    }

    public string ReadConfigRaw()
    {
        EnsureDirectory();
        return File.Exists(ConfigFilePath)
            ? File.ReadAllText(ConfigFilePath)
            : JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings);
    }

    public bool TryUpdateConfig(IEnumerable<KeyValuePair<string, string>> fields, out string? error)
    {
        EnsureDirectory();
        var text = File.Exists(ConfigFilePath)
            ? File.ReadAllText(ConfigFilePath)
            : JsonConvert.SerializeObject(NextBotAdapterConfig.Default, JsonSettings);

        var root = JObject.Parse(text);

        foreach (var (path, value) in fields)
        {
            var token = root.SelectToken(path);
            if (token is null)
            {
                error = $"Unknown config field '{path}'.";
                return false;
            }

            token.Replace(ParseValue(value));
        }

        File.WriteAllText(ConfigFilePath, root.ToString(Formatting.Indented));
        error = null;
        return true;
    }

    internal void EnsureDirectory()
    {
        Directory.CreateDirectory(ConfigDirectoryPath);
    }

    private static JToken ParseValue(string value)
    {
        if (bool.TryParse(value, out var b)) return new JValue(b);
        if (long.TryParse(value, out var l)) return new JValue(l);
        if (double.TryParse(value, out var d)) return new JValue(d);
        return new JValue(value);
    }

    private void WriteConfigFile(NextBotAdapterConfig config)
    {
        File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(config, JsonSettings));
    }
}
