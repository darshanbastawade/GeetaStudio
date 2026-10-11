using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Foundation.Tests;

[Collection("SQL runtime")]
[Trait("Category", "Sql")]
public sealed class SqlEditorialReviewRegressionTests
{
    [SqlFact]
    public async Task Audience_mismatch_is_rejected_at_evaluation_and_rechecked_at_approval()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await EditorialTestFixture.Setup(s); var e = s.GetRequiredService<IEditorialWorkbench>(); var db = s.GetRequiredService<CoreDbContext>();
        var adult = Guid.NewGuid(); db.Audiences.Add(new() { Id = adult, UniverseId = f.Universe, Key = "adult", Name = "Adult fixture", MinAge = 24, MaxAge = 120 }); await db.SaveChangesAsync();
        var p = await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f), default);
        p = await e.ActAsync(f.Owner, f.Universe, p.Id, EditorialTestFixture.Action(p, "Draft"), default);
        var wrong = await EditorialTestFixture.Report(s, f, p, adult);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => e.ActAsync(f.Owner, f.Universe, p.Id, EditorialTestFixture.Action(p, "Evaluate", wrong), default));
        p = await e.InspectAsync(f.Owner, f.Universe, p.Id, default); Assert.Equal("Draft", p.State); Assert.Empty(p.Approvals);
        p = await EditorialTestFixture.Reviewed(s, f, p);
        db.ChangeTracker.Clear(); var head = await db.Contents.SingleAsync(x => x.Id == p.ContentId); head.AudienceId = adult; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => e.ActAsync(f.Owner, f.Universe, p.Id, EditorialTestFixture.Action(p, "Approve"), default));
        var unchanged = await e.InspectAsync(f.Owner, f.Universe, p.Id, default); Assert.Equal("Review", unchanged.State); Assert.Empty(unchanged.Approvals);
    }

    [SqlFact]
    public async Task Asset_parent_lineage_propagates_source_correction_to_the_episode_and_its_copy()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await EditorialTestFixture.Setup(s); var e = s.GetRequiredService<IEditorialWorkbench>(); var sources = s.GetRequiredService<ISourceRegistry>();
        var source = await SourceTestFixture.CreateAsync(sources, f.Owner, f.Universe);
        var parent = await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f, "parent"), default);
        await sources.LinkAsync(f.Owner, f.Universe, new(Guid.NewGuid(), source.Snapshot, null, parent.RevisionId, null, "source"), default);
        parent = await EditorialTestFixture.Approved(s, f, await e.InspectAsync(f.Owner, f.Universe, parent.Id, default));
        var child = await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(f, "child") with { Format = "Episode" }, default);
        child = await e.AttachAssetAsync(f.Owner, f.Universe, child.Id, new(Guid.NewGuid(), child.RowVersion, "voice", "audio/wav", EditorialTestFixture.Wav(), "fixture:asset-parent"), default);
        await sources.LinkAsync(f.Owner, f.Universe, new(Guid.NewGuid(), null, parent.RevisionId, null, child.Assets.Single().AssetReferenceId, "asset-derived"), default);
        child = await e.InspectAsync(f.Owner, f.Universe, child.Id, default);
        Assert.Contains(child.Lineage, x => x.ContentId == parent.ContentId);
        Assert.Contains((await e.InspectAsync(f.Owner, f.Universe, parent.Id, default)).Lineage, x => x.ContentId == child.ContentId);
        child = await EditorialTestFixture.Approved(s, f, child);
        child = await e.ActAsync(f.Owner, f.Universe, child.Id, EditorialTestFixture.Action(child, "Schedule"), default);
        child = await e.ActAsync(f.Owner, f.Universe, child.Id, EditorialTestFixture.Action(child, "PublishDryRun"), default);
        var copy = Assert.Single(child.Copies);
        var correction = await e.CorrectAsync(f.Owner, f.Universe, new(Guid.NewGuid(), null, source.Snapshot, "Fixture source correction", "fixture:asset-impact"), default);
        var tasks = (await e.CorrectionsAsync(f.Owner, f.Universe, default)).Where(x => x.CorrectionId == correction).ToArray();
        Assert.Single(tasks, x => x.ContentId == parent.ContentId && x.Kind == "Internal");
        Assert.Single(tasks, x => x.ContentId == child.ContentId && x.Kind == "Internal");
        Assert.Single(tasks, x => x.ContentId == child.ContentId && x.Kind == "External" && x.CopyId == copy.Id);
        using var restart = root.CreateScope(); var durable = await restart.ServiceProvider.GetRequiredService<IEditorialWorkbench>().CorrectionsAsync(f.Owner, f.Universe, default);
        Assert.Equal(3, durable.Count(x => x.CorrectionId == correction));
        var withheld = await e.InspectAsync(f.Owner, f.Universe, child.Id, default); Assert.Equal("Corrected", withheld.State); Assert.True(withheld.SourceOutputWithheld); Assert.All(withheld.Approvals, x => Assert.False(x.Current));
    }

    [SqlFact]
    public async Task Factory_cast_context_cannot_be_omitted_and_retirement_prevents_renewed_approval()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await FactoryTestFixture.Setup(s); var factory = s.GetRequiredService<IDraftFactory>(); var e = s.GetRequiredService<IEditorialWorkbench>(); var registry = s.GetRequiredService<IUniverseRegistry>();
        var request = FactoryTestFixture.Request(f, second: false);
        request = request with { Briefs = [request.Briefs[0] with { Formats = [request.Briefs[0].Formats.Single(x => x.Kind == "Story")] }] };
        var plan = await FactoryTestFixture.Approved(s, f, request);
        await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(plan), default); await FactoryTestFixture.Drain(s);
        var artifact = Assert.Single(await factory.ArtifactsAsync(f.Owner, f.Universe, default));
        var p = await e.StartAsync(f.Owner, f.Universe, new(Guid.NewGuid(), "factory-cast", "Episode", "Fixture cast review", ContentId: artifact.CoreContentId), default);
        var db = s.GetRequiredService<CoreDbContext>(); var subject = await db.Contents.Where(x => x.Id == p.ContentId).Select(x => x.SubjectId).SingleAsync();
        var ef = new EditorialTestFixture.Fixture(f.Owner, f.Universe, f.Audience, subject, f.Bible, f.Episode);
        var bodyOnly = await EditorialTestFixture.Report(s, ef, p, omitCast: true);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => e.ActAsync(f.Owner, f.Universe, p.Id, EditorialTestFixture.Action(p, "Evaluate", bodyOnly), default));
        p = await EditorialTestFixture.Approved(s, ef, await e.InspectAsync(f.Owner, f.Universe, p.Id, default));
        Assert.Single(p.Approvals, x => x.Current);
        var character = (await registry.CharactersAsync(f.Owner, f.Universe, default)).Single(x => x.CharacterId == f.Character);
        await registry.ControlCharacterAsync(f.Owner, f.Universe, f.Character, new(Guid.NewGuid(), character.RowVersion, "Retire", "fixture:retired-cast"), default);
        var expired = await e.InspectAsync(f.Owner, f.Universe, p.Id, default);
        Assert.Contains("ApprovalExpired", expired.Blocks); Assert.Contains("CurrentApprovedCastRequired", expired.Blocks);
        Assert.All(expired.Approvals, x => Assert.False(x.Current));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => EditorialTestFixture.Approved(s, ef, expired));
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => s.GetRequiredService<IContentCore>().ExportAsync(f.Owner, f.Universe, p.ContentId, default));
    }
    [SqlFact]
    public async Task Factory_ancestor_cast_is_visible_and_retirement_invalidates_a_derivative_export()
    {
        using var root = EditorialTestFixture.Services(); using var scope = root.CreateScope(); var s = scope.ServiceProvider;
        var f = await FactoryTestFixture.Setup(s); var factory = s.GetRequiredService<IDraftFactory>(); var e = s.GetRequiredService<IEditorialWorkbench>(); var registry = s.GetRequiredService<IUniverseRegistry>();
        var request = FactoryTestFixture.Request(f, second: false);
        request = request with { Briefs = [request.Briefs[0] with { Formats = [request.Briefs[0].Formats.Single(x => x.Kind == "Story")] }] };
        var plan = await FactoryTestFixture.Approved(s, f, request);
        await factory.StartAsync(f.Owner, f.Universe, FactoryTestFixture.Start(plan), default); await FactoryTestFixture.Drain(s);
        var artifact = Assert.Single(await factory.ArtifactsAsync(f.Owner, f.Universe, default));
        var db = s.GetRequiredService<CoreDbContext>(); var subject = await db.Contents.Where(x => x.Id == artifact.CoreContentId).Select(x => x.SubjectId).SingleAsync();
        var ef = new EditorialTestFixture.Fixture(f.Owner, f.Universe, f.Audience, subject, f.Bible, f.Episode);
        var child = await e.StartAsync(f.Owner, f.Universe, EditorialTestFixture.Request(ef, "factory-derivative"), default);
        await s.GetRequiredService<ISourceRegistry>().LinkAsync(f.Owner, f.Universe, new(Guid.NewGuid(), null, artifact.CoreRevisionId, child.RevisionId, null, "derived"), default);
        child = await e.InspectAsync(f.Owner, f.Universe, child.Id, default); Assert.Single(child.FactoryHistory);
        child = await EditorialTestFixture.Approved(s, ef, child);
        await s.GetRequiredService<IContentCore>().ExportAsync(f.Owner, f.Universe, child.ContentId, default);
        var character = (await registry.CharactersAsync(f.Owner, f.Universe, default)).Single(x => x.CharacterId == f.Character);
        await registry.ControlCharacterAsync(f.Owner, f.Universe, f.Character, new(Guid.NewGuid(), character.RowVersion, "Retire", "fixture:retired-ancestor"), default);
        var expired = await e.InspectAsync(f.Owner, f.Universe, child.Id, default);
        Assert.Contains("ApprovalExpired", expired.Blocks); Assert.Contains("CurrentApprovedCastRequired", expired.Blocks);
        await Assert.ThrowsAsync<EvidenceBlockedException>(() => s.GetRequiredService<IContentCore>().ExportAsync(f.Owner, f.Universe, child.ContentId, default));
    }
}
