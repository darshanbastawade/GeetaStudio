using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Foundation.Tests;

internal static partial class HistoricalTestFixture
{
    internal static async Task<WatchProductionView> Watch(IServiceProvider s, Fixture f, bool fact, string? outputDirectory = null)
    {
        var db = s.GetRequiredService<CoreDbContext>(); var u = f.Universe; var owner = f.Owner;
        if (!await db.Locales.AnyAsync(x => x.Id == "en-US")) { db.Locales.Add(new() { Id = "en-US", Name = "US English" }); await db.SaveChangesAsync(); }
        var id = Guid.NewGuid().ToString("N"); var registry = s.GetRequiredService<IUniverseRegistry>();
        var episodeId = await registry.RegisterEpisodeAsync(owner, u, new(Guid.NewGuid(), "history-watch-" + id), default);
        var g = new RegistryTestFixture.Fixture(owner, u, f.Audience, f.Bible, episodeId);
        var rules = f.Guide.Profile.Rules with { Name = "Archive Watch Guide " + id[..8], Aliases = [], Locales = ["en-US"], EpisodeId = episodeId };
        var c = await registry.RegisterCharacterAsync(owner, u, new(Guid.NewGuid(), "watch-guide-" + id, "EpisodeOnly", rules), default);
        await RegistryTestFixture.Review(s, owner, u, c.Profile.Id);
        await registry.ControlCharacterAsync(owner, u, c.CharacterId, new(Guid.NewGuid(), c.RowVersion, "Activate", "fixture:original-fictional-guide"), default);
        c = (await registry.CharactersAsync(owner, u, default)).Single(x => x.CharacterId == c.CharacterId);
        var body = fact ? "Two cited historical accounts place Shivaji's coronation in 1674." : "An original fictional archive guide asks: Which record supports this claim? This invented dialogue is not attributed to Shivaji.";
        var original = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == f.Anchors["coronation"]);
        var spans = fact ? (await db.ClaimSpans.AsNoTracking().Where(x => x.ClaimRevisionId == original.Id).ToArrayAsync()).Select(x => new EvidenceSpanInput(x.SnapshotId, x.Start, x.Length, x.ExactText, x.Sha256, x.Locator, x.Relation)).ToArray() : [];
        var engine = s.GetRequiredService<IEvidenceEngine>(); var type = fact ? "HistoricalClaim" : "FictionalScene";
        var claim = await engine.SubmitAsync(owner, u, new(Guid.NewGuid(), "watch-claim-" + id, "", type, type, "en-US", body, "Private historical sample", "Historical accounts or original invented guide dialogue", fact ? "Limited" : "Fictional", original.RubricId, spans), default);
        await EvidenceTestFixture.ApproveAsync(engine, owner, u, claim, fact ? "Corroborated" : "Attributed", "Supported");
        var editor = s.GetRequiredService<IEditorialWorkbench>();
        var script = await editor.StartAsync(owner, u, new(Guid.NewGuid(), "watch-script-" + id, "Episode", "Private historical learning; historians and human listening review pending.", new(Guid.NewGuid(), f.Subject, f.Audience, "en-US", fact ? "Coronation: cited historical accounts" : "Invented archive-guide dialogue", body, fact ? "Historical" : "Fiction", fact ? "PublicDomain" : "Owned", "fixture:M26-labelled-history-never-historical-impersonation"), PublicIntended: false), default);
        await engine.LinkAsync(owner, u, new(Guid.NewGuid(), claim, script.RevisionId, 0, body.Length), default);
        foreach (var snap in spans.Select(x => x.SnapshotId).Distinct()) await s.GetRequiredService<ISourceRegistry>().LinkAsync(owner, u, new(Guid.NewGuid(), snap, null, script.RevisionId, null, "historical-script"), default);
        var media = s.GetRequiredService<IMediaLibrary>();
        var image = await media.ImportAsync(owner, u, MediaTestFixture.Upload(g, c, "history-sheet-" + id, MediaFiles.ReferenceCard(c.Profile.Rules.VisualAnchor, [])), default);
        await MediaTestFixture.Review(s, g, image);
        var token = WindowsSpeechProvider.AvailableEnglishVoices().FirstOrDefault() ?? throw new InvalidOperationException("Install a Windows English SAPI voice for historical WATCH samples.");
        var vpId = await MediaTestFixture.Profile(s, g, "media.speech.windows-sapi.v1", "Speech", "en-US", token);
        var run = await media.GenerateAsync(owner, u, new(Guid.NewGuid(), vpId, MediaTestFixture.Upload(g, c, "history-voice-" + id, [], "Voice") with { ExpectedSha256 = "", License = MediaTestFixture.License with { SourceSnapshotIds = spans.Select(x => x.SnapshotId).Distinct().ToArray() } }, body, [], [], 0), default);
        await MediaTestFixture.Drain(s); run = (await media.GenerationsAsync(owner, u, default)).Single(x => x.Id == run.Id);
        if (run.State != "Completed") throw new InvalidOperationException("Historical speech failed: " + run.Outcome);
        var voice = (await media.AssetsAsync(owner, u, default)).SelectMany(x => x.Versions).Single(x => x.Id == run.AcceptedVersionId);
        await MediaTestFixture.Review(s, g, voice); var vp = (await media.ProfilesAsync(owner, u, default)).Single(x => x.Id == vpId);
        var package = await MediaTestFixture.Package(s, g, c, image, voice, "history-package-" + id, vp.Configuration.Key, vp.Configuration.ResourceKey);
        var presentation = await media.BindPackageAsync(owner, u, package.Id, Guid.NewGuid(), default); await RegistryTestFixture.Review(s, owner, u, presentation);
        var episode = (await registry.EpisodesAsync(owner, u, default)).Single(x => x.Id == episodeId);
        var brief = RegistryTestFixture.Brief(g) with { Locale = "en-US", SituationTags = ["history"], LearningObjectives = ["evaluate-historical-sources"], PinnedChoices = [new(c.CharacterId, "Lead", presentation)], Budget = new(2, 0, "INR", true) };
        await registry.ComposeAsync(owner, u, new(Guid.NewGuid(), episode.RowVersion, brief, "None", [new(c.CharacterId, "FictionalDialogue", "The fictional guide reads the cited learning script; these are not historical quotations by a real person.", ["curious"], [], true)], [], ContentRevisionId: script.RevisionId), default);
        script = await editor.InspectAsync(owner, u, script.Id, default);
        script = await editor.ActAsync(owner, u, script.Id, EditorialTestFixture.Action(script, "Draft"), default);
        var quality = s.GetRequiredService<IQualityEvaluation>();
        var test = EvaluationFixtureCatalog.Create(f.Audience).Cases.First() with { Locale = "en-US", RequiredProperties = [], ForbiddenProperties = [], ExpectedTextIds = [], AllowedLifeStages = ["Adult"], Cast = brief };
        var dataset = await quality.RegisterDatasetAsync(owner, u, new(Guid.NewGuid(), "history-watch-" + id, 1, "Fixture only; human history/listening acceptance pending.", [test]), default);
        var candidate = await quality.RegisterCandidateAsync(owner, u, new(Guid.NewGuid(), "history-watch-" + id, 1, new("recorded.v1", "generated-text.v1", "none.v1", "history.v1", "fixture.v1")), default);
        var report = await quality.RunAsync(owner, u, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development", RecordedOutputs: [new(test.Key, new(body, "en-US", [], false, [new(c.CharacterId, c.Profile.Id, "Lead", c.Profile.Rules.VoiceAnchor, c.Profile.Rules.LifeStage)], [], null, [], 0, "INR"))]), default);
        if (report.Results.Any(x => x.Deterministic.Any(y => y.Severity == "Critical"))) throw new InvalidOperationException("Historical WATCH evaluation: " + System.Text.Json.JsonSerializer.Serialize(report.Results));
        script = await editor.ActAsync(owner, u, script.Id, EditorialTestFixture.Action(script, "Evaluate", new(report.Id, test.Key)), default);
        script = await editor.ActAsync(owner, u, script.Id, EditorialTestFixture.Action(script, "Review"), default);
        script = await editor.ActAsync(owner, u, script.Id, EditorialTestFixture.Action(script, "Approve"), default);
        var w = s.GetRequiredService<IWatchFactory>(); var template = await w.TemplateAsync(owner, u, new(Guid.NewGuid(), "history-layout-" + id, 1, new()), default);
        var tv = (await w.TemplatesAsync(owner, u, default)).Single(x => x.Id == template);
        await w.ReviewTemplateAsync(owner, u, template, new(Guid.NewGuid(), tv.RowVersion, "Approve", "Fixture", "fixture:historical-layout-human-review-pending", [], 1), default);
        var profile = await w.ProfileAsync(owner, u, new(Guid.NewGuid(), "history-renderer-" + id, 1, "watch.ffmpeg.v1", "local:historical-preview", template, 0, "INR", "Fixture", "fixture:owned-renderer"), default);
        var wf = new WatchTestFixture.Fixture(new(owner, u, f.Audience, f.Subject, f.Bible, episodeId), c, script, image, voice, presentation, template, profile);
        var p = await w.CreateAsync(owner, u, WatchTestFixture.Request(wf, "history-" + id) with { Scenes = [new("historical-preview", 0, body.Length, c.CharacterId, presentation, image.Id, voice.Id, "fixture:Windows-SAPI-exact-labelled-script")] }, default);
        p = await WatchTestFixture.Drain(s, wf, p.Id);
        if (p.State != "Review") throw new InvalidOperationException("Historical WATCH failed: " + System.Text.Json.JsonSerializer.Serialize(p.Stages));
        await w.ReviewAsync(owner, u, p.Id, new(Guid.NewGuid(), p.RowVersion, "Approve", "Fixture", "fixture:technical-history-not-historian-or-listening-acceptance", [], 1), default);
        var export = await w.ExportAsync(owner, u, p.Id, "Export", default);
        if (outputDirectory != null)
        {
            Directory.CreateDirectory(outputDirectory);
            foreach (var a in p.Artifacts) await File.WriteAllBytesAsync(Path.Combine(outputDirectory, a.Aspect + ".mp4"), (await w.PreviewAsync(owner, u, a.Id, default)).Bytes);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "captions.vtt"), export.Vtt);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "manifest.json"), System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        return export.Production;
    }
}
