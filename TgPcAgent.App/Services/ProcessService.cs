using System.Diagnostics;
using TgPcAgent.App.Models;

namespace TgPcAgent.App.Services;

public sealed class ProcessService
{
    public IReadOnlyList<ProcessSnapshot> GetTopProcessesByMemory(int count)
    {
        return Process.GetProcesses()
            .Select(process =>
            {
                try
                {
                    return new ProcessSnapshot(
                        process.Id,
                        process.ProcessName,
                        Math.Round(process.WorkingSet64 / 1024d / 1024d, 1));
                }
                catch
                {
                    return null;
                }
            })
            .Where(snapshot => snapshot is not null)
            .Cast<ProcessSnapshot>()
            .OrderByDescending(snapshot => snapshot.MemoryMb)
            .Take(count)
            .ToList();
    }
}
