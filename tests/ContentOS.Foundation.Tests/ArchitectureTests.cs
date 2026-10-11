using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ContentOS.Foundation.Tests;

public sealed class ArchitectureTests
{
    internal static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GeetaStudio.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["ContentOS.Domain"] = [],
        ["ContentOS.Contracts"] = [],
        ["ContentOS.Application"] = ["ContentOS.Domain", "ContentOS.Contracts"],
        ["ContentOS.Infrastructure"] = ["ContentOS.Application", "ContentOS.Domain"],
        ["ContentOS.Integrations"] = ["ContentOS.Application", "ContentOS.Contracts"],
        ["GeetaStudio.Api"] = ["ContentOS.Application", "ContentOS.Contracts", "ContentOS.Infrastructure", "ContentOS.Integrations"],
        ["GeetaStudio.Web"] = ["ContentOS.Contracts"],
        ["GeetaStudio.Worker"] = ["ContentOS.Application", "ContentOS.Infrastructure", "ContentOS.Integrations"]
    };

    internal static IEnumerable<string> CheckProject(string name, string xml)
    {
        var document = XDocument.Parse(xml);
        foreach (var reference in document.Descendants("ProjectReference"))
        {
            var dependency = Path.GetFileNameWithoutExtension(((string?)reference.Attribute("Include") ?? "").Replace('\\', '/'));
            if (!Allowed[name].Contains(dependency)) yield return $"{name}: forbidden project reference {dependency}";
        }
        if (name is "ContentOS.Domain" or "ContentOS.Application" or "ContentOS.Contracts")
        {
            foreach (var reference in document.Descendants().Where(e => e.Name.LocalName is "PackageReference" or "Reference" or "FrameworkReference"))
                yield return $"{name}: forbidden dependency {reference.Attribute("Include")}";
            if ((string?)document.Root?.Attribute("Sdk") != "Microsoft.NET.Sdk") yield return $"{name}: forbidden SDK";
        }
    }

    internal static bool HasForbiddenEndpoint(string source) =>
        Regex.IsMatch(source, @"https?\s*:\s*//|\bHttpClient\b|\bSystem\.Net\b", RegexOptions.IgnoreCase);

    [Fact]
    public void Repository_obeys_dependency_and_endpoint_boundaries()
    {
        var violations = new List<string>();
        foreach (var name in Allowed.Keys)
        {
            var folder = Path.Combine(RepositoryRoot, "src", name);
            violations.AddRange(CheckProject(name, File.ReadAllText(Path.Combine(folder, name + ".csproj"))));
            if (name is "ContentOS.Domain" or "ContentOS.Application" or "ContentOS.Contracts")
            {
                foreach (var source in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
                    .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin")))
                    if (HasForbiddenEndpoint(File.ReadAllText(source))) violations.Add($"{source}: forbidden endpoint/network dependency");
            }
        }
        // Inherited build files cannot smuggle dependencies into core projects.
        foreach (var path in Directory.EnumerateFiles(RepositoryRoot, "Directory.Build.*", SearchOption.AllDirectories)
            .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin")))
        {
            var doc = XDocument.Load(path);
            Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName is "PackageReference" or "Reference" or "FrameworkReference" or "ProjectReference");
        }
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData("PackageReference", "Vendor.TextSdk")]
    [InlineData("Reference", "Vendor.TextSdk")]
    [InlineData("FrameworkReference", "Microsoft.AspNetCore.App")]
    [InlineData("ProjectReference", "../ContentOS.Integrations/ContentOS.Integrations.csproj")]
    public void Gate_rejects_deliberate_core_dependencies(string kind, string dependency)
    {
        var xml = $"<Project Sdk='Microsoft.NET.Sdk'><ItemGroup><{kind} Include='{dependency}' /></ItemGroup></Project>";
        Assert.NotEmpty(CheckProject("ContentOS.Domain", xml));
        Assert.NotEmpty(CheckProject("ContentOS.Application", xml));
    }

    [Theory]
    [InlineData("const string endpoint = \"https://vendor.example/v1\";")]
    [InlineData("new System.Net.Http.HttpClient();")]
    public void Gate_rejects_core_endpoints_and_network_clients(string source) => Assert.True(HasForbiddenEndpoint(source));
}
