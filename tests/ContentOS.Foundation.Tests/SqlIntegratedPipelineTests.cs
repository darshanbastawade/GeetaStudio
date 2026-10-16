using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace ContentOS.Foundation.Tests;

// This executable acceptance fixture uses the production module ports and real SQL/FFmpeg.
// Its synthetic event, reviews, prices, pictures and tones never qualify live acceptance.
[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlIntegratedPipelineTests
{
    private sealed class Text : ITextGenerationProvider
    {
        public string Key => "fixture.text.a";
        public Guid Character; public bool Youth; public int Submitted; public bool Transaction;
        public void Validate(AiProviderSettings settings) => new FixtureTextA().Validate(settings);
        public Task<AiProviderResult> GenerateAsync(AiProviderCall call, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Submitted++;
            var script = Youth
                ? new FactoryGeneratedScript("factory-script.v1", "Before sharing the claim", "A student organizer hears two accounts of a meeting. She checks the original notice before forwarding either account.", [new FactoryDialogue(Character, "Let us separate what we observed from what we assumed.")], "Compare the notice and ask a witness.", "What evidence would change your view?")
                : new FactoryGeneratedScript("factory-script.v1", "Whose turn is next?", "Two friends disagree at the playground. A visiting learner asks them to check the turn list together.", [new FactoryDialogue(Character, "Can we look at the list before choosing?")], "Read the list and invite a calm reply.", "What could you check first?");
            var json = JsonSerializer.Serialize(script, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return Task.FromResult(new AiProviderResult("Success", JsonSerializer.Serialize(new GeneratedText(json, call.SourceIds), new JsonSerializerOptions(JsonSerializerDefaults.Web)), new(200, 0, Youth ? 120 : 100), "fixture:p7-" + call.AttemptId));
        }
        public IAsyncEnumerable<AiStreamEvent> StreamAsync(AiProviderCall call, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Tone : ISpeechSynthesisProvider
    {
        public string Key => "media.speech.pipeline-fixture.v1"; public int Submitted;
        public MediaProviderCapabilities Capabilities => new("Speech", ["en-IN"], false, false, false, true, true, "OfflineFixture", ["TonesAreNotNarration", "NoLivePermission"]);
        public Task<MediaProviderResult> SubmitAsync(MediaProviderRequest r, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Submitted++;
            var seconds = Math.Max(.5, Math.Max(r.PromptOrText.Length / 23.0, r.PromptOrText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length / 3.9));
            return Task.FromResult(new MediaProviderResult("Completed", "fixture:p7-" + r.OperationId, WatchTestFixture.Voice(Math.Ceiling(seconds * 4) / 4), 0, "ControlledToneNotNarration"));
        }
        public Task<MediaProviderResult> ReconcileAsync(MediaProviderRequest r, string? resource, CancellationToken ct) => throw new InvalidOperationException("This fixture has no uncertain submission.");
    }
    private static async Task<FactoryBatchView> Generate(ServiceProvider root, TrendTestFixture.Fixture f, StartFactoryBatch request)
    {
        using var scope = root.CreateScope(); var s = scope.ServiceProvider; var factory = s.GetRequiredService<IDraftFactory>(); var batch = await factory.StartAsync(f.Owner, f.Universe, request, default);
        for (var n = 0; n < 100; n++)
        {
            using var worker = root.CreateScope(); var fresh = worker.ServiceProvider;
            await fresh.GetRequiredService<IDraftFactory>().ProcessNextAsync(default);
            await fresh.GetRequiredService<IWorkflowWorker>().ProcessNextAsync(default);
            batch = (await factory.BatchesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == batch.Id);
            if (batch.State == "Completed" && await s.GetRequiredService<CoreDbContext>().EditorialItems.AnyAsync(x => x.TenantId == f.Owner.TenantId && batch.Work.Select(w => w.ArtifactId).Contains(s.GetRequiredService<CoreDbContext>().FactoryArtifacts.Where(a => a.CoreContentId == x.ContentId).Select(a => (Guid?)a.Id).FirstOrDefault()))) return batch;
            Assert.NotEqual("CompletedWithIssues", batch.State);
        }
        throw new InvalidOperationException("Integrated factory did not settle.");
    }
    [SqlFact]
    public async Task Two_audiences_reuse_original_AI_and_media_through_private_manual_exports_and_current_rights()
    {
        var text = new Text(); var tone = new Tone();
        using var root = WatchTestFixture.Services(services =>
        {
            services.AddOwnedWorkflows(); services.RemoveAll<ITextGenerationProvider>();
            services.AddScoped<ITextGenerationProvider>(s => new ObservedText(text, s.GetRequiredService<CoreDbContext>()));
            services.AddSingleton<ITextGenerationProvider, FixtureTextB>(); services.AddSingleton<ISpeechSynthesisProvider>(tone);
            services.AddSingleton<ISocialCapabilityCatalog, SocialChannelProfiles>(); services.AddSocialPublishing();
            services.AddSingleton<ISocialPublisher>(s => new SocialFixturePublisher("social.fixture.a.v1", s.GetRequiredService<ISocialEffectLedger>()));
        });
        using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await TrendTestFixture.Setup(s, "prompt7-" + Guid.NewGuid(), video: true, generationFixture: true);
        var db = s.GetRequiredService<CoreDbContext>(); var trends = s.GetRequiredService<ITrendWorkbench>(); var factory = s.GetRequiredService<IDraftFactory>(); var registry = s.GetRequiredService<IUniverseRegistry>(); var media = s.GetRequiredService<IMediaLibrary>(); var watch = s.GetRequiredService<IWatchFactory>(); var social = s.GetRequiredService<ISocialPublishing>();
        var noMatch = await TrendTestFixture.Match(s, f, "How do I bake a pizza?"); Assert.Equal("NoSuitableGeetaConnection", noMatch.Outcome);
        var match = await TrendTestFixture.Match(s, f); var basis = await TrendTestFixture.Package(s, f, match);
        var basePlan = (await factory.PlansAsync(f.Owner, f.Universe, default)).Single(x => x.Id == basis.FactoryPlanId).Snapshot!.Rules;
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => trends.PrepareAsync(f.Owner, f.Universe, new(Guid.NewGuid(), noMatch.Id, "no-forced-match", 1, "OriginalFiction", "No supported analogy", "None", ["LocalPreview"], basePlan), default));
        var ai = s.GetRequiredService<IAiAdministration>(); var runtime = s.GetRequiredService<IWorkflowRuntime>();
        var profile = await ai.RegisterAsync(f.Owner, new(SqlAiTests.Settings(streaming: false)), default);
        await ai.BudgetAsync(f.Owner, new("ai.text", "USD", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), .2m), default);
        var directory = Path.Combine(ArchitectureTests.RepositoryRoot, ".local", "M25-2-prompt7", "sample"); Directory.CreateDirectory(directory);
        var packages = new List<SocialPackageView>(); var productions = new List<WatchProductionView>(); var outputs = new List<object>(); var bodies = new List<string>();
        foreach (var input in basePlan.Briefs)
        {
            var audience = input.Cast.AudienceId; var youth = audience == f.YouthAudience; text.Youth = youth; text.Character = youth ? f.YouthCharacter : f.ChildCharacter;
            var g = new RegistryTestFixture.Fixture(f.Owner, f.Universe, audience, f.Bible, f.Episode); var character = (await registry.CharactersAsync(f.Owner, f.Universe, default)).Single(x => x.CharacterId == text.Character); Assert.Equal("Guest", character.PersistenceScope);
            var image = await media.ImportAsync(f.Owner, f.Universe, MediaTestFixture.Upload(g, character, input.Key + "-placeholder-art", MediaFiles.ReferenceCard(character.Profile.Rules.VisualAnchor, [])), default); await MediaTestFixture.Review(s, g, image);
            var voiceProfile = await MediaTestFixture.Profile(s, g, tone.Key, "Speech", resource: "fixture:p7-" + input.Key); var output = MediaTestFixture.Upload(g, character, input.Key + "-tone", [], "Voice") with { ExpectedSha256 = "" };
            var seed = await media.GenerateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), voiceProfile, output, "Controlled tone, not narration.", [], [], 0), default); await MediaTestFixture.Drain(s);
            var voice = (await media.AssetsAsync(f.Owner, f.Universe, default)).SelectMany(x => x.Versions).Single(x => x.GenerationId == seed.Id); await MediaTestFixture.Review(s, g, voice);
            var vp = (await media.ProfilesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == voiceProfile); var package = await MediaTestFixture.Package(s, g, character, image, voice, input.Key + "-cast", vp.Configuration.Key, vp.Configuration.ResourceKey);
            var presentation = await media.BindPackageAsync(f.Owner, f.Universe, package.Id, Guid.NewGuid(), default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, presentation);
            var prompt = await runtime.RegisterPromptAsync(f.Owner, f.Universe, new("p7-" + input.Key, 1, "Original fiction. {{brief}} Audience {{audience}} Cast {{cast}} Lesson {{lesson}} Format {{format}}", new[] { "brief", "audience", "cast", "lesson", "format" }.Select(x => new PromptInput(x, 2000)).ToArray(), "en-IN", audience, RequireEvidence: true), default);
            var route = await ai.RouteAsync(f.Owner, new("draft", "p7-" + input.Key + ".v1", [profile], .1m, "USD", DeadlineSeconds: 20, PromptVersion: "p7-" + input.Key + ".v1"), default); await ai.ActivateAsync(f.Owner, route, default);
            var definition = await runtime.RegisterDefinitionAsync(f.Owner, f.Universe, new("p7-" + input.Key, 1, prompt, route, WorkflowRules.Sequence.Select(x => new WorkflowStepDefinition(x, x == "draft" ? "ai.generate.v1" : x == "retrieve" ? "evidence.retrieve.v1" : null)).ToArray(), .1m), default);
            var rules = basePlan with { OperationId = Guid.NewGuid(), Adapter = "ai-workflow.v1", WorkflowDefinitionId = definition, Briefs = [input with { Cast = input.Cast with { PinnedChoices = [new(character.CharacterId, "Lead", presentation)] } }] };
            var brief = await trends.PrepareAsync(f.Owner, f.Universe, new(Guid.NewGuid(), match.Id, "p7-" + input.Key, 1, "OriginalFiction", "An original analogy about verifying context, not an event reenactment.", "Gentle self-correction.", ["LocalPreview"], rules), default);
            await trends.ReviewStoryAsync(f.Owner, f.Universe, brief.Id, new(Guid.NewGuid(), brief.RowVersion, "Approve", "Fixture", "fixture:synthetic-not-human-acceptance"), default);
            var plan = (await factory.PlansAsync(f.Owner, f.Universe, default)).Single(x => x.Id == brief.FactoryPlanId); await factory.ReviewPlanAsync(f.Owner, f.Universe, plan.Id, new(Guid.NewGuid(), plan.RowVersion, true, "fixture:private-plan"), default);
            var request = new StartFactoryBatch(Guid.NewGuid(), "p7-" + input.Key, plan.Id, new(.1m, "USD", 12000, 900, 1)); var batch = await Generate(root, f, request);
            Assert.Equal(batch.Id, (await factory.StartAsync(f.Owner, f.Universe, request, default)).Id);
            var artifact = (await factory.ArtifactsAsync(f.Owner, f.Universe, default)).Single(x => x.Id == batch.Work.Single().ArtifactId); var generation = artifact.Snapshot!.Generation!;
            var run = await s.GetRequiredService<IAiTaskRouter>().InspectAsync(f.Owner, generation.AiRunId, default); Assert.Equal(run.Spent, batch.GenerationCost); Assert.Equal(run.Attempts.Sum(x => x.Cost), run.Spent); Assert.True(run.Spent > 0); Assert.Equal(0, run.Reserved); Assert.Single(run.Attempts);
            var fiction = await db.ClaimRevisions.AsNoTracking().SingleAsync(x => x.Id == generation.FictionClaimRevisionId); await s.GetRequiredService<IEvidenceEngine>().ReviewAsync(f.Owner, f.Universe, fiction.Id, new(Convert.ToBase64String(fiction.RowVersion), "Approved", "Attributed", "Supported", "fixture:generated-fiction-not-human-review"), default);
            var editor = s.GetRequiredService<IEditorialWorkbench>(); var item = await db.EditorialItems.AsNoTracking().SingleAsync(x => x.ContentId == artifact.CoreContentId); var packet = await editor.InspectAsync(f.Owner, f.Universe, item.Id, default); var context = new EditorialTestFixture.Fixture(f.Owner, f.Universe, audience, Guid.Empty, f.Bible, f.Episode);
            packet = await ApproveFixture(s, context, packet, artifact.Snapshot.Input.Lesson.Snapshot.Writing.Rules.Writing!.EligibleLifeStages); Assert.False(packet.PublicIntended); Assert.All(packet.Approvals, x => Assert.Equal("Fixture", x.Mode)); bodies.Add(packet.Current!.Body);
            var template = await watch.TemplateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "p7-" + input.Key, 1, new()), default); var layout = (await watch.TemplatesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == template); await watch.ReviewTemplateAsync(f.Owner, f.Universe, template, new(Guid.NewGuid(), layout.RowVersion, "Approve", "Fixture", "fixture:layout-not-human-quality", [], 1), default);
            var renderer = await watch.ProfileAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "p7-" + input.Key, 1, "watch.ffmpeg.v1", "local:p7-fixture", template, 0, "INR", "Fixture", "fixture:local-renderer"), default);
            var create = new CreateWatch(Guid.NewGuid(), "p7-" + input.Key, 1, packet.Id, renderer, [new("narrator", 0, packet.Current.Body.Length, character.CharacterId, presentation, image.Id, null, "fixture:tone-not-narration", VoiceGeneration: new(Guid.NewGuid(), voiceProfile, output, packet.Current.Body, [], [], 0))], ["Standard"], 0, 900, brief.Id);
            create = await watch.PlanNarrationAsync(f.Owner, f.Universe, create, default); var production = await watch.CreateAsync(f.Owner, f.Universe, create, default);
            for (var n = 0; n < 20; n++) { await watch.ProcessNextAsync(default); production = await watch.InspectAsync(f.Owner, f.Universe, production.Id, default); if (production.Stages.Single(x => x.Kind == "Voice").State == "Waiting") break; }
            for (var n = 0; n < 80 && await media.ProcessNextAsync("p7-fixture-worker", default); n++) { }
            var jobs = (await media.GenerationsAsync(f.Owner, f.Universe, default)).Where(x => x.Snapshot.Request.Admission?.ProductionId == production.Id).ToArray(); Assert.Equal(create.Scenes.Length, jobs.Length); Assert.All(jobs, x => Assert.Equal("Completed", x.State));
            foreach (var asset in (await media.AssetsAsync(f.Owner, f.Universe, default)).SelectMany(x => x.Versions).Where(x => jobs.Any(j => j.AcceptedVersionId == x.Id))) await MediaTestFixture.Review(s, g, asset);
            var accepted = production.Stages.Where(x => x.State == "Accepted").ToDictionary(x => x.Id, x => x.Attempts);
            var submitted = tone.Submitted; using var restart = root.CreateScope(); var fresh = restart.ServiceProvider; var resumed = fresh.GetRequiredService<IWatchFactory>();
            var wf = new WatchTestFixture.Fixture(context, character, packet, image, voice, presentation, template, renderer); production = await WatchTestFixture.Drain(fresh, wf, production.Id); Assert.Equal("Review", production.State); Assert.Equal(submitted, tone.Submitted); Assert.All(accepted, x => Assert.Equal(x.Value, production.Stages.Single(y => y.Id == x.Key).Attempts));
            Assert.Equal(production.Id, (await resumed.CreateAsync(f.Owner, f.Universe, create, default)).Id); Assert.Equal(submitted, tone.Submitted); Assert.Equal(packet.Current.Body, string.Concat(production.Timeline!.Captions.Select(x => x.Text))); Assert.All(f.NewsSources, id => Assert.Contains(id, production.Timeline.SourceSnapshotIds)); Assert.Equal(presentation, production.Timeline.Cast.Single().Presentation.Id);
            await resumed.ReviewAsync(f.Owner, f.Universe, production.Id, new(Guid.NewGuid(), production.RowVersion, "Approve", "Fixture", "fixture:private-technical-review-not-release", [], 1), default); var export = await resumed.ExportAsync(f.Owner, f.Universe, production.Id, "Export", default); await Assert.ThrowsAsync<EvidenceBlockedException>(() => resumed.ExportAsync(f.Owner, f.Universe, production.Id, "Publish", default));
            var account = await SocialTestFixture.Account(fresh, context, "YouTube", key: "p7-" + input.Key); var socialPackage = await SocialTestFixture.Package(fresh, context, packet, account, "p7-" + input.Key, "Video", [image.Id], production.Id, "Standard"); var channel = await fresh.GetRequiredService<ISocialPublishing>().ExportAsync(f.Owner, f.Universe, socialPackage.Id, default); Assert.Contains("NotPublished", channel.Status); Assert.Equal("Draft", channel.Publication!.State);
            var master = export.Production.Artifacts.Single(); var bytes = await resumed.PreviewAsync(f.Owner, f.Universe, master.Id, default); Assert.Equal(master.Sha256, MediaRules.Hash(bytes.Bytes));
            await File.WriteAllBytesAsync(Path.Combine(directory, input.Key + "-CONTROLLED-TONE-NOT-NARRATION.mp4"), bytes.Bytes); await File.WriteAllTextAsync(Path.Combine(directory, input.Key + ".vtt"), export.Vtt); await File.WriteAllTextAsync(Path.Combine(directory, input.Key + ".srt"), export.Srt);
            var evidence = new { fixtureOnly = true, audience = input.Key, audienceId = audience, teachingRationale = match.Rationale, teaching = match, analogy = brief.Snapshot!.Analogy, brief, plan, batch, artifact, aiRun = run, mediaJobs = jobs, watch = export, manualPackage = channel, costs = new { ai = new { currency = "USD", ledgerAmount = run.Spent, actualExternalCharge = 0, pricingIsSynthetic = true }, media = new { currency = production.Currency, ledgerAmount = production.CreationCost }, unknownCosts = jobs.Count(x => x.ActualCost == null) }, genuineHumanReview = false, liveConnected = false };
            await File.WriteAllTextAsync(Path.Combine(directory, input.Key + ".json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true })); outputs.Add(new { input.Key, audience, production.Id, master.Sha256, socialPackage = socialPackage.Id, run = run.Id, run.Spent }); packages.Add(socialPackage); productions.Add(production);
        }
        Assert.NotEqual(bodies[0], bodies[1]); Assert.Contains("playground", bodies[0]); Assert.Contains("forwarding", bodies[1]); Assert.Equal(2, text.Submitted); Assert.False(text.Transaction);
        // Move only this isolated fixture's clock across its expiry boundary; restore it to test withdrawal independently.
        db.ChangeTracker.Clear(); var cluster = await db.TrendClusters.SingleAsync(x => x.Id == f.Cluster); var expiry = cluster.ExpiresUtc; cluster.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        foreach (var p in packages) await Assert.ThrowsAsync<EvidenceBlockedException>(() => social.ExportAsync(f.Owner, f.Universe, p.Id, default));
        db.ChangeTracker.Clear(); cluster = await db.TrendClusters.SingleAsync(x => x.Id == f.Cluster); cluster.ExpiresUtc = expiry; await db.SaveChangesAsync();
        var sources = s.GetRequiredService<ISourceRegistry>(); var source = await db.SourceSnapshots.AsNoTracking().SingleAsync(x => x.Id == f.NewsSources[0]); var edition = await db.SourceEditions.AsNoTracking().SingleAsync(x => x.Id == source.EditionId); var work = await sources.InspectAsync(f.Owner, f.Universe, edition.WorkId, default);
        await sources.RevokeAsync(f.Owner, f.Universe, work.Work.Id, new(Guid.NewGuid(), null, "fixture:p7-withdraw-synthetic-event", work.Work.RowVersion), default);
        foreach (var p in packages) await Blocked(() => social.ExportAsync(f.Owner, f.Universe, p.Id, default));
        foreach (var p in productions) { await Blocked(() => watch.PreviewAsync(f.Owner, f.Universe, p.Artifacts.Single().Id, default)); Assert.Null((await watch.InspectAsync(f.Owner, f.Universe, p.Id, default)).Timeline); }
        Assert.Equal(2, text.Submitted); Assert.Empty(await db.SocialPublications.Where(x => x.TenantId == f.Owner.TenantId && x.State != "Draft").ToArrayAsync());
        await File.WriteAllTextAsync(Path.Combine(directory, "acceptance.json"), JsonSerializer.Serialize(new { fixtureOnly = true, outputs, noSuitableMatchBlocked = true, staleExportsBlocked = true, withdrawnExportsAndPreviewBlocked = true, generationSubmissions = text.Submitted, mediaSubmissions = tone.Submitted, acceptedStagesReused = true, genuineHumanReview = false, actualExternalCharge = 0, publicPosts = 0 }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static async Task<EditorialPacket> ApproveFixture(IServiceProvider s, EditorialTestFixture.Fixture f, EditorialPacket packet, string[] allowedLifeStages)
    {
        var q = s.GetRequiredService<IQualityEvaluation>(); var production = packet.Episodes.Single();
        var cast = production.Cast.Select(x => new EvaluationCastMember(x.Character.CharacterId, x.Character.Profile.Id, x.Role, x.Presentation?.Rules.VoiceAnchor ?? x.Character.Profile.Rules.VoiceAnchor, x.Character.Profile.Rules.LifeStage)).ToArray();
        var c = EvaluationFixtureCatalog.Create(f.Audience, cast: production.Brief).Cases.First() with { RequiredProperties = [], ForbiddenProperties = [], ExpectedTextIds = [], Cast = production.Brief, AllowedLifeStages = allowedLifeStages, VerifiedClaims = [TrendTestFixture.Fact] };
        var dataset = await q.RegisterDatasetAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "p7-review-" + Guid.NewGuid(), 1, "Controlled body/cast evaluation; genuine human review pending.", [c]), default);
        var candidate = await q.RegisterCandidateAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "p7-recorded-" + Guid.NewGuid(), 1, new("recorded.v1", "script.v1", "none.v1", "p7.v1", "fixture.v1")), default);
        var report = await q.RunAsync(f.Owner, f.Universe, new(Guid.NewGuid(), dataset.Id, candidate.Id, "Development", RecordedOutputs: [new(c.Key, new(packet.Current!.Body, "en-IN", [], false, cast, [TrendTestFixture.Fact], null, [], 0, "INR"))]), default);
        Assert.DoesNotContain(report.Results.Single().Deterministic, x => x.Severity == "Critical");
        var editor = s.GetRequiredService<IEditorialWorkbench>();
        packet = await editor.ActAsync(f.Owner, f.Universe, packet.Id, EditorialTestFixture.Action(packet, "Evaluate", new(report.Id, c.Key)), default);
        packet = await editor.ActAsync(f.Owner, f.Universe, packet.Id, EditorialTestFixture.Action(packet, "Review"), default);
        return await editor.ActAsync(f.Owner, f.Universe, packet.Id, EditorialTestFixture.Action(packet, "Approve"), default);
    }
    private static async Task Blocked(Func<Task> action) => Assert.True(await Record.ExceptionAsync(action) is EvidenceBlockedException or RightsBlockedException);
    private sealed class ObservedText(Text inner, CoreDbContext db) : ITextGenerationProvider
    {
        public string Key => inner.Key;
        public void Validate(AiProviderSettings settings) => inner.Validate(settings);
        public Task<AiProviderResult> GenerateAsync(AiProviderCall call, CancellationToken ct) { inner.Transaction |= db.Database.CurrentTransaction != null; return inner.GenerateAsync(call, ct); }
        public IAsyncEnumerable<AiStreamEvent> StreamAsync(AiProviderCall call, CancellationToken ct) => inner.StreamAsync(call, ct);
    }
}
