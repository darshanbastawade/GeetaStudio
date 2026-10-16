using System.Security.Cryptography;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Infrastructure;
using GeetaStudio.Api;
using Microsoft.Extensions.Configuration;
namespace ContentOS.Foundation.Tests;

public sealed class ReleaseGateTests
{
    private sealed class Modules : IReleaseModuleEvidence
    {
        public ReleaseObservation Current { get; set; } = new("content.v1", "provider.v1", [], ["source:real-bound-source"], [Guid.NewGuid()]);
        public Task<ReleaseObservation> ObserveAsync(string feature, ReleaseTarget target, string actor, CancellationToken ct) => Task.FromResult(Current);
    }
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "geeta-release-test-" + Guid.NewGuid());
        public Modules Modules { get; } = new();
        public ReleaseTarget Target { get; } = new("selected-read", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        public IConfiguration Configuration { get; } = new ConfigurationBuilder().AddInMemoryCollection().Build();
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, "ops"));
            Save("ops/release-baseline.json", new ReleaseManifest(2, "SYNTHETIC TEST ONLY", [new("READ", true, [Target]), new("Payments", false, []), new("TALK", false, [])]));
            Save("ops/release-evidence.json", Array.Empty<ReleaseReceipt>());
        }
        public void Save<T>(string path, T value) => File.WriteAllText(Path.Combine(Root, path), JsonSerializer.Serialize(value));
        public ReleaseGate Gate(string code = "synthetic-code.v1") => new(Configuration, Modules, Root, code, "Test");
        public async Task<ReleaseReceipt[]> Qualify()
        {
            var decision = await Gate().EvaluateAsync("test-reviewer", default); var subject = decision.Features[0].Targets[0].SubjectFingerprint; var now = DateTimeOffset.UtcNow;
            var receipts = ReleaseRules.OperationalGates.Select(kind =>
            {
                var path = "ops/" + kind + ".json"; var mode = ReleaseRules.RequiredMode(kind);
                Save(path, new ReleaseProof(kind, mode, subject, "Passed", now, false, ["manual:synthetic-unit-test-not-real-approval"], ReleaseRules.Metrics(kind).ToDictionary(x => x.Name, x => x.Minimum), mode == "Human" ? "human:synthetic-unit-test-reviewer" : null));
                return new ReleaseReceipt(kind, "READ", Target.Key, kind, mode, subject, now.AddMinutes(-1), now.AddDays(1), "manual:synthetic-unit-test-not-real-approval", path, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root, path)))));
            }).ToArray();
            Save("ops/release-evidence.json", receipts); return receipts;
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
    [Fact]
    public async Task Missing_evidence_blocks_selected_read_without_requiring_disabled_payments_or_talk()
    {
        using var f = new Fixture(); var d = await f.Gate().EvaluateAsync("reviewer", default);
        Assert.False(d.ProductionReady); Assert.Contains("Hosting:MissingOrAmbiguousEvidence", d.Features[0].Targets[0].Blocks);
        await f.Qualify(); d = await f.Gate().EvaluateAsync("reviewer", default);
        Assert.True(d.ProductionReady); Assert.False(d.Features.Single(x => x.Key == "Payments").Ready);
    }
    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("superseded")]
    [InlineData("fixture")]
    [InlineData("file")]
    [InlineData("code")]
    [InlineData("policy")]
    [InlineData("provider")]
    [InlineData("content")]
    [InlineData("configuration")]
    [InlineData("manualFlag")]
    [InlineData("recoveryOverrun")]
    [InlineData("incompleteCI")]
    [InlineData("missingReviewer")]
    public async Task Material_changes_and_invalid_evidence_invalidate_prior_decision(string change)
    {
        using var f = new Fixture(); var receipts = await f.Qualify(); Assert.True((await f.Gate().EvaluateAsync("reviewer", default)).ProductionReady);
        if (change == "expired") receipts[0] = receipts[0] with { ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1) };
        if (change == "revoked") receipts[0] = receipts[0] with { Revoked = true };
        if (change == "superseded") receipts[0] = receipts[0] with { SupersededBy = "new-review" };
        if (change == "policy") receipts[0] = receipts[0] with { SubjectFingerprint = "old-policy-binding" };
        if (change == "fixture")
        {
            f.Save(receipts[0].Path, new ReleaseProof(receipts[0].Kind, receipts[0].Mode, receipts[0].SubjectFingerprint, "Passed", DateTimeOffset.UtcNow, true, ["fixture:not-human"]));
            receipts[0] = receipts[0] with { Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(f.Root, receipts[0].Path)))) };
        }
        if (change is "manualFlag" or "recoveryOverrun" or "incompleteCI" or "missingReviewer")
        {
            var index = change is "manualFlag" or "recoveryOverrun" ? Array.FindIndex(receipts, x => x.Kind == "Recovery") : change == "incompleteCI" ? Array.FindIndex(receipts, x => x.Kind == "MatchingCI") : 0;
            var path = Path.Combine(f.Root, receipts[index].Path); var proof = JsonSerializer.Deserialize<ReleaseProof>(File.ReadAllText(path))!;
            if (change == "manualFlag") proof = proof with { Outcome = "Passed", Measurements = null, ReviewerReference = null };
            if (change == "recoveryOverrun") proof.Measurements!["RtoSeconds"] = 301;
            if (change == "incompleteCI") proof.Measurements!["SqlExpected"] = proof.Measurements["SqlPassed"] + 1;
            if (change == "missingReviewer") proof = proof with { ReviewerReference = null };
            f.Save(receipts[index].Path, proof); receipts[index] = receipts[index] with { Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
        }
        if (change == "file") File.AppendAllText(Path.Combine(f.Root, receipts[0].Path), " ");
        if (change == "provider") f.Modules.Current = f.Modules.Current with { ProviderFingerprint = "provider.v2" };
        if (change == "content") f.Modules.Current = f.Modules.Current with { ContentFingerprint = "corrected-content.v2" };
        if (change == "configuration") f.Configuration["Read:PublicEnabled"] = "false";
        f.Save("ops/release-evidence.json", receipts);
        var d = await f.Gate(change == "code" ? "new-code" : "synthetic-code.v1").EvaluateAsync("reviewer", default);
        Assert.False(d.ProductionReady); Assert.NotEmpty(d.Features[0].Targets[0].Blocks);
    }
    [Fact]
    public async Task Withdrawn_source_or_new_m12_block_cannot_be_overridden_by_operational_receipts()
    {
        using var f = new Fixture(); await f.Qualify(); f.Modules.Current = f.Modules.Current with { Blocks = ["SourceWithdrawn", "M12:HumanReleaseAcceptanceRequired"] };
        var d = await f.Gate().EvaluateAsync("reviewer", default);
        Assert.False(d.ProductionReady); Assert.Contains("SourceWithdrawn", d.Features[0].Targets[0].Blocks);
    }
    [Fact]
    public async Task Missing_manifest_or_unsupported_enabled_feature_fails_closed()
    {
        using var f = new Fixture(); f.Save("ops/release-baseline.json", new ReleaseManifest(2, "test", [new("Payments", true, [f.Target])]));
        Assert.False((await f.Gate().EvaluateAsync("reviewer", default)).ProductionReady);
        File.Delete(Path.Combine(f.Root, "ops/release-baseline.json"));
        Assert.Contains("ManifestMissingOrInvalid", (await f.Gate().EvaluateAsync("reviewer", default)).Blocks);
    }
    [Fact]
    public async Task Route_gate_enforces_selected_content_methods_and_disabled_features()
    {
        using var f = new Fixture(); await f.Qualify(); var d = await f.Gate().EvaluateAsync("reviewer", default);
        Assert.True(ReleaseBoundary.Allows(d, "GET", "/api/v1/read/selected-read"));
        Assert.False(ReleaseBoundary.Allows(d, "GET", "/api/v1/read/another-collection"));
        Assert.False(ReleaseBoundary.Allows(d, "POST", "/api/v1/read/selected-read"));
        Assert.False(ReleaseBoundary.Allows(d, "POST", "/api/v1/customer/tenants/anything/checkout"));
        Assert.False(ReleaseBoundary.Allows(d, "POST", "/api/v1/social/tenants/anything/schedule"));
        Assert.False(ReleaseBoundary.Allows(d with { Blocks = ["EvidenceUnavailable"] }, "GET", "/api/v1/read/selected-read"));
    }
}
