using System.Security;

namespace TgPcAgent.App.Services;

internal sealed class SafeFileSystemEnumerator
{
    private readonly Func<string, IEnumerable<string>> _directoryProvider;
    private readonly Func<string, IEnumerable<string>> _fileProvider;
    private readonly Action<string>? _onSkippedDirectory;

    public SafeFileSystemEnumerator(
        Func<string, IEnumerable<string>> directoryProvider,
        Func<string, IEnumerable<string>> fileProvider,
        Action<string>? onSkippedDirectory = null)
    {
        _directoryProvider = directoryProvider;
        _fileProvider = fileProvider;
        _onSkippedDirectory = onSkippedDirectory;
    }

    public IEnumerable<string> EnumerateFilesRecursive(IEnumerable<string> roots)
    {
        var pendingDirectories = new Stack<string>(
            roots.Where(path => !string.IsNullOrWhiteSpace(path)).Reverse());

        while (pendingDirectories.Count > 0)
        {
            var directoryPath = pendingDirectories.Pop();

            IEnumerable<string> files;
            try
            {
                files = _fileProvider(directoryPath);
            }
            catch (Exception exception) when (IsSkippable(exception))
            {
                Skip(directoryPath);
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            IEnumerable<string> childDirectories;
            try
            {
                childDirectories = _directoryProvider(directoryPath);
            }
            catch (Exception exception) when (IsSkippable(exception))
            {
                Skip(directoryPath);
                continue;
            }

            foreach (var childDirectory in childDirectories.Reverse())
            {
                pendingDirectories.Push(childDirectory);
            }
        }
    }

    private void Skip(string directoryPath)
    {
        _onSkippedDirectory?.Invoke(directoryPath);
    }

    private static bool IsSkippable(Exception exception)
    {
        return exception is UnauthorizedAccessException
            || exception is IOException
            || exception is SecurityException;
    }
}
