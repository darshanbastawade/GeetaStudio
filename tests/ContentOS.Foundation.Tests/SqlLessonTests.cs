using System.Net;
using System.Net.Http.Json;
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
public sealed class SqlLessonTests
{
    [SqlFact]
    public async Task Shared_verse_produces_distinct_audience_variants_with_pinned_rules_and_linked_revisions()
    {
        using var root = LessonTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await LessonTestFixture.Setup(s); var e = s.GetRequiredService<ILessonEngine>();
        var request = LessonTestFixture.Request(f); var child = await e.AdaptAsync(f.Owner, f.Universe, request, default);
        var youth = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f, "youth", "writing-15-18"), default);
        var adult = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f, "adult", "writing-adult"), default);
        Assert.NotEqual(child.Sections!.Example, youth.Sections!.Example); Assert.NotEqual(youth.Sections.Example, adult.Sections!.Example);
        Assert.Equal(child.Sections.SourceAnchor, youth.Sections.SourceAnchor); Assert.Equal(child.Sections.Interpretation, youth.Sections.Interpretation);
        Assert.Equal(child.Snapshot.Bundle.Sanskrit, youth.Snapshot.Bundle.Sanskrit); Assert.NotEmpty(child.Snapshot.Bundle.Citations); Assert.Equal(child.BundleId, youth.BundleId);
        Assert.Equal(child.Id, (await e.AdaptAsync(f.Owner, f.Universe, request, default)).Id);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => e.AdaptAsync(f.Owner, f.Universe, request with { AudienceProfileId = youth.Snapshot.Audience.Id }, default));
        var updated = child.Snapshot.Audience.Rules with { OperationId = Guid.NewGuid(), Version = 2, Audience = child.Snapshot.Audience.Rules.Audience! with { Tone = "Quiet encouragement", Example = "Another child-safe context example." } };
        await e.RegisterProfileAsync(f.Owner, f.Universe, updated, default);
        using var restart = root.CreateScope();
        var old = await restart.ServiceProvider.GetRequiredService<ILessonEngine>().InspectAsync(f.Owner, f.Universe, child.Id, default);
        Assert.Equal(1, old.Snapshot.Audience.Rules.Version); Assert.Equal(child.Sections.Example, old.Sections!.Example);
        var linked = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f) with { ParentVariantId = child.Id }, default);
        Assert.Equal(2, linked.Version); Assert.Equal(child.Id, linked.ParentVariantId); Assert.Equal("Draft", linked.ReviewStatus);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => e.ExportAsync(f.Owner, f.Universe, child.Id, default));
    }
    [SqlFact]
    public async Task Draft_defaults_are_repeatable_and_never_claim_human_review_or_locale_support()
    {
        using var root = LessonTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await LessonTestFixture.Setup(s); var e = s.GetRequiredService<ILessonEngine>();
        var before = await e.ProfilesAsync(f.Owner, f.Universe, default); var again = await e.ProposeDefaultsAsync(f.Owner, f.Universe, default); Assert.Equal(before.Select(x => x.Id), again.Select(x => x.Id));
        var draft = before.Single(x => x.Rules.Key == "children").Rules with { OperationId = Guid.NewGuid(), Version = 2 };
        var id = await e.RegisterProfileAsync(f.Owner, f.Universe, draft, default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f) with { AudienceProfileId = id }, default));
        await Assert.ThrowsAsync<RuntimeValidationException>(() => e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f, locale: "mr-IN"), default));
        await Assert.ThrowsAsync<CoreAccessException>(() => e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f) with { LocaleProfileId = Guid.NewGuid() }, default));
        var newLocale = before.Single(x => x.Rules.Key == "mr-IN").Rules with { OperationId = Guid.NewGuid(), Key = "Tamil proposed", Locale = before.Single(x => x.Rules.Key == "mr-IN").Rules.Locale! with { Locale = "ta-IN" } };
        var localeId = await e.RegisterProfileAsync(f.Owner, f.Universe, newLocale, default);
        Assert.True(await s.GetRequiredService<CoreDbContext>().Locales.AnyAsync(x => x.Id == "ta-IN"));
        Assert.Equal("Draft", (await e.ProfilesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == localeId).ReviewStatus);
        var db = s.GetRequiredService<CoreDbContext>(); var row = await db.LessonProfiles.SingleAsync(x => x.Id == id); row.RulesJson = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [SqlFact]
    public async Task Reviewed_memory_preserves_anchor_names_and_concepts_and_route_switch_keeps_old_binding()
    {
        using var root = LessonTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await LessonTestFixture.Setup(s); var e = s.GetRequiredService<ILessonEngine>();
        var en = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f), default);
        var target = en.Sections! with { Situation = "Fixture only: संदर्भ समजून घ्या.", Interpretation = "Fixture only: निर्णय घेण्याआधी संदर्भ तपासा. हे मत आहे.", Example = "Fixture only: मित्रांच्या खेळाचा संदर्भ पाहा.", PracticalAction = "Fixture only: थांबा आणि संदर्भ तपासा.", Reflection = "Fixture only: संदर्भ कसा बदलतो?" };
        var req = new RegisterLessonMemory(Guid.NewGuid(), f.Profiles.Single(x => x.Rules.Key == "mr-IN").Id, f.Bundle, en.Sections!, target, "Fixture only; native-speaker meaning/pronunciation review has NOT occurred.");
        await Assert.ThrowsAsync<RuntimeValidationException>(() => e.RegisterMemoryAsync(f.Owner, f.Universe, req with { Target = target with { SourceAnchor = "changed scripture" } }, default));
        var id = await e.RegisterMemoryAsync(f.Owner, f.Universe, req, default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f, locale: "mr-IN"), default));
        var memory = (await e.MemoryAsync(f.Owner, f.Universe, default)).Single(x => x.Id == id);
        await e.ReviewMemoryAsync(f.Owner, f.Universe, id, new(Guid.NewGuid(), memory.RowVersion, true, "fixture:memory-contract-only"), default);
        var first = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f, locale: "mr-IN"), default);
        var second = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f, locale: "mr-IN", route: "translation-b"), default);
        Assert.Equal(en.Sections!.SourceAnchor, first.Sections!.SourceAnchor); Assert.Equal(target.Interpretation, first.Sections.Interpretation); Assert.Equal(id, first.Snapshot.TranslationMemoryId);
        Assert.Equal("reviewed-memory.a.v1", first.Snapshot.Route.Rules.Route!.Adapter); Assert.Equal("reviewed-memory.b.v1", second.Snapshot.Route.Rules.Route!.Adapter);
        Assert.Equal("reviewed-memory.a.v1", (await e.InspectAsync(f.Owner, f.Universe, first.Id, default)).Snapshot.Route.Rules.Route!.Adapter);
        await e.ReviewVariantAsync(f.Owner, f.Universe, first.Id, new(Guid.NewGuid(), first.RowVersion, true, "fixture:local-contract-check-only"), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => e.ExportAsync(f.Owner, f.Universe, first.Id, default));
    }
    [SqlFact]
    public async Task Newly_activated_eligible_guest_is_cast_without_rebuild_and_never_reaged()
    {
        using var root = LessonTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await LessonTestFixture.Setup(s); var e = s.GetRequiredService<ILessonEngine>();
        var before = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f, "youth", "writing-15-18"), default); Assert.Empty(before.Snapshot.Cast);
        var id = await e.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "new-peer", "New fictional peer", "Teen", "A reviewed fixture teenager; life stage remains Teen.", "fixture:identity-and-suitability"), default);
        var after = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f, "youth", "writing-15-18"), default);
        Assert.Equal(id, Assert.Single(after.Snapshot.Cast).CharacterId); Assert.Equal("Teen", after.Snapshot.Cast.Single().LifeStage);
        var youngAdult = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f, "youth", "writing-19-23"), default); Assert.Empty(youngAdult.Snapshot.Cast);
        Assert.Empty((await e.InspectAsync(f.Owner, f.Universe, before.Id, default)).Snapshot.Cast);
        await Assert.ThrowsAsync<RuntimeConflictException>(() => e.RegisterCharacterAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "new-peer", "Reaged peer", "Adult", "Cannot silently change canonical life stage.", "fixture:invalid"), default));
    }
    [SqlFact]
    public async Task Unsafe_examples_and_stale_review_versions_fail_before_accepting_a_variant()
    {
        using var root = LessonTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await LessonTestFixture.Setup(s); var e = s.GetRequiredService<ILessonEngine>();
        var child = f.Profiles.Single(x => x.Rules.Key == "children");
        var bad = child.Rules with { OperationId = Guid.NewGuid(), Version = 2, Audience = child.Rules.Audience! with { Example = "Try gambling as an example." } };
        var id = await e.RegisterProfileAsync(f.Owner, f.Universe, bad, default); var p = (await e.ProfilesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == id);
        var review = new LessonReview(Guid.NewGuid(), p.RowVersion, true, "fixture:policy-still-authoritative");
        await e.ReviewProfileAsync(f.Owner, f.Universe, id, review, default); await e.ReviewProfileAsync(f.Owner, f.Universe, id, review, default);
        var w = f.Profiles.Single(x => x.Rules.Key == "writing-children").Rules with { OperationId = Guid.NewGuid(), Version = 2, Writing = f.Profiles.Single(x => x.Rules.Key == "writing-children").Rules.Writing! with { AudienceProfileId = id } };
        var wid = await e.RegisterProfileAsync(f.Owner, f.Universe, w, default); var wp = (await e.ProfilesAsync(f.Owner, f.Universe, default)).Single(x => x.Id == wid);
        await e.ReviewProfileAsync(f.Owner, f.Universe, wid, new(Guid.NewGuid(), wp.RowVersion, true, "fixture:writing"), default);
        await Assert.ThrowsAsync<RuntimeValidationException>(() => e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f) with { AudienceProfileId = id, WritingProfileId = wid }, default));
        var run = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f), default);
        await Assert.ThrowsAsync<CoreConcurrencyException>(() => e.ReviewVariantAsync(f.Owner, f.Universe, run.Id, new(Guid.NewGuid(), p.RowVersion, true, "fixture:stale"), default));
    }
    [SqlFact]
    public async Task Revoked_sources_withhold_derived_text_including_snapshots_and_block_new_variants()
    {
        using var root = LessonTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider; var f = await LessonTestFixture.Setup(s); var e = s.GetRequiredService<ILessonEngine>();
        var v = await e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f), default);
        var sources = s.GetRequiredService<ISourceRegistry>(); var detail = await sources.InspectAsync(f.Owner, f.Universe, f.SourceWork, default);
        await sources.RevokeAsync(f.Owner, f.Universe, f.SourceWork, new(Guid.NewGuid(), null, "Fixture revocation", detail.Work.RowVersion), default);
        var withheld = await e.InspectAsync(f.Owner, f.Universe, v.Id, default); Assert.True(withheld.OutputWithheld); Assert.Null(withheld.Sections);
        Assert.Equal("[Withheld]", withheld.Snapshot.Bundle.Anchor); Assert.Equal("[Withheld]", withheld.Snapshot.Bundle.Interpretation); Assert.Null(withheld.Snapshot.Bundle.Sanskrit); Assert.Empty(withheld.Snapshot.Bundle.Citations);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => e.AdaptAsync(f.Owner, f.Universe, LessonTestFixture.Request(f), default));
        await Assert.ThrowsAsync<RightsBlockedException>(() => e.ExportAsync(f.Owner, f.Universe, v.Id, default));
    }
    [SqlFact]
    public async Task Concurrent_replay_is_single_revision_and_staff_membership_header_and_scope_are_enforced()
    {
        using var root = LessonTestFixture.Services(); using var setup = root.CreateScope(); var f = await LessonTestFixture.Setup(setup.ServiceProvider); var req = LessonTestFixture.Request(f);
        using var a = root.CreateScope(); using var b = root.CreateScope();
        var results = await Task.WhenAll(a.ServiceProvider.GetRequiredService<ILessonEngine>().AdaptAsync(f.Owner, f.Universe, req, default), b.ServiceProvider.GetRequiredService<ILessonEngine>().AdaptAsync(f.Owner, f.Universe, req, default));
        Assert.Equal(results[0].Id, results[1].Id);
        var engine = setup.ServiceProvider.GetRequiredService<ILessonEngine>();
        await Assert.ThrowsAsync<CoreAccessException>(() => engine.AdaptAsync(new(f.Owner.TenantId, "m09-reader"), f.Universe, req with { OperationId = Guid.NewGuid() }, default));
        await Assert.ThrowsAsync<CoreAccessException>(() => engine.InspectAsync(new(Guid.NewGuid(), f.Owner.Actor), f.Universe, results[0].Id, default));
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
            }));
            builder.ConfigureServices(TestAuthentication.Configure);
        });
        using var client = factory.CreateClient(); var path = $"/api/v1/lessons/tenants/{f.Owner.TenantId}/universes/{f.Universe}/variants";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, req)).StatusCode);
        client.DefaultRequestHeaders.Add("X-ContentOS-Staff", "1");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/lessons/tenants/{Guid.NewGuid()}/universes/{f.Universe}/variants")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, req)).StatusCode);
    }
}
