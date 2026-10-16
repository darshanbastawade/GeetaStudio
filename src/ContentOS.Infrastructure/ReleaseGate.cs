using System.Security.Cryptography;
using System.Text.Json;
using ContentOS.Application;
using Microsoft.Extensions.Configuration;
namespace ContentOS.Infrastructure;

public sealed class ReleaseGate(IConfiguration configuration, IReleaseModuleEvidence modules, string root, string codeFingerprint, string environment) : IReleaseGate
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    public static string BinaryFingerprint(string directory) => ReleaseRules.Hash(Directory.GetFiles(directory)
        .Where(p => (Path.GetFileName(p).StartsWith("ContentOS.", StringComparison.Ordinal) || Path.GetFileName(p).StartsWith("GeetaStudio.", StringComparison.Ordinal))
            && (p.EndsWith(".dll", StringComparison.Ordinal) || p.EndsWith(".deps.json", StringComparison.Ordinal) || p.EndsWith(".runtimeconfig.json", StringComparison.Ordinal)))
        .OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(p => new { File = Path.GetFileName(p), Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray());
    private string LocalPath(string path)
    {
        var resolved = Path.GetFullPath(path, root); var boundary = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(boundary, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new IOException("Evidence outside operational root.");
        for (var item = new FileInfo(resolved) as FileSystemInfo; item != null && item.FullName.Length >= boundary.Length; item = item is FileInfo f ? f.Directory : ((DirectoryInfo)item).Parent)
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked evidence path.");
        return resolved;
    }
    private T Load<T>(string path)
    {
        var p = LocalPath(path); if (new FileInfo(p).Length > 1048576) throw new IOException("Operational evidence capacity exceeded.");
        return JsonSerializer.Deserialize<T>(File.ReadAllBytes(p), Options) ?? throw new JsonException("Empty operational evidence.");
    }
    public async Task<ReleaseDecision> EvaluateAsync(string actor, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow; var policy = ReleaseRules.PolicyFingerprint;
        var config = ReleaseRules.Hash(new { Environment = environment, OperationalRoot = Path.GetFullPath(root), Values = configuration.AsEnumerable().Where(x => !new[] { "Release:CheckOnly", "Release:Output", "Release:RequireProduction", "Release:Root", "Release:Manifest", "Release:Evidence" }.Contains(x.Key, StringComparer.OrdinalIgnoreCase)).OrderBy(x => x.Key, StringComparer.Ordinal).ToArray() });
        ReleaseManifest manifest; ReleaseReceipt[] receipts; string[] global;
        try { manifest = Load<ReleaseManifest>(configuration["Release:Manifest"] ?? "ops/release-baseline.json"); global = ReleaseRules.Validate(manifest); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or ArgumentException or NullReferenceException)
        { return new("Unavailable manifest", now, codeFingerprint, policy, config, "", false, ["ManifestMissingOrInvalid"], []); }
        try { receipts = Load<ReleaseReceipt[]>(configuration["Release:Evidence"] ?? "ops/release-evidence.json"); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or ArgumentException) { receipts = []; global = [.. global, "OperationalEvidenceMissingOrInvalid"]; }
        if (receipts.Any(x => x == null || string.IsNullOrWhiteSpace(x.Id))) { receipts = []; global = [.. global, "OperationalEvidenceMissingOrInvalid"]; }
        if (receipts.Select(x => x.Id).Distinct().Count() != receipts.Length) global = [.. global, "DuplicateEvidenceIdentity"];
        var identity = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(LocalPath(configuration["Release:Manifest"] ?? "ops/release-baseline.json")))); var features = new List<ReleaseFeatureDecision>();
        foreach (var f in manifest.Features)
        {
            if (!f.Selected) { features.Add(new(f.Key, false, false, ["DisabledByManifest"], [])); continue; }
            var featureBlocks = new List<string>(); var targets = new List<ReleaseTargetDecision>();
            if (!ReleaseRules.Supported.Contains(f.Key)) featureBlocks.Add("UnsupportedPilotFeature");
            if (f.Targets.Length == 0) featureBlocks.Add("ExplicitTargetsRequired");
            foreach (var target in f.Targets.Take(20))
            {
                ReleaseObservation observed;
                try { observed = await modules.ObserveAsync(f.Key, target, actor, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception) { observed = new("", "", ["ModuleEvidenceUnavailable"], [], []); }
                var blocks = observed.Blocks.ToList();
                if (f.Key == "ManualExport" && (observed.WatchDependencies == null || observed.WatchDependencies.Length == 0 || observed.WatchDependencies.Any(id => !manifest.Features.Any(feature => feature.Key == "WATCH" && feature.Selected && feature.Targets.Any(t => t.ResourceId == id && t.TenantId == target.TenantId && t.UniverseId == target.UniverseId))))) blocks.Add("SelectedWATCHDependencyRequired");
                if (string.IsNullOrWhiteSpace(observed.ContentFingerprint) || string.IsNullOrWhiteSpace(observed.ProviderFingerprint)) blocks.Add("CurrentModuleBindingsRequired");
                var subject = ReleaseRules.Hash(new { Code = codeFingerprint, Policy = policy, Configuration = config, Manifest = identity, Feature = f.Key, Target = target, observed.ContentFingerprint, observed.ProviderFingerprint });
                foreach (var kind in ReleaseRules.OperationalGates)
                {
                    var entries = receipts.Where(x => x.Feature == f.Key && x.TargetKey == target.Key && x.Kind == kind).ToArray();
                    if (entries.Length != 1) { blocks.Add(kind + ":MissingOrAmbiguousEvidence"); continue; }
                    var receipt = entries[0]; string? reason;
                    try
                    {
                        var path = LocalPath(receipt.Path); if (new FileInfo(path).Length > 1048576) throw new IOException("Evidence capacity exceeded."); var bytes = File.ReadAllBytes(path);
                        if (bytes.Length > 1048576 || Convert.ToHexString(SHA256.HashData(bytes)) != receipt.Sha256) reason = "EvidenceFileChanged";
                        else reason = ReleaseRules.ReceiptBlock(receipt, JsonSerializer.Deserialize<ReleaseProof>(bytes, Options)!, subject, now);
                    }
                    catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or ArgumentException or NullReferenceException) { reason = "EvidenceFileMissingOrInvalid"; }
                    if (reason != null) blocks.Add(kind + ":" + reason);
                }
                targets.Add(new(target, subject, blocks.Distinct().Order(StringComparer.Ordinal).ToArray(), [.. observed.References, .. receipts.Where(x => x.Feature == f.Key && x.TargetKey == target.Key).Select(x => x.Id + ":" + x.Reference)], observed.ResourceIds, observed.ContentFingerprint, observed.ProviderFingerprint));
            }
            if (targets.Any(x => x.Blocks.Length > 0)) featureBlocks.Add("TargetEvidenceBlocked");
            features.Add(new(f.Key, true, global.Length == 0 && featureBlocks.Count == 0, featureBlocks.ToArray(), targets.ToArray(), ReleaseRules.Requirements(f.Key)));
        }
        var watchReady = features.Any(x => x.Key == "WATCH" && x.Ready);
        features = features.Select(x => x.Key == "ManualExport" && x.Selected && !watchReady ? x with { Ready = false, Blocks = [.. x.Blocks, "WATCHDependencyBlocked"] } : x).ToList();
        return new(manifest.Release, now, codeFingerprint, policy, config, identity, global.Length == 0 && features.Any(x => x.Selected) && features.Where(x => x.Selected).All(x => x.Ready), global, features.ToArray());
    }
}
