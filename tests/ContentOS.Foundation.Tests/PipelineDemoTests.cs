using System.Diagnostics;
namespace ContentOS.Foundation.Tests;

public sealed class PipelineDemoTests
{
    [Theory]
    [InlineData(false, "1", "USD")]
    [InlineData(true, "0", "USD")]
    [InlineData(true, "1001", "USD")]
    [InlineData(true, "1", "INVALID")]
    public async Task Live_demo_refuses_missing_opt_in_or_unbounded_spend_before_reading_configuration(bool enabled, string limit, string currency)
    {
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "powershell" : "pwsh") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(ArchitectureTests.RepositoryRoot, "scripts", "pipeline-demo.ps1"), "-Mode", "Live", "-MaxGenerationCost", limit, "-Currency", currency, "-Configuration", "this-file-must-never-be-read.json" }) start.ArgumentList.Add(arg);
        if (enabled) start.ArgumentList.Add("-EnableLive");
        using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); var message = await output + await error;
        Assert.NotEqual(0, process.ExitCode); Assert.DoesNotContain("Cannot find path", message); Assert.Contains(currency == "INVALID" ? "supported budget currency" : "Live mode requires", message);
    }
}
