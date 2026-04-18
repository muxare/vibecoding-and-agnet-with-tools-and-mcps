using System.Text.RegularExpressions;

namespace TeamFlow.Agents;

internal static class PromptLoader
{
    public static string Load(string promptsDirectory, string agent, string version)
        => LoadRendered(promptsDirectory, agent, version, vars: null);

    /// <summary>
    /// Loads a prompt by agent + version, preferring <c>.prompty</c> (frontmatter-prefixed)
    /// and falling back to the legacy <c>.txt</c> format. Frontmatter is stripped before
    /// returning. <c>{{var}}</c> placeholders in the body are substituted from <paramref name="vars"/>;
    /// unknown placeholders are left intact so missing variables fail loudly at model time.
    /// </summary>
    public static string LoadRendered(
        string promptsDirectory,
        string agent,
        string version,
        IReadOnlyDictionary<string, string>? vars)
    {
        foreach (var ext in new[] { ".prompty", ".txt" })
        {
            var fileName = $"{agent}.{version}{ext}";
            foreach (var candidate in CandidatePaths(promptsDirectory, fileName))
            {
                if (!File.Exists(candidate)) continue;
                var raw = File.ReadAllText(candidate);
                var body = ext == ".prompty" ? StripFrontmatter(raw) : raw;
                return vars is null or { Count: 0 } ? body : Render(body, vars);
            }
        }
        throw new FileNotFoundException(
            $"Prompt '{agent}.{version}' not found (tried .prompty and .txt). " +
            "Searched relative to base dir and repo root.",
            $"{agent}.{version}");
    }

    private static readonly Regex FrontmatterRegex = new(
        @"\A---\s*\r?\n(?<fm>.*?)\r?\n---\s*\r?\n",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex VariableRegex = new(
        @"\{\{\s*(?<name>\w+)\s*\}\}",
        RegexOptions.Compiled);

    private static string StripFrontmatter(string text)
    {
        var m = FrontmatterRegex.Match(text);
        return m.Success ? text[m.Length..] : text;
    }

    private static string Render(string body, IReadOnlyDictionary<string, string> vars)
        => VariableRegex.Replace(body, m =>
            vars.TryGetValue(m.Groups["name"].Value, out var value) ? value : m.Value);

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
