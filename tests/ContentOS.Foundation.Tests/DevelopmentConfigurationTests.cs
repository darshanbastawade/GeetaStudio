using System.Text.Json;

namespace ContentOS.Foundation.Tests;

public sealed class DevelopmentConfigurationTests
{
    [Fact]
    public void Debug_and_run_paths_share_development_profiles_and_loopback_ports()
    {
        var root = ArchitectureTests.RepositoryRoot;
        using var launch = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".vscode", "launch.json")));
        using var tasks = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".vscode", "tasks.json")));
        Assert.Contains(tasks.RootElement.GetProperty("tasks").EnumerateArray(), t => t.GetProperty("label").GetString() == "Geeta Studio: build");
        foreach (var host in new[] { "Api", "Web", "Worker" })
        {
            var project = "GeetaStudio." + host;
            var hostFolder = Path.Combine(root, "src", project);
            using var profile = JsonDocument.Parse(File.ReadAllText(Path.Combine(hostFolder, "Properties", "launchSettings.json")));
            var development = profile.RootElement.GetProperty("profiles").GetProperty("Development");
            var debug = launch.RootElement.GetProperty("configurations").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Geeta Studio: " + host);
            Assert.Equal("Development", development.GetProperty("environmentVariables").GetProperty("DOTNET_ENVIRONMENT").GetString());
            Assert.Equal("Development", debug.GetProperty("env").GetProperty("DOTNET_ENVIRONMENT").GetString());
            Assert.Equal("Development", debug.GetProperty("env").GetProperty("ASPNETCORE_ENVIRONMENT").GetString());
            var dllPath = debug.GetProperty("program").GetString()!.Replace("${workspaceFolder}", root);
            Assert.True(File.Exists(dllPath), "Missing built debug target: " + dllPath);
            Assert.Equal(hostFolder.Replace('\\', '/'), debug.GetProperty("cwd").GetString()!.Replace("${workspaceFolder}", root).Replace('\\', '/'));
            Assert.Equal("Geeta Studio: build", debug.GetProperty("preLaunchTask").GetString());
            if (host != "Worker")
            {
                var url = host == "Api" ? "http://localhost:5100" : "http://localhost:5200";
                Assert.Equal(url, development.GetProperty("applicationUrl").GetString());
                Assert.Equal(url, debug.GetProperty("env").GetProperty("ASPNETCORE_URLS").GetString());
                Assert.True(new Uri(url).IsLoopback);
            }
            else Assert.False(development.TryGetProperty("applicationUrl", out _));
            var runTask = tasks.RootElement.GetProperty("tasks").EnumerateArray().Single(t => t.GetProperty("label").GetString() == "Geeta Studio: run " + host);
            Assert.Equal("Geeta Studio: build", runTask.GetProperty("dependsOn").GetString());
            Assert.Contains(runTask.GetProperty("args").EnumerateArray(), a => a.GetString() == "Development");
        }
        var compound = launch.RootElement.GetProperty("compounds").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Geeta Studio: all");
        Assert.Equal("Geeta Studio: build", compound.GetProperty("preLaunchTask").GetString());
        foreach (var name in compound.GetProperty("configurations").EnumerateArray())
        {
            var target = launch.RootElement.GetProperty("configurations").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name.GetString());
            Assert.False(target.TryGetProperty("preLaunchTask", out _));
            Assert.True(target.GetProperty("presentation").GetProperty("hidden").GetBoolean());
            var individual = launch.RootElement.GetProperty("configurations").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name.GetString()!.Replace(" (compound)", ""));
            Assert.Equal(individual.GetProperty("program").GetString(), target.GetProperty("program").GetString());
            Assert.Equal(individual.GetProperty("cwd").GetString(), target.GetProperty("cwd").GetString());
            Assert.Equal(individual.GetProperty("env").GetRawText(), target.GetProperty("env").GetRawText());
        }
        Assert.True(compound.GetProperty("stopAll").GetBoolean());
        Assert.Equal(3, compound.GetProperty("configurations").GetArrayLength());
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src", "GeetaStudio.Web", "appsettings.Development.json")));
        Assert.Equal("http://localhost:5100/", config.RootElement.GetProperty("Api").GetProperty("BaseAddress").GetString());
    }
}
