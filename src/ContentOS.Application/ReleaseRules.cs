using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace ContentOS.Application;

// Operational attestations supplement the existing module stores; they cannot override their eligibility gates.
public sealed record ReleaseTarget(string Key, Guid TenantId, Guid UniverseId, Guid ResourceId, Guid EvaluationRunId);
public sealed record ReleaseFeature(string Key, bool Selected, ReleaseTarget[] Targets);
public sealed record ReleaseManifest(int Version, string Release, ReleaseFeature[] Features);
public sealed record ReleaseReceipt(string Id, string Feature, string TargetKey, string Kind, string Mode,
    string SubjectFingerprint, DateTimeOffset IssuedUtc, DateTimeOffset ExpiresUtc, string Reference,
    string Path, string Sha256, bool Revoked = false, string? SupersededBy = null);
public sealed record ReleaseProof(string Kind, string Mode, string SubjectFingerprint, string Outcome,
    DateTimeOffset ObservedUtc, bool FixtureOnly, string[] EvidenceReferences, Dictionary<string, double>? Measurements = null, string? ReviewerReference = null);
public sealed record ReleaseMetric(string Name, double Minimum, double Maximum);
public sealed record ReleaseObservation(string ContentFingerprint, string ProviderFingerprint, string[] Blocks,
    string[] References, Guid[] ResourceIds, Guid[]? WatchDependencies = null);
public sealed record ReleaseTargetDecision(ReleaseTarget Target, string SubjectFingerprint, string[] Blocks, string[] EvidenceReferences, Guid[] ResourceIds, string ContentFingerprint = "", string ProviderFingerprint = "");
public sealed record ReleaseFeatureDecision(string Key, bool Selected, bool Ready, string[] Blocks, ReleaseTargetDecision[] Targets, string[]? RequiredEvidence = null);
public sealed record ReleaseDecision(string Release, DateTimeOffset EvaluatedUtc, string CodeFingerprint, string PolicyFingerprint,
    string ConfigurationFingerprint, string ManifestFingerprint, bool ProductionReady, string[] Blocks, ReleaseFeatureDecision[] Features);
public interface IReleaseGate { Task<ReleaseDecision> EvaluateAsync(string actor, CancellationToken ct); }
public interface IReleaseModuleEvidence { Task<ReleaseObservation> ObserveAsync(string feature, ReleaseTarget target, string actor, CancellationToken ct); }

