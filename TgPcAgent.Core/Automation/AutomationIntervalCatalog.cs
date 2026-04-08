namespace TgPcAgent.Core.Automation;

public static class AutomationIntervalCatalog
{
    private static readonly int[] Presets = [0, 1, 5, 15, 30, 60];

    public static IReadOnlyList<int> SupportedMinutes => Presets;

    public static bool IsSupported(int minutes)
    {
        return Presets.Contains(minutes);
    }

    public static string FormatLabel(int minutes)
    {
        return minutes switch
        {
            0 => "Выкл",
            1 => "1 мин",
            60 => "1 ч",
            _ => $"{minutes} мин"
        };
    }
}
