using System.Security.Claims;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using GeetaStudio.Api;
using Microsoft.Extensions.Configuration;

namespace ContentOS.Foundation.Tests;

public sealed class RuntimePolicyTests
{
    [Fact]
    public async Task Staff_roles_require_explicit_mapping_even_with_claimed_admin_role()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Staff:Reader:0"] = "local-reader" }).Build();
        var claimedAdmin = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Name, "local-reader"), new(ClaimTypes.Role, "Administrator")], "Negotiate"));
        var result = await new StaffAccess(config).TransformAsync(claimedAdmin);
        Assert.True(result.IsInRole("Reader")); Assert.False(result.IsInRole("Administrator"));
        var unknown = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Name, "unconfigured")], "Negotiate"));
        Assert.False((await new StaffAccess(config).TransformAsync(unknown)).IsInRole("Reader"));
    }
    [Fact]
    public async Task Secret_resolution_is_restricted_redacted_and_cleared_on_disposal()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ProviderSecrets:fixture"] = "deterministic-test-value" }).Build();
        var resolver = new ConfigurationSecretResolver(config);
        Assert.Null(await resolver.ResolveAsync("config:ConnectionStrings:ContentOS"));
        Assert.Null(await resolver.ResolveAsync("env:PATH"));
        using var secret = await resolver.ResolveAsync("config:ProviderSecrets:fixture"); Assert.NotNull(secret);
        Assert.Equal("[redacted]", secret.ToString()); var memory = secret.Value; secret.Dispose();
        Assert.All(memory.ToArray(), c => Assert.Equal('\0', c));
    }
    [Theory]
    [InlineData("external.adapter", "local:test", "runtime.probe", 1, 1, 1, 60, "Asia/Kolkata")]
    [InlineData("test.a", "local:test", "text.generate", 1, 1, 1, 60, "Asia/Kolkata")]
    [InlineData("test.a", "local:test", "runtime.probe", 0, 1, 1, 60, "Asia/Kolkata")]
    [InlineData("test.a", "local:test", "runtime.probe", 1, 5, 1, 60, "Asia/Kolkata")]
    [InlineData("test.a", "local:test", "runtime.probe", 1, 1, 1, 60, "Invalid/Timezone")]
    public void Unsafe_runtime_profiles_are_rejected(string adapter, string endpoint, string capability, int concurrency,
        int attempts, decimal budget, int deadline, string timezone) =>
        Assert.Throws<RuntimeValidationException>(() => RuntimeRules.Validate(adapter, endpoint, capability, "en-IN", concurrency, attempts, budget, deadline, timezone));
}