public static class ReleaseRules
{
    public static readonly string[] Supported = ["READ", "WATCH", "ManualExport"];
    public static readonly string[] Known = ["READ", "WATCH", "ManualExport", "TALK", "Payments", "B2B", "AutomaticSocial", "CREATE", "Analytics"];
    public static readonly string[] OperationalGates = ["OwnerScope", "SecurityPrivacy", "Usability", "Hosting", "Monitoring", "Recovery", "MatchingCI", "ProviderSwitch"];
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static string PolicyFingerprint => Hash(new { Version = "release.v1", Supported, Known, OperationalGates, Binding = "exact-code-config-manifest-module-provider-m12", MaxEvidenceDays = 30, Criteria = OperationalGates.Select(kind => new { Kind = kind, Bounds = Metrics(kind) }).ToArray() });
    public static string[] Requirements(string feature) => ["CurrentCodeContentPolicyProviderBindings", "CurrentSourceRights", "M12HumanQualification", "CurrentEditorialApproval", .. feature == "READ" ? new[] { "CurrentPublicCollectionAndReaderEvidence" } : new[] { "ApprovedArtVoiceAndMediaRights", "ExactEpisodeHumanListeningVisualReview", "CurrentWATCHMaster" }, .. OperationalGates];
    public static string RequiredMode(string kind) => kind is "OwnerScope" or "SecurityPrivacy" or "Usability" ? "Human" : "Measured";
    public static ReleaseMetric[] Metrics(string kind) => kind switch
    {
        "OwnerScope" => [],
        "SecurityPrivacy" => [new("ReviewedBoundaries", 1, 1000000), new("CriticalFindings", 0, 0), new("HighFindings", 0, 0)],
        "Usability" => [new("Participants", 1, 1000000), new("CompletedTasks", 1, 1000000), new("CriticalTaskFailures", 0, 0)],
        "Hosting" => [new("VerifiedChecks", 4, 1000000), new("TlsFailures", 0, 0), new("IsolationFailures", 0, 0), new("RetentionFailures", 0, 0), new("SecretLeaks", 0, 0)],
        "Monitoring" => [new("ObservationSeconds", 300, 2592000), new("AlertTests", 1, 1000000), new("AlertFailures", 0, 0)],
        "Recovery" => [new("RtoSeconds", 0.001, 300), new("RpoSeconds", 0, 900), new("RestoredRecords", 1, 1000000000), new("LineageMismatches", 0, 0), new("LostAcknowledgedOperations", 0, 0), new("AssetHashMismatches", 0, 0), new("RollbackFailures", 0, 0)],
        "MatchingCI" => [new("OfflineExpected", 1, 1000000), new("OfflinePassed", 1, 1000000), new("SqlExpected", 1, 1000000), new("SqlPassed", 1, 1000000), new("Failures", 0, 0), new("Skips", 0, 0), new("FormatViolations", 0, 0), new("ModelDifferences", 0, 0), new("HighVulnerabilities", 0, 0)],
        "ProviderSwitch" => [new("OperationsChecked", 1, 1000000), new("ContractMismatches", 0, 0), new("ResourceBindingMismatches", 0, 0), new("DuplicateEffects", 0, 0), new("UnreconciledUnknowns", 0, 0)],
        _ => [new("UnsupportedEvidence", 1, 0)]
    };
    public static string[] Validate(ReleaseManifest manifest)
    {
        var blocks = new List<string>();
        if (manifest.Version != 2 || string.IsNullOrWhiteSpace(manifest.Release) || manifest.Features.Length == 0) blocks.Add("InvalidManifest");
        if (manifest.Features.Select(x => x.Key).Distinct().Count() != manifest.Features.Length || manifest.Features.Any(x => !Known.Contains(x.Key))) blocks.Add("UnknownOrDuplicateFeature");
        foreach (var f in manifest.Features.Where(x => x.Selected))
        {
            if (!Supported.Contains(f.Key)) blocks.Add(f.Key + ":UnsupportedPilotFeature");
            if (f.Targets.Length == 0 || f.Targets.Length > 20 || f.Targets.Select(x => x.Key).Distinct().Count() != f.Targets.Length) blocks.Add(f.Key + ":ExplicitBoundedTargetsRequired");
            if (f.Targets.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.TenantId == Guid.Empty || x.UniverseId == Guid.Empty || x.ResourceId == Guid.Empty || x.EvaluationRunId == Guid.Empty)) blocks.Add(f.Key + ":InvalidTarget");
        }
        if (!manifest.Features.Any(x => x.Selected)) blocks.Add("NoSelectedFeature");
        if (manifest.Features.Any(x => x.Key == "ManualExport" && x.Selected) && !manifest.Features.Any(x => x.Key == "WATCH" && x.Selected)) blocks.Add("ManualExport:WATCHDependencyRequired");
        return blocks.ToArray();
    }
    public static string? ReceiptBlock(ReleaseReceipt receipt, ReleaseProof proof, string subject, DateTimeOffset now)
    {
        if (receipt.Revoked || !string.IsNullOrEmpty(receipt.SupersededBy)) return "RevokedOrSuperseded";
        if (receipt.IssuedUtc > now || receipt.ExpiresUtc <= now || receipt.ExpiresUtc <= receipt.IssuedUtc || receipt.ExpiresUtc > receipt.IssuedUtc.AddDays(30)) return "ExpiredOrInvalidWindow";
        if (receipt.SubjectFingerprint != subject || proof.SubjectFingerprint != subject) return "BindingChanged";
        if (proof.FixtureOnly || receipt.Reference.StartsWith("fixture:", StringComparison.OrdinalIgnoreCase) || proof.EvidenceReferences.Length == 0 || proof.EvidenceReferences.Any(x => string.IsNullOrWhiteSpace(x) || x.StartsWith("fixture:", StringComparison.OrdinalIgnoreCase))) return "FixtureOrMissingEvidence";
        if (proof.Kind != receipt.Kind || proof.Mode != RequiredMode(receipt.Kind) || receipt.Mode != proof.Mode || string.IsNullOrWhiteSpace(receipt.Reference) || proof.Outcome != "Passed" || proof.ObservedUtc > now || proof.ObservedUtc < receipt.IssuedUtc || proof.ObservedUtc >= receipt.ExpiresUtc) return "UnqualifiedEvidence";
        if (RequiredMode(receipt.Kind) == "Human" && (string.IsNullOrWhiteSpace(proof.ReviewerReference) || !proof.ReviewerReference.StartsWith("human:", StringComparison.OrdinalIgnoreCase))) return "HumanReviewerRequired";
        if (Metrics(receipt.Kind).Any(metric => proof.Measurements == null || !proof.Measurements.TryGetValue(metric.Name, out var value) || !double.IsFinite(value) || value < metric.Minimum || value > metric.Maximum)) return "MeasuredCriteriaNotMet";
        if (receipt.Kind == "MatchingCI" && (proof.Measurements!["OfflineExpected"] != proof.Measurements["OfflinePassed"] || proof.Measurements["SqlExpected"] != proof.Measurements["SqlPassed"])) return "IncompleteCI";
        return null;
    }
}
