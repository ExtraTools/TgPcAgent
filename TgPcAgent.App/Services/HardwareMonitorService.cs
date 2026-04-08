using LibreHardwareMonitor.Hardware;
using TgPcAgent.App.Models;

namespace TgPcAgent.App.Services;

public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private readonly object _sync = new();

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true
        };
        _computer.Open();
    }

    public HardwareReadings Read()
    {
        lock (_sync)
        {
            double? cpuLoad = null;
            double? cpuTemperature = null;
            double? gpuTemperature = null;

            foreach (var hardware in EnumerateHardware(_computer.Hardware))
            {
                hardware.Update();

                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.Value is null)
                    {
                        continue;
                    }

                    if (sensor.SensorType == SensorType.Load &&
                        sensor.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuLoad = sensor.Value.Value;
                    }

                    if (sensor.SensorType != SensorType.Temperature)
                    {
                        continue;
                    }

                    if (hardware.HardwareType == HardwareType.Cpu &&
                        (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                         sensor.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase)))
                    {
                        cpuTemperature = Max(cpuTemperature, sensor.Value.Value);
                    }

                    if (hardware.HardwareType is HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia)
                    {
                        // Prefer "GPU Core" sensor for accurate temp (not hotspot/memory)
                        if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        {
                            gpuTemperature = sensor.Value.Value;
                        }
                        else if (!gpuTemperature.HasValue)
                        {
                            gpuTemperature = sensor.Value.Value;
                        }
                    }
                }
            }

            return new HardwareReadings(cpuLoad, cpuTemperature, gpuTemperature);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _computer.Close();
        }
    }

    private static IEnumerable<IHardware> EnumerateHardware(IEnumerable<IHardware> hardwareNodes)
    {
        foreach (var hardware in hardwareNodes)
        {
            yield return hardware;

            foreach (var subHardware in EnumerateHardware(hardware.SubHardware))
            {
                yield return subHardware;
            }
        }
    }

    private static double? Max(double? current, float next)
    {
        return current.HasValue ? Math.Max(current.Value, next) : next;
    }
}
