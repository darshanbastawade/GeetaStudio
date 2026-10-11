using System.Text.RegularExpressions;

namespace ContentOS.Foundation.Tests;

public sealed class SecretExclusionTests
{
    [Fact]
    public void Source_has_no_known_secret_values_or_private_keys()
    {
        var root = ArchitectureTests.RepositoryRoot;
        var extensions = new[] { ".cs", ".razor", ".json", ".ps1", ".yml", ".yaml", ".config", ".props", ".csproj" };
        var findings = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            if (relative.Split(Path.DirectorySeparatorChar).Any(p => p is ".git" or "bin" or "obj" or ".local" or "artifacts" or "TestResults")) continue;
            if (!extensions.Contains(Path.GetExtension(file))) continue;
            var source = File.ReadAllText(file);
            if (Regex.IsMatch(source, @"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|\bsk-[A-Za-z0-9_-]{20,}|\bAKIA[A-Z0-9]{16}\b")) findings.Add(relative);
            if (Path.GetFileName(file) != "SecretExclusionTests.cs"
                && Regex.IsMatch(source, "(?i)[\"']?(?:ApiKey|Password|ClientSecret|AccessToken)[\"']?\\s*[:=]\\s*[\"'][^\"'\\r\\n]+[\"']")) findings.Add(relative);
        }
        Assert.True(findings.Count == 0, "Potential secrets in: " + string.Join(", ", findings));
    }
}
