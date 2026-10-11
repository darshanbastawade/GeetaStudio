using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlRegistryTests
{
    [SqlFact]
    public async Task New_guest_is_available_to_pinned_draft_then_activated_promoted_and_snapshotted_without_rebuild()
    {
        using var root = RegistryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var r = s.GetRequiredService<IUniverseRegistry>();
        await RegistryTestFixture.Character(s, f, "learner");
        var brief = RegistryTestFixture.Brief(f, ["Lead", "Mentor"]); var missing = await r.CastAsync(f.Owner, f.Universe, brief, default); Assert.Equal("Mentor", Assert.Single(missing.MissingRoles)); Assert.Single(missing.GuestProposals);
        var coach = await RegistryTestFixture.Character(s, f, "new-coach", "Mentor", active: false);
        var pinned = brief with { PinnedChoices = [new(coach.CharacterId, "Mentor")] }; Assert.Equal(2, (await r.CastAsync(f.Owner, f.Universe, pinned, default)).Selected.Length);
        var draft = await RegistryTestFixture.Story(s, f, pinned); Assert.Equal("Draft", draft.ReviewStatus); Assert.Contains(draft.Rules.Cast, c => c.Character.CharacterId == coach.CharacterId);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => RegistryTestFixture.Review(s, f.Owner, f.Universe, draft.Id));
        await RegistryTestFixture.Review(s, f.Owner, f.Universe, coach.Profile.Id); await r.ControlCharacterAsync(f.Owner, f.Universe, coach.CharacterId, new(Guid.NewGuid(), coach.RowVersion, "Activate", "fixture:coach"), default);
        var story = await RegistryTestFixture.Story(s, f, brief); await RegistryTestFixture.Review(s, f.Owner, f.Universe, story.Id);
        var current = (await r.CharactersAsync(f.Owner, f.Universe, default)).Single(c => c.CharacterId == coach.CharacterId);
        await r.ControlCharacterAsync(f.Owner, f.Universe, coach.CharacterId, new(Guid.NewGuid(), current.RowVersion, "PromoteGuest", "fixture:promotion"), default);
        using var restart = root.CreateScope(); var registry = restart.ServiceProvider.GetRequiredService<IUniverseRegistry>();
        Assert.Equal("Recurring", (await registry.CharactersAsync(f.Owner, f.Universe, default)).Single(c => c.CharacterId == coach.CharacterId).PersistenceScope);
        var old = (await registry.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == story.Id); Assert.Equal("Guest", old.Rules.Cast.Single(c => c.Character.CharacterId == coach.CharacterId).Character.PersistenceScope);
        Assert.Contains("not scripture", old.Rules.FictionLabel);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => registry.ExportAsync(f.Owner, f.Universe, story.Id, default));
    }
    [SqlFact]
    public async Task Edits_aliases_distinct_identity_and_review_freezing_preserve_completed_episode_traits()
    {
        using var root = RegistryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var r = s.GetRequiredService<IUniverseRegistry>();
        var c = await RegistryTestFixture.Character(s, f, "learner"); var story = await RegistryTestFixture.Story(s, f);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => r.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), c.Key, c.PersistenceScope, c.Profile.Rules with { LifeStage = "Adult" }, c.CharacterId, c.RowVersion), default));
        var edit = await r.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), c.Key, c.PersistenceScope, c.Profile.Rules with { Traits = ["reflective"], Aliases = ["Learner alias"] }, c.CharacterId, c.RowVersion), default);
        Assert.Equal(2, edit.Profile.Version); Assert.Equal("Draft", edit.Status);
        Assert.Equal("curious", (await r.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == story.Id).Rules.Cast[0].Character.Profile.Rules.Traits[0]);
        var copy = await r.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "distinct", "Guest", edit.Profile.Rules with { Name = "Distinct adult", LifeStage = "Adult", Aliases = [], VisualAnchor = "distinct-visual", VoiceAnchor = "distinct-voice" }, DistinctFromCharacterId: c.CharacterId), default);
        Assert.NotEqual(c.CharacterId, copy.CharacterId); Assert.Equal(c.CharacterId, copy.DistinctFromCharacterId);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => r.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "alias-collision", "Guest", copy.Profile.Rules with { Name = "Other", Aliases = ["Learner alias"] }), default));
        var db = s.GetRequiredService<CoreDbContext>(); db.ChangeTracker.Clear(); var immutable = await db.RegistryRevisions.SingleAsync(x => x.Id == c.Profile.Id); immutable.RulesJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear(); immutable = await db.RegistryRevisions.SingleAsync(x => x.Id == c.Profile.Id); db.Remove(immutable); await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [SqlFact]
    public async Task Episode_only_scope_and_retirement_block_new_selection_while_preserving_history()
    {
        using var root = RegistryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var r = s.GetRequiredService<IUniverseRegistry>();
        var c = await RegistryTestFixture.Character(s, f, "one-episode", scope: "EpisodeOnly"); var story = await RegistryTestFixture.Story(s, f);
        var other = await r.RegisterEpisodeAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "other"), default); Assert.Single((await r.CastAsync(f.Owner, f.Universe, RegistryTestFixture.Brief(f) with { EpisodeId = other }, default)).MissingRoles);
        await r.ControlCharacterAsync(f.Owner, f.Universe, c.CharacterId, new(Guid.NewGuid(), c.RowVersion, "Retire", "fixture:retired"), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => r.CastAsync(f.Owner, f.Universe, RegistryTestFixture.Brief(f) with { PinnedChoices = [new(c.CharacterId, "Lead")] }, default));
        Assert.Single((await r.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == story.Id).Rules.Cast);
    }
    [SqlFact]
    public async Task Humor_escalation_and_policy_revision_apply_to_new_stories_without_rewriting_old_snapshots()
    {
        using var root = RegistryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var r = s.GetRequiredService<IUniverseRegistry>();
        var c = await RegistryTestFixture.Character(s, f, "learner"); var episode = (await r.EpisodesAsync(f.Owner, f.Universe, default)).Single();
        var req = new ComposeRegistryEpisode(Guid.NewGuid(), episode.RowVersion, RegistryTestFixture.Brief(f), "Satire", [new(c.CharacterId, "FictionalDialogue", "I choose to pause.", ["curious"], [], true)], [], ReferencesCurrentEvents: true);
        var story = await r.ComposeAsync(f.Owner, f.Universe, req, default); Assert.True(story.Rules.EditorialEscalationRequired); Assert.NotEmpty(story.Rules.SatireLabel);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => RegistryTestFixture.Review(s, f.Owner, f.Universe, story.Id));
        await RegistryTestFixture.Review(s, f.Owner, f.Universe, story.Id, "fixture:separate-escalation");
        var bible = (await r.BiblesAsync(f.Owner, f.Universe, default)).Single(); var next = await r.RegisterBibleAsync(f.Owner, f.Universe, new(Guid.NewGuid(), bible.Key, 2, bible.Rules with { HumorEnabled = false }), default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, next);
        episode = (await r.EpisodesAsync(f.Owner, f.Universe, default)).Single();
        await Assert.ThrowsAsync<RuntimeValidationException>(() => r.ComposeAsync(f.Owner, f.Universe, req with { OperationId = Guid.NewGuid(), ExpectedRowVersion = episode.RowVersion, Brief = req.Brief with { BibleRevisionId = next } }, default));
        Assert.True((await r.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == story.Id).Rules.Bible.Rules.HumorEnabled);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => r.ComposeAsync(f.Owner, f.Universe, req with { OperationId = Guid.NewGuid(), ExpectedRowVersion = episode.RowVersion, Beats = [req.Beats[0] with { HumorTreatments = ["RealSuffering"] }] }, default));
    }
    [SqlFact]
    public async Task Presentation_identity_budget_real_people_and_media_readiness_are_independent_gates()
    {
        using var root = RegistryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var r = s.GetRequiredService<IUniverseRegistry>();
        var c = await RegistryTestFixture.Character(s, f, "learner"); var p = new RegisterRegistryPresentation(Guid.NewGuid(), "guide", 1, new(c.Profile.Id, f.Audience, "en-IN", "Coach", c.Profile.Rules.VisualAnchor, c.Profile.Rules.VoiceAnchor, "Fixture pronunciation", null, null, 5, "INR"));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => r.RegisterPresentationAsync(f.Owner, f.Universe, p with { Rules = p.Rules with { VoiceAnchor = "rebound" } }, default));
        var id = await r.RegisterPresentationAsync(f.Owner, f.Universe, p, default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, id);
        Assert.Single((await r.CastAsync(f.Owner, f.Universe, RegistryTestFixture.Brief(f), default)).MissingRoles);
        var brief = RegistryTestFixture.Brief(f) with { Budget = new(4, 5, "INR") }; Assert.Single((await r.CastAsync(f.Owner, f.Universe, brief, default)).Selected);
        Assert.Single((await r.CastAsync(f.Owner, f.Universe, brief with { Budget = new(4, 5, "INR", true), Mode = "Production" }, default)).MissingRoles);
        var person = await r.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "familiar-person", "Guest", RegistryTestFixture.Rules(f, "Familiar name") with { FictionalStatus = "RealPerson" }), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => RegistryTestFixture.Review(s, f.Owner, f.Universe, person.Profile.Id));
        await Assert.ThrowsAsync<CoreAccessException>(() => r.RegisterPresentationAsync(f.Owner, f.Universe, p with { OperationId = Guid.NewGuid(), Rules = p.Rules with { VisualAssetReferenceId = Guid.NewGuid() } }, default));
    }
    [SqlFact]
    public async Task Relationships_are_scoped_reviewed_revisions_and_episode_pins_do_not_follow_edits()
    {
        using var root = RegistryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var r = s.GetRequiredService<IUniverseRegistry>();
        var lead = await RegistryTestFixture.Character(s, f, "learner"); var mentor = await RegistryTestFixture.Character(s, f, "coach", "Mentor");
        var relation = new RegisterRegistryRelationship(Guid.NewGuid(), "coach-learner", 1, new(mentor.CharacterId, lead.CharacterId, "Coach", "Original teamwork", "Learning trust"));
        var id = await r.RegisterRelationshipAsync(f.Owner, f.Universe, relation, default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, id);
        var episode = (await r.EpisodesAsync(f.Owner, f.Universe, default)).Single(); var brief = RegistryTestFixture.Brief(f, ["Lead", "Mentor"]); var plan = await r.CastAsync(f.Owner, f.Universe, brief, default);
        var story = await r.ComposeAsync(f.Owner, f.Universe, new(Guid.NewGuid(), episode.RowVersion, brief, "None", plan.Selected.Select(c => new RegistryStoryBeat(c.Character.CharacterId, "FictionalDialogue", "I choose to listen.", ["curious"], [], c.Role == "Lead")).ToArray(), [id]), default);
        await r.RegisterRelationshipAsync(f.Owner, f.Universe, relation with { OperationId = Guid.NewGuid(), Version = 2, Rules = relation.Rules with { ArcState = "A later explicitly proposed arc" } }, default);
        Assert.Equal("Learning trust", (await r.StoriesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == story.Id).Rules.Relationships.Single().Rules.ArcState);
        await Assert.ThrowsAsync<CoreAccessException>(() => r.RegisterRelationshipAsync(f.Owner, f.Universe, relation with { OperationId = Guid.NewGuid(), Key = "foreign", Rules = relation.Rules with { ToCharacterId = Guid.NewGuid() } }, default));
    }
    [SqlFact]
    public async Task Concurrent_replay_stale_review_and_windows_staff_scope_header_are_enforced()
    {
        using var root = RegistryTestFixture.Services(); using var setup = root.CreateScope(); var s = setup.ServiceProvider; var f = await RegistryTestFixture.Setup(s);
        var req = new RegisterRegistryCharacter(Guid.NewGuid(), "concurrent", "Guest", RegistryTestFixture.Rules(f));
        using var a = root.CreateScope(); using var b = root.CreateScope(); var results = await Task.WhenAll(a.ServiceProvider.GetRequiredService<IUniverseRegistry>().RegisterCharacterAsync(f.Owner, f.Universe, req, default), b.ServiceProvider.GetRequiredService<IUniverseRegistry>().RegisterCharacterAsync(f.Owner, f.Universe, req, default)); Assert.Equal(results[0].CharacterId, results[1].CharacterId);
        var r = s.GetRequiredService<IUniverseRegistry>(); await Assert.ThrowsAsync<RuntimeConflictException>(() => r.RegisterCharacterAsync(f.Owner, f.Universe, req with { Rules = req.Rules with { Name = "Different" } }, default));
        await Assert.ThrowsAsync<CoreConcurrencyException>(() => r.ReviewAsync(f.Owner, f.Universe, results[0].Profile.Id, new(Guid.NewGuid(), "stale", true, "fixture:stale"), default));
        await Assert.ThrowsAsync<CoreAccessException>(() => r.RegisterCharacterAsync(new(f.Owner.TenantId, "m10-reader"), f.Universe, req with { OperationId = Guid.NewGuid() }, default));
        using var factory = new WebApplicationFactory<GeetaStudio.Api.ApiAssemblyMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Runtime:Enabled"] = "true",
                ["ConnectionStrings:ContentOS"] = LessonTestFixture.Connection,
                ["Staff:Administrator:0"] = "integration-test-Administrator",
                ["Staff:Reader:0"] = "integration-test-Reader",
                ["Workflows:WorkerEnabled"] = "false",
                ["Knowledge:IndexWorkerEnabled"] = "false"
            })); builder.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = factory.CreateClient(); var path = $"/api/v1/registry/tenants/{f.Owner.TenantId}/universes/{f.Universe}/characters";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode); client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, req)).StatusCode); client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/registry/tenants/{Guid.NewGuid()}/universes/{f.Universe}/characters")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader"); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, req)).StatusCode);
    }
    [SqlFact]
    public async Task Accepted_seeds_keep_ids_alias_and_same_identity_presentations_across_configured_universes()
    {
        using var root = RegistryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var db = s.GetRequiredService<CoreDbContext>(); var f = await RegistryTestFixture.Setup(s); var r = s.GetRequiredService<IUniverseRegistry>();
        var data = JsonSerializer.Deserialize<RegistrySeedDocument[]>(await File.ReadAllTextAsync(Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M10-bibles.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Single(x => x.UniverseKey == "geeta");
        var original = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(ArchitectureTests.RepositoryRoot, "docs", "fixtures", "M02-universes.json"))).RootElement.GetProperty("universes")[0];
        var universe = await db.Universes.SingleAsync(x => x.Id == f.Universe); universe.Key = "geeta";
        foreach (var key in new[] { "teens", "young-adults", "adults" }) db.Audiences.Add(new() { Id = Guid.NewGuid(), UniverseId = f.Universe, Key = key, Name = key, MinAge = 15, MaxAge = 120 });
        var ids = new List<Guid>(); foreach (var c in original.GetProperty("characters").EnumerateArray())
        {
            var id = Guid.NewGuid(); ids.Add(id); db.Characters.Add(new() { Id = id, UniverseId = f.Universe, Key = c.GetProperty("key").GetString()!, PersistenceScope = "Core" });
            db.CharacterRevisions.Add(new() { Id = Guid.NewGuid(), CharacterId = id, Version = 1, DisplayName = c.GetProperty("displayName").GetString()!, LifeStage = c.GetProperty("lifeStage").GetString()!, Biography = c.GetProperty("biography").GetString()! });
            foreach (var alias in c.GetProperty("aliases").EnumerateArray()) db.Aliases.Add(new() { UniverseId = f.Universe, CharacterId = id, Name = alias.GetString()! });
        }
        await db.SaveChangesAsync();
        await r.ProposeDefaultsAsync(f.Owner, f.Universe, data, default); await r.ProposeDefaultsAsync(f.Owner, f.Universe, data, default);
        var characters = await r.CharactersAsync(f.Owner, f.Universe, default); Assert.Equal(7, characters.Count); Assert.Equal(ids.Order(), characters.Select(c => c.CharacterId).Order());
        Assert.Contains("Neeraja", characters.Single(c => c.Key == "neerja").Profile.Rules.Aliases);
        var profiles = (await r.PresentationsAsync(f.Owner, f.Universe, default)).Where(p => p.Key.StartsWith("shakuni-")).ToArray(); Assert.Equal(3, profiles.Length); Assert.Single(profiles.Select(p => p.Rules.ProfileRevisionId).Distinct()); Assert.Single(profiles.Select(p => p.Rules.VoiceAnchor).Distinct()); Assert.All(profiles, p => Assert.Equal("Draft", p.ReviewStatus));
        Assert.All(characters, c => Assert.Equal("Draft", c.Status));
        var alternate = await r.RegisterBibleAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "alternate", 1, data.Bible with { Title = "Another editable fictional setting" }), default); await RegistryTestFixture.Review(s, f.Owner, f.Universe, alternate);
        Assert.Contains((await r.BiblesAsync(f.Owner, f.Universe, default)), b => b.Rules.Title == "Another editable fictional setting");
        var other = await RegistryTestFixture.Setup(s, "m10-alternate-owner");
        var otherCharacter = await RegistryTestFixture.Character(s, other, "other-universe-lead");
        var otherBible = await r.RegisterBibleAsync(other.Owner, other.Universe, new(Guid.NewGuid(), "other-setting", 1, RegistryTestFixture.Bible(other.Audience, "A different configured universe")), default);
        await RegistryTestFixture.Review(s, other.Owner, other.Universe, otherBible);
        var subject = new Subject { Id = Guid.NewGuid(), UniverseId = other.Universe, Key = "original-guide", Type = "Theme", Name = "Original fixture media guides" };
        db.Subjects.Add(subject);
        var content = new ContentItem { Id = Guid.NewGuid(), TenantId = other.Owner.TenantId, UniverseId = other.Universe, SubjectId = subject.Id, AudienceId = other.Audience, LocaleId = "en-IN", CurrentVersion = 1 };
        db.Contents.Add(content);
        var revision = new ContentRevision { Id = Guid.NewGuid(), ContentId = content.Id, Version = 1, OperationId = Guid.NewGuid(), Title = "Original fixture guide", Body = "Original fictional visual and voice guide; not real media qualification.", Kind = "Fiction", RightsStatus = "Owned", Provenance = "fixture:repository-original-guide" };
        db.Revisions.Add(revision); await db.SaveChangesAsync();
        var store = s.GetRequiredService<IAssetStore>();
        var visual = await store.PutAsync(System.Text.Encoding.UTF8.GetBytes("Original fixture visual guide; no real-person likeness."), other.Owner.Actor, default);
        var voice = await store.PutAsync(System.Text.Encoding.UTF8.GetBytes("Original fixture voice guide; pronunciation quality unqualified."), other.Owner.Actor, default);
        var visualRef = new CoreAssetReference { Id = Guid.NewGuid(), TenantId = other.Owner.TenantId, ContentRevisionId = revision.Id, StorageAssetId = visual, RightsStatus = "Owned" };
        var voiceRef = new CoreAssetReference { Id = Guid.NewGuid(), TenantId = other.Owner.TenantId, ContentRevisionId = revision.Id, StorageAssetId = voice, RightsStatus = "Owned" };
        db.Assets.AddRange(visualRef, voiceRef); await db.SaveChangesAsync();
        var guide = await r.RegisterPresentationAsync(other.Owner, other.Universe, new(Guid.NewGuid(), "other-guide", 1,
            new(otherCharacter.Profile.Id, other.Audience, "en-IN", "OriginalGuide", otherCharacter.Profile.Rules.VisualAnchor, otherCharacter.Profile.Rules.VoiceAnchor, "Fixture pronunciation guide", visualRef.Id, voiceRef.Id, 0, "INR")), default);
        await RegistryTestFixture.Review(s, other.Owner, other.Universe, guide);
        var production = RegistryTestFixture.Brief(other) with { BibleRevisionId = otherBible, Mode = "Production", Budget = new(4, 0, "INR", true) };
        Assert.Equal(guide, Assert.Single((await r.CastAsync(other.Owner, other.Universe, production, default)).Selected).Presentation!.Id);
        var otherStory = await RegistryTestFixture.Story(s, other, RegistryTestFixture.Brief(other) with { BibleRevisionId = otherBible });
        Assert.Equal("A different configured universe", otherStory.Rules.Bible.Rules.Title);
        Assert.DoesNotContain(otherStory.Rules.Cast, c => ids.Contains(c.Character.CharacterId));
        Assert.Equal(visualRef.Id, otherStory.Rules.Cast[0].Presentation!.Rules.VisualAssetReferenceId);
        db.ChangeTracker.Clear(); var revoked = await db.Assets.SingleAsync(x => x.Id == visualRef.Id); revoked.RightsStatus = "Unverified"; await db.SaveChangesAsync();
        Assert.Single((await r.CastAsync(other.Owner, other.Universe, production, default)).MissingRoles);
    }
    [SqlFact]
    public async Task M09_release_gate_participates_in_parent_transaction_without_nested_begin_or_premature_commit()
    {
        using var root = LessonTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await LessonTestFixture.Setup(s); var lessons = s.GetRequiredService<ILessonEngine>();
        var v = await lessons.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f), default); var db = s.GetRequiredService<CoreDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(); await Assert.ThrowsAsync<RuntimeValidationException>(() => lessons.ExportAsync(f.Owner, f.Universe, v.Id, default));
        Assert.Same(tx, db.Database.CurrentTransaction); await tx.RollbackAsync();
    }
    [SqlFact]
    public async Task Casting_finds_feasible_distinct_roles_and_total_budget_instead_of_greedy_starvation()
    {
        using var root = RegistryTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await RegistryTestFixture.Setup(s); var r = s.GetRequiredService<IUniverseRegistry>();
        var flexible = await r.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "a-flexible", "Guest", RegistryTestFixture.Rules(f, "Flexible") with { Roles = ["Lead", "Mentor"] }), default);
        await RegistryTestFixture.Review(s, f.Owner, f.Universe, flexible.Profile.Id);
        await r.ControlCharacterAsync(f.Owner, f.Universe, flexible.CharacterId, new(Guid.NewGuid(), flexible.RowVersion, "Activate", "fixture:eligible"), default);
        var lead = await RegistryTestFixture.Character(s, f, "z-only-lead");
        var plan = await r.CastAsync(f.Owner, f.Universe, RegistryTestFixture.Brief(f, ["Lead", "Mentor"]), default);
        Assert.Empty(plan.MissingRoles); Assert.Equal(flexible.CharacterId, plan.Selected.Single(c => c.Role == "Mentor").Character.CharacterId);
        Assert.Equal(lead.CharacterId, plan.Selected.Single(c => c.Role == "Lead").Character.CharacterId);
        var guide = await r.RegisterPresentationAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "cost", 1, new(flexible.Profile.Id, f.Audience, "en-IN", "Default", flexible.Profile.Rules.VisualAnchor, flexible.Profile.Rules.VoiceAnchor, "Fixture", null, null, 1, "INR")), default);
        await RegistryTestFixture.Review(s, f.Owner, f.Universe, guide);
        var constrained = await r.CastAsync(f.Owner, f.Universe, RegistryTestFixture.Brief(f, ["Lead", "Mentor"]), default);
        Assert.Contains("Mentor", constrained.MissingRoles); Assert.All(constrained.Selected, c => Assert.Equal(0, c.EstimatedCost));
    }
}
