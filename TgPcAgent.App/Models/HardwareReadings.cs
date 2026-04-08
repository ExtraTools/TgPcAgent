namespace TgPcAgent.App.Models;

public sealed record HardwareReadings(
    double? CpuLoadPercent,
    double? CpuTemperatureCelsius,
    double? GpuTemperatureCelsius);
