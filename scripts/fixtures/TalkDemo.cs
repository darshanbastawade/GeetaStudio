using System.Security.Principal;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Foundation.Tests;
using Microsoft.Extensions.DependencyInjection;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;

ArchitectureTests.RepositoryRoot = Path.GetFullPath(Path.Combine(args[0], "..", ".."));
if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This controlled demo requires Windows authentication.");
using var root = TalkTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
using var windowsIdentity = WindowsIdentity.GetCurrent(); var actor = windowsIdentity.Name; var f = await TalkTestFixture.Setup(s, actor: actor);
var db = s.GetRequiredService<CoreDbContext>(); if (!await db.Members.AnyAsync(x => x.TenantId == f.Customer.Tenant && x.Actor == actor)) { db.Members.Add(new() { TenantId = f.Customer.Tenant, Actor = actor, Permission = "Administrator" }); await db.SaveChangesAsync(); }
var w = s.GetRequiredService<ITalkWorkbench>(); var id = await w.OpenAsync(f.Customer.Alice, new(Guid.NewGuid(), f.Policy, f.Customer.Profile), default);
// Another registered account owns a separate session; Windows caller must receive403.
var other = CustomerTestFixture.Scope(f.Customer.Tenant, "fixture-other:" + f.Customer.Tenant); var customers = s.GetRequiredService<ICustomerWorkbench>(); await customers.RegisterAsync(other, new(Guid.NewGuid(), "Other fixture adult", true), default); var household = await customers.HouseholdAsync(other, new(Guid.NewGuid(), "Other fixture household"), default); var profile = await customers.ProfileAsync(other, new(Guid.NewGuid(), household, "Other fixture reader", "Adult", "en-IN"), default); var otherSession = await w.OpenAsync(other, new(Guid.NewGuid(), f.Policy, profile), default);
await File.WriteAllTextAsync(Path.Combine(args[0], "fixture.json"), JsonSerializer.Serialize(new { tenant = f.Customer.Tenant, universe = f.Customer.Universe, policy = f.Policy, profile = f.Customer.Profile, session = id, otherSession, routeA = f.RouteA, routeB = f.RouteB }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

namespace ContentOS.Foundation.Tests
{
    internal static class ArchitectureTests { internal static string RepositoryRoot = ""; }
    internal static class SourceTestFixture { internal static readonly byte[] Bytes = "Unused original fixture text"u8.ToArray(); }
    internal static class SqlAiTests
    {
        internal static AiProviderSettings Settings(string adapter = "fixture.text.a", string scenario = "success", bool streaming = true) => new(adapter, adapter == "fixture.text.b" ? "fixture-b.v1" : "fixture-a.v1", "ai.text", "en-IN", true, streaming, false, 32768, 128, new("fixture-rates.v1", "USD", 1m, .25m, 8m), new(["Public", "Internal"], "local", 0, false, "owned-policy.v1"), "OfflineVerified", "fixture:M21-private", scenario);
    }
}
