using System.Security.Principal;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Foundation.Tests;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The demo needs Windows authentication and speech.");
var database = Environment.GetEnvironmentVariable("GEETA_M26_DATABASE") ?? "GeetaStudio_M1Tests";
using var root = HistoricalTestFixture.Services(database); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
if (s.GetRequiredService<CoreDbContext>().Database.GetDbConnection().Database != database || s.GetRequiredService<RuntimeDbContext>().Database.GetDbConnection().Database != database) throw new InvalidOperationException("Historical demo database bindings differ; no import performed.");
using var identity = WindowsIdentity.GetCurrent();
var directory = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "m26-demo", database); Directory.CreateDirectory(directory);
var saved = Path.Combine(directory, "fixture.json");
if (File.Exists(saved))
{
    using var previous = JsonDocument.Parse(await File.ReadAllTextAsync(saved)); var data = previous.RootElement;
    if (data.GetProperty("database").GetString() != database || data.GetProperty("universe").GetGuid() != CoreSeed.StableId("universe:shivaji-maharaj")) throw new InvalidOperationException("Previous demo binding differs; no import performed.");
    var read = s.GetRequiredService<IReadWorkbench>(); var owner = new CoreScope(data.GetProperty("tenant").GetGuid(), identity.Name);
    var old = await read.InspectAsync(owner, data.GetProperty("universe").GetGuid(), data.GetProperty("collection").GetGuid(), default);
    if (old.Book.Collection.State != "Disabled") await read.ControlAsync(owner, data.GetProperty("universe").GetGuid(), old.Book.Collection.Id, new(Guid.NewGuid(), old.RowVersion, "Disable"), default);
}
var f = await HistoricalTestFixture.Setup(s, identity.Name);
var book = await s.GetRequiredService<IReadWorkbench>().PreviewAsync(f.Owner.Actor, f.Collection, default);
if (book?.Pieces.Length != 10) throw new InvalidOperationException("All ten historical READ samples must be available.");
object? pipeline = null; object[] watch = []; Guid? geetaCollection = null; string? geetaKey = null;
if (database == "GeetaStudio_M1Tests")
{
    Console.WriteLine("Ten private READ items accepted. Checking retrieval/workflow/factory/export reuse.");
    pipeline = await HistoricalTestFixture.Pipeline(s, f);
    Console.WriteLine("Rendering fact sample with installed Windows speech.");
    var one = await HistoricalTestFixture.Watch(s, f, true, Path.Combine(directory, "coronation"));
    Console.WriteLine("Rendering explicitly invented dialogue sample.");
    var two = await HistoricalTestFixture.Watch(s, f, false, Path.Combine(directory, "dialogue"));
    watch = new object[] { new { one.Id, one.Artifacts, label = "HistoricalClaim" }, new { two.Id, two.Artifacts, label = "FictionalScene" } };
    // A new isolated M14 source-reading fixture exercises the original scripture path without editing Geeta's stored corpus.
    var geeta = await ReadTestFixture.Setup(s, identity.Name); geetaCollection = geeta.Collection; geetaKey = geeta.Key;
    var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var row = await db.Universes.SingleAsync(x => x.Id == geeta.Universe);
    row.Name = "Geeta scripture fixture — private technical sample"; await db.SaveChangesAsync();
}
await File.WriteAllTextAsync(Path.Combine(directory, "fixture.json"), JsonSerializer.Serialize(new { database, tenant = f.Owner.TenantId, universe = f.Universe, collection = f.Collection, key = f.Key, audience = f.Audience, manifest = f.Manifest, geetaCollection, geetaKey, readCount = book.Pieces.Length, pipeline, watch, historicalHumanReview = "Pending", publicRelease = false, liveCalls = 0 }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
await File.WriteAllTextAsync(Path.Combine(directory, "read.json"), JsonSerializer.Serialize(book, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
Console.WriteLine("Historical fixture saved to " + directory + "; human release review pending.");
namespace ContentOS.Foundation.Tests { internal static class ArchitectureTests { public static string RepositoryRoot => Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!; } }
