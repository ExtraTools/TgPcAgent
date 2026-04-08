using TgPcAgent.App.Services;

namespace TgPcAgent.App.Tests.Services;

public sealed class SafeFileSystemEnumeratorTests
{
    [Fact]
    public void EnumerateFilesRecursive_SkipsUnauthorizedDirectories_AndContinues()
    {
        var deniedPaths = new List<string>();
        var directories = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["root"] = ["good", "blocked"],
            ["good"] = []
        };

        var files = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["root"] = [],
            ["good"] = ["good\\app.lnk"]
        };

        var enumerator = new SafeFileSystemEnumerator(
            directoryPath =>
            {
                if (directoryPath.Equals("blocked", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException();
                }

                return directories.TryGetValue(directoryPath, out var children)
                    ? children
                    : [];
            },
            directoryPath => files.TryGetValue(directoryPath, out var items) ? items : [],
            deniedPaths.Add);

        var result = enumerator.EnumerateFilesRecursive(["root"]).ToList();

        Assert.Single(result);
        Assert.Equal("good\\app.lnk", result[0]);
        Assert.Single(deniedPaths);
        Assert.Equal("blocked", deniedPaths[0]);
    }

    [Fact]
    public void EnumerateFilesRecursive_SkipsDirectoriesThatThrowWhileReadingFiles()
    {
        var deniedPaths = new List<string>();
        var enumerator = new SafeFileSystemEnumerator(
            directoryPath => directoryPath == "root" ? ["blocked", "good"] : [],
            directoryPath =>
            {
                if (directoryPath == "blocked")
                {
                    throw new IOException("denied");
                }

                return directoryPath == "good" ? ["good\\tool.lnk"] : [];
            },
            deniedPaths.Add);

        var result = enumerator.EnumerateFilesRecursive(["root"]).ToList();

        Assert.Single(result);
        Assert.Equal("good\\tool.lnk", result[0]);
        Assert.Single(deniedPaths);
        Assert.Equal("blocked", deniedPaths[0]);
    }
}
