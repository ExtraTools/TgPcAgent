using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TgPcAgent.App.Models;

namespace TgPcAgent.App.Services;

public sealed class ConfigurationService
{
    private readonly object _sync = new();
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ConfigurationService()
    {
        BaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TgPcAgent");
        LogsDirectory = Path.Combine(BaseDirectory, "logs");
        ConfigPath = Path.Combine(BaseDirectory, "config.json");

        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(LogsDirectory);

        Current = LoadFromDisk();
    }

    public string BaseDirectory { get; }

    public string LogsDirectory { get; }

    public string ConfigPath { get; }

    public bool ConfigExists => File.Exists(ConfigPath);

    public AppConfig Current { get; private set; }

    public AppConfig GetSnapshot()
    {
        lock (_sync)
        {
            return Current.Clone();
        }
    }

    public void Save(AppConfig config)
    {
        lock (_sync)
        {
            CloudRelayDefaults.Apply(config);
            Current = config.Clone();
            var json = JsonSerializer.Serialize(Current, _serializerOptions);
            File.WriteAllText(ConfigPath, json, Encoding.UTF8);
        }
    }

    public string? GetBotToken(AppConfig config)
    {
        return GetProtectedValue(config.ProtectedBotToken);
    }

    public void SetBotToken(AppConfig config, string? botToken)
    {
        config.ProtectedBotToken = SetProtectedValue(botToken);
    }

    public string? GetCloudAgentSecret(AppConfig config)
    {
        return GetProtectedValue(config.ProtectedCloudAgentSecret);
    }

    public void SetCloudAgentSecret(AppConfig config, string? secret)
    {
        config.ProtectedCloudAgentSecret = SetProtectedValue(secret);
    }

    public string? GetAgentSecret(AppConfig config)
    {
        return GetProtectedValue(config.ProtectedAgentSecret);
    }

    public void SetAgentSecret(AppConfig config, string? secret)
    {
        config.ProtectedAgentSecret = SetProtectedValue(secret);
    }

    /// <summary>
    /// Ensures the agent has a unique ID and secret. Generates them on first run.
    /// </summary>
    public bool EnsureAgentCredentials(AppConfig config)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(config.AgentId))
        {
            config.AgentId = Guid.NewGuid().ToString("D");
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(GetAgentSecret(config)))
        {
            var secret = GenerateSecureSecret(64);
            SetAgentSecret(config, secret);
            changed = true;
        }

        return changed;
    }

    private static string GenerateSecureSecret(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = new byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }

    private static string? GetProtectedValue(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedValue);
            var rawBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(rawBytes);
        }
        catch
        {
            return null;
        }
    }

    private static string? SetProtectedValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var rawBytes = Encoding.UTF8.GetBytes(rawValue.Trim());
        var protectedBytes = ProtectedData.Protect(rawBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private AppConfig LoadFromDisk()
    {
        if (!File.Exists(ConfigPath))
        {
            var freshConfig = new AppConfig();
            CloudRelayDefaults.Apply(freshConfig);
            return freshConfig;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
            var config = JsonSerializer.Deserialize<AppConfig>(json, _serializerOptions) ?? new AppConfig();
            CloudRelayDefaults.Apply(config);
            return config;
        }
        catch
        {
            var fallbackConfig = new AppConfig();
            CloudRelayDefaults.Apply(fallbackConfig);
            return fallbackConfig;
        }
    }
}
