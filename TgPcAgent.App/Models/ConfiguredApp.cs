namespace TgPcAgent.App.Models;

public sealed class ConfiguredApp
{
    public string Alias { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public string? Arguments { get; set; }

    public bool UseShellExecute { get; set; } = true;

    public ConfiguredApp Clone()
    {
        return new ConfiguredApp
        {
            Alias = Alias,
            TargetPath = TargetPath,
            Arguments = Arguments,
            UseShellExecute = UseShellExecute
        };
    }
}
