using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlNarrationTests
{
    private sealed class Speech(bool uncertain = false) : ISpeechSynthesisProvider
    {
        public string Key => "media.speech.segment-fixture.v1";
        public int Submitted, Reconciled;
        public MediaProviderCapabilities Capabilities => new("Speech", ["en-IN"], false, false, false, true, true, "OfflineFixture", ["ControlledToneNotNarration", "NoLiveVoicePermission"]);
        public Task<MediaProviderResult> SubmitAsync(MediaProviderRequest r, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Submitted++; return Task.FromResult(uncertain && r.PromptOrText.StartsWith("Controlled") ? new MediaProviderResult("Unknown", "fixture-segment:" + r.OperationId, null, null, "ControlledAmbiguity") : Result(r)); }
        public Task<MediaProviderResult> ReconcileAsync(MediaProviderRequest r, string? resource, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Reconciled++; Assert.Equal("fixture-segment:" + r.OperationId, resource); return Task.FromResult(Result(r)); }
        private static MediaProviderResult Result(MediaProviderRequest r) => new("Completed", "fixture-segment:" + r.OperationId, WatchTestFixture.Voice(Math.Max(2, Math.Ceiling(r.PromptOrText.Length / 18.0))), 0, "ControlledToneNotNarration");
    }
    private static async Task<(WatchTestFixture.Fixture Fixture, CreateWatch Request)> Setup(IServiceProvider s, Speech speech, string renderer)
    {
        var f = await WatchTestFixture.Setup(s, renderer, "prompt6-" + Guid.NewGuid()); var c = f.Context; var g = new RegistryTestFixture.Fixture(c.Owner, c.Universe, c.Audience, c.Bible, c.Episode); var media = s.GetRequiredService<IMediaLibrary>();
        var profile = await MediaTestFixture.Profile(s, g, speech.Key, "Speech", "en-IN", "fixture-pinned-voice");
        var output = MediaTestFixture.Upload(g, f.Character, "segment-voice", [], "Voice") with { ExpectedSha256 = "" };
        var seed = await media.GenerateAsync(c.Owner, c.Universe, new(Guid.NewGuid(), profile, output, "Seed controlled voice binding.", [], [], 0), default); await MediaTestFixture.Drain(s);
        var voice = (await media.AssetsAsync(c.Owner, c.Universe, default)).SelectMany(x => x.Versions).Single(x => x.GenerationId == seed.Id); await MediaTestFixture.Review(s, g, voice);
        var vp = (await media.ProfilesAsync(c.Owner, c.Universe, default)).Single(x => x.Id == profile); var package = await MediaTestFixture.Package(s, g, f.Character, f.Image, voice, "segment-package", vp.Configuration.Key, vp.Configuration.ResourceKey);
        var presentation = await media.BindPackageAsync(c.Owner, c.Universe, package.Id, Guid.NewGuid(), default); await RegistryTestFixture.Review(s, c.Owner, c.Universe, presentation);
        const string body = "Controlled technical fixture: tones are not spoken narration. An original learner notices that a classmate has a different account of a shared problem. Before deciding, the learner asks a calm question, compares the observations, and invites another perspective. This original scene is a test of complete captions and durable segment ordering, with real artwork, voice permission and human review still pending.";
        var editor = s.GetRequiredService<IEditorialWorkbench>(); var draft = EditorialTestFixture.Request(c, "segmented-script") with { Draft = EditorialTestFixture.Request(c).Draft! with { Body = body } }; var script = await editor.StartAsync(c.Owner, c.Universe, draft, default);
        var registry = s.GetRequiredService<IUniverseRegistry>(); var episode = (await registry.EpisodesAsync(c.Owner, c.Universe, default)).Single(x => x.Id == c.Episode); var brief = RegistryTestFixture.Brief(g) with { PinnedChoices = [new(f.Character.CharacterId, "Lead", presentation)], Budget = new(2, 0, "INR", true) };
        await registry.ComposeAsync(c.Owner, c.Universe, new(Guid.NewGuid(), episode.RowVersion, brief, "None", [new(f.Character.CharacterId, "FictionalDialogue", body, ["curious"], [], true)], [], ContentRevisionId: script.RevisionId), default);
        script = await EditorialTestFixture.Approved(s, c, await editor.InspectAsync(c.Owner, c.Universe, script.Id, default));
        f = f with { Script = script, Voice = voice, Presentation = presentation };
        var request = WatchTestFixture.Request(f) with { Scenes = [new("narrator", 0, body.Length, f.Character.CharacterId, presentation, f.Image.Id, null, "fixture:generated-controlled-tone", VoiceGeneration: new(Guid.NewGuid(), profile, output, body, [], [], 0))], DeadlineSeconds = 900 };
        return (f, request);
    }
    private static async Task<WatchProductionView> VoiceCheckpoint(IServiceProvider s, WatchTestFixture.Fixture f, CreateWatch plan)
    {
        var watch = s.GetRequiredService<IWatchFactory>(); var c = f.Context; var p = await watch.CreateAsync(c.Owner, c.Universe, plan, default);
        for (var i = 0; i < 6; i++) { await watch.ProcessNextAsync(default); p = await watch.InspectAsync(c.Owner, c.Universe, p.Id, default); if (p.Stages.Single(x => x.Kind == "Voice").State == "Waiting") return p; }
        throw new InvalidOperationException("Expected a bounded M16 voice checkpoint.");
    }
    private static async Task ReviewOutputs(IServiceProvider s, WatchTestFixture.Fixture f, Guid production)
    {
        var c = f.Context; var media = s.GetRequiredService<IMediaLibrary>(); var ids = (await media.GenerationsAsync(c.Owner, c.Universe, default)).Where(x => x.Snapshot.Request.Admission?.ProductionId == production).Select(x => x.AcceptedVersionId).ToArray();
        foreach (var asset in (await media.AssetsAsync(c.Owner, c.Universe, default)).SelectMany(x => x.Versions).Where(x => ids.Contains(x.Id))) await media.ReviewAsync(c.Owner, c.Universe, asset.Id, new(Guid.NewGuid(), asset.RowVersion, "Approve", "Fixture", "fixture:technical-segment-not-human-voice-review", []), default);
    }
    [SqlFact]
    public async Task Planned_narration_uses_original_media_jobs_and_renders_after_known_failure_restart()
    {
        var speech = new Speech(); using var root = WatchTestFixture.Services(x => x.AddSingleton<ISpeechSynthesisProvider>(speech)); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var (f, request) = await Setup(s, speech, "watch.fail-once.v1"); var c = f.Context; var watch = s.GetRequiredService<IWatchFactory>(); var db = s.GetRequiredService<CoreDbContext>(); var before = await db.MediaGenerations.CountAsync();
        var plan = await watch.PlanNarrationAsync(c.Owner, c.Universe, request, default); Assert.Equal(JsonSerializer.Serialize(plan), JsonSerializer.Serialize(await watch.PlanNarrationAsync(c.Owner, c.Universe, request, default))); Assert.Equal(before, await db.MediaGenerations.CountAsync());
        await Assert.ThrowsAsync<CoreAccessException>(() => watch.PlanNarrationAsync(new(Guid.NewGuid(), c.Owner.Actor), c.Universe, request, default));
        var p = await VoiceCheckpoint(s, f, plan); await MediaTestFixture.Drain(s); await ReviewOutputs(s, f, p.Id); var submitted = speech.Submitted;
        Assert.Equal(plan.Scenes.Length + 1, submitted);
        using var restart = root.CreateScope(); var fresh = restart.ServiceProvider; p = await WatchTestFixture.Drain(fresh, f, p.Id); Assert.Equal("Failed", p.State); var accepted = p.Stages.Where(x => x.State == "Accepted").ToDictionary(x => x.Id, x => x.Attempts);
        var resumed = fresh.GetRequiredService<IWatchFactory>();
        for (var i = 0; p.State == "Failed" && i < plan.Aspects.Length; i++)
        {
            foreach (var checkpoint in p.Stages.Where(x => x.State == "Accepted")) accepted[checkpoint.Id] = checkpoint.Attempts;
            await resumed.ControlAsync(c.Owner, c.Universe, p.Id, new(Guid.NewGuid(), p.RowVersion, "Resume", "fixture:resume-original-render"), default);
            p = await WatchTestFixture.Drain(fresh, f, p.Id);
        }
        Assert.Equal("Review", p.State); Assert.Equal(submitted, speech.Submitted); Assert.All(p.Stages.Where(x => accepted.ContainsKey(x.Id)), x => Assert.Equal(accepted[x.Id], x.Attempts));
        Assert.Equal(p.Id, (await resumed.CreateAsync(c.Owner, c.Universe, plan, default)).Id); Assert.Equal(f.Script.Current!.Body, p.Timeline!.Script); Assert.Equal(f.Script.Current.Body, string.Concat(p.Timeline.Captions.Select(x => x.Text))); Assert.Equal(plan.Scenes.Select(x => x.Key), p.Timeline.Scenes.Select(x => x.Key)); Assert.All(p.Timeline.Scenes, x => Assert.Equal(f.Character.CharacterId, x.CharacterId)); Assert.Equal(0, p.CreationCost);
        Assert.Equal(2, p.Artifacts.Length); foreach (var a in p.Artifacts) { var bytes = await resumed.PreviewAsync(c.Owner, c.Universe, a.Id, default); Assert.True(bytes.Bytes.Length > 1000); }
        var results = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "M25-2-prompt6", "sample"); Directory.CreateDirectory(results);
        foreach (var a in p.Artifacts) await File.WriteAllBytesAsync(Path.Combine(results, a.Aspect + "-CONTROLLED-TONE-NOT-NARRATION.mp4"), (await resumed.PreviewAsync(c.Owner, c.Universe, a.Id, default)).Bytes);
        await File.WriteAllTextAsync(Path.Combine(results, "captions.vtt"), fresh.GetRequiredService<ISubtitleProvider>().Vtt(p.Timeline.Captions)); await File.WriteAllTextAsync(Path.Combine(results, "captions.srt"), fresh.GetRequiredService<ISubtitleProvider>().Srt(p.Timeline.Captions));
        await File.WriteAllTextAsync(Path.Combine(results, "manifest.json"), JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => resumed.ExportAsync(c.Owner, c.Universe, p.Id, "Publish", default));
    }
    [SqlFact]
    public async Task Unknown_segment_reconciles_original_job_without_resubmission_and_cancel_preserves_accepted_stages()
    {
        var speech = new Speech(true); using var root = WatchTestFixture.Services(x => x.AddSingleton<ISpeechSynthesisProvider>(speech)); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var (f, request) = await Setup(s, speech, "watch.ffmpeg.v1"); var c = f.Context; var watch = s.GetRequiredService<IWatchFactory>(); var plan = await watch.PlanNarrationAsync(c.Owner, c.Universe, request, default); var p = await VoiceCheckpoint(s, f, plan); await MediaTestFixture.Drain(s); var media = s.GetRequiredService<IMediaLibrary>(); var runs = (await media.GenerationsAsync(c.Owner, c.Universe, default)).Where(x => x.Snapshot.Request.Admission?.ProductionId == p.Id).ToArray(); Assert.Contains(runs, x => x.State == "Unknown"); var count = speech.Submitted;
        using var restart = root.CreateScope(); var fresh = restart.ServiceProvider.GetRequiredService<IMediaLibrary>();
        foreach (var run in runs.Where(x => x.State == "Unknown")) { var reconcile = new ReconcileMedia(Guid.NewGuid(), run.Id); await fresh.ReconcileAsync(c.Owner, c.Universe, reconcile, default); await fresh.ReconcileAsync(c.Owner, c.Universe, reconcile, default); }
        Assert.Equal(count, speech.Submitted); Assert.Equal(runs.Count(x => x.State == "Unknown"), speech.Reconciled); Assert.All((await fresh.GenerationsAsync(c.Owner, c.Universe, default)).Where(x => runs.Select(y => y.Id).Contains(x.Id)), x => Assert.Equal("Completed", x.State));
        p = await watch.InspectAsync(c.Owner, c.Universe, p.Id, default); await watch.ControlAsync(c.Owner, c.Universe, p.Id, new(Guid.NewGuid(), p.RowVersion, "Cancel", "fixture:stop-before-render"), default); p = await watch.InspectAsync(c.Owner, c.Universe, p.Id, default); Assert.Equal("Cancelled", p.State); Assert.Equal("Accepted", p.Stages.Single(x => x.Kind == "Storyboard").State); Assert.Equal("Accepted", p.Stages.Single(x => x.Kind == "Assets").State);
    }
}
