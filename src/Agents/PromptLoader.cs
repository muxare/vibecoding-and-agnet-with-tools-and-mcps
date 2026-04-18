namespace TeamFlow.Agents;

internal static class PromptLoader
{
    public static string Load(string promptsDirectory, string agent, string version)
    {
        var fileName = $"{agent}.{version}.txt";
        foreach (var candidate in CandidatePaths(promptsDirectory, fileName))
        {
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException(
            $"Prompt file '{fileName}' not found. Searched relative to base dir and repo root.",
            fileName);
    }

    private static IEnumerable<string> CandidatePaths(string promptsDirectory, string fileName)
    {
        yield return Path.Combine(AppContext.BaseDirectory, promptsDirectory, fileName);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, promptsDirectory, fileName);
            dir = dir.Parent;
        }
    }
}
