using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Infrastructure;

public sealed partial class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMember> Members => Set<TenantMember>();
    public DbSet<Universe> Universes => Set<Universe>();
    public DbSet<UniverseGrant> Grants => Set<UniverseGrant>();
    public DbSet<ContentLocale> Locales => Set<ContentLocale>();
    public DbSet<AudienceProfile> Audiences => Set<AudienceProfile>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<UniversePack> Packs => Set<UniversePack>();
    public DbSet<PackTerm> Terms => Set<PackTerm>();
    public DbSet<PackSubjectType> SubjectTypes => Set<PackSubjectType>();
    public DbSet<SourceRule> SourceRules => Set<SourceRule>();
    public DbSet<EditorialPolicy> Policies => Set<EditorialPolicy>();
    public DbSet<PackAudience> PackAudiences => Set<PackAudience>();
    public DbSet<PackCharacter> PackCharacters => Set<PackCharacter>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<CharacterRevision> CharacterRevisions => Set<CharacterRevision>();
    public DbSet<CharacterAlias> Aliases => Set<CharacterAlias>();
    public DbSet<CharacterRelationship> Relationships => Set<CharacterRelationship>();
    public DbSet<PresentationProfile> Presentations => Set<PresentationProfile>();
    public DbSet<ContentItem> Contents => Set<ContentItem>();
    public DbSet<ContentRevision> Revisions => Set<ContentRevision>();
    public DbSet<CoreAssetReference> Assets => Set<CoreAssetReference>();
    public DbSet<WorkflowRun> Workflows => Set<WorkflowRun>();
    public DbSet<EpisodeCastEntry> Cast => Set<EpisodeCastEntry>();
    public DbSet<CoreOperation> Operations => Set<CoreOperation>();
    public DbSet<CoreEvent> Events => Set<CoreEvent>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema("core");
        foreach (var type in new[] { typeof(Organization), typeof(Tenant), typeof(Universe), typeof(AudienceProfile), typeof(Subject),
            typeof(UniversePack), typeof(Character), typeof(PresentationProfile), typeof(ContentItem), typeof(CoreAssetReference), typeof(WorkflowRun) })
        {
            m.Entity(type).HasBaseType((Type?)null);
            m.Entity(type).HasKey("Id");
            m.Entity(type).Property<byte[]>("RowVersion").IsRowVersion();
        }
        m.Entity<TenantMember>().HasKey(x => new { x.TenantId, x.Actor });
        m.Entity<UniverseGrant>().HasKey(x => new { x.UniverseId, x.GranteeTenantId });
        m.Entity<PackTerm>().HasKey(x => new { x.PackId, x.Key });
        m.Entity<PackSubjectType>().HasKey(x => new { x.PackId, x.Key });
        m.Entity<SourceRule>().HasKey(x => new { x.PackId, x.Kind });
        m.Entity<EditorialPolicy>().HasKey(x => new { x.PackId, x.Key });
        m.Entity<PackAudience>().HasKey(x => new { x.PackId, x.AudienceId });
        m.Entity<PackCharacter>().HasKey(x => new { x.PackId, x.CharacterId });
        m.Entity<CharacterAlias>().HasKey(x => new { x.UniverseId, x.Name });
        m.Entity<CharacterRelationship>().HasKey(x => new { x.FromCharacterId, x.ToCharacterId, x.Kind });
        m.Entity<EpisodeCastEntry>().HasKey(x => new { x.ContentRevisionId, x.CharacterRevisionId });
        m.Entity<CoreOperation>().HasKey(x => new { x.TenantId, x.Id });
        foreach (var entity in m.Model.GetEntityTypes())
            foreach (var p in entity.GetProperties())
                if (p.ClrType == typeof(string)) p.SetMaxLength(200);
        m.Entity<CharacterRevision>().Property(x => x.Biography).HasMaxLength(2000);
        m.Entity<ContentRevision>().Property(x => x.Body).HasMaxLength(16000);
        m.Entity<ContentRevision>().Property(x => x.Provenance).HasMaxLength(1000);
        m.Entity<SourceRule>().Property(x => x.Rule).HasMaxLength(1000);
        m.Entity<EditorialPolicy>().Property(x => x.Rule).HasMaxLength(1000);
        m.Entity<Tenant>().HasIndex(x => x.Key).IsUnique();
        m.Entity<Universe>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        m.Entity<Universe>().HasAlternateKey(x => new { x.Id, x.TenantId });
        m.Entity<AudienceProfile>().HasIndex(x => new { x.UniverseId, x.Key }).IsUnique();
        m.Entity<AudienceProfile>().HasAlternateKey(x => new { x.Id, x.UniverseId });
        m.Entity<Subject>().HasIndex(x => new { x.UniverseId, x.Key }).IsUnique();
        m.Entity<Subject>().HasAlternateKey(x => new { x.Id, x.UniverseId });
        m.Entity<UniversePack>().HasIndex(x => new { x.UniverseId, x.Version }).IsUnique();
        m.Entity<Character>().HasIndex(x => new { x.UniverseId, x.Key }).IsUnique();
        m.Entity<Character>().HasAlternateKey(x => new { x.Id, x.UniverseId });
        m.Entity<CharacterRevision>().HasIndex(x => new { x.CharacterId, x.Version }).IsUnique();
        m.Entity<ContentItem>().HasIndex(x => new { x.TenantId, x.UniverseId });
        m.Entity<ContentRevision>().HasIndex(x => new { x.ContentId, x.Version }).IsUnique();
        m.Entity<ContentRevision>().HasIndex(x => new { x.ContentId, x.OperationId }).IsUnique();
        m.Entity<WorkflowRun>().HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        m.Entity<Tenant>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TenantMember>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Universe>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<UniverseGrant>().HasOne<Universe>().WithMany().HasForeignKey(x => x.UniverseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<UniverseGrant>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.GranteeTenantId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AudienceProfile>().HasOne<Universe>().WithMany().HasForeignKey(x => x.UniverseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Subject>().HasOne<Universe>().WithMany().HasForeignKey(x => x.UniverseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<UniversePack>().HasOne<Universe>().WithMany().HasForeignKey(x => x.UniverseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PackTerm>().HasOne<UniversePack>().WithMany().HasForeignKey(x => x.PackId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PackSubjectType>().HasOne<UniversePack>().WithMany().HasForeignKey(x => x.PackId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceRule>().HasOne<UniversePack>().WithMany().HasForeignKey(x => x.PackId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EditorialPolicy>().HasOne<UniversePack>().WithMany().HasForeignKey(x => x.PackId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PackAudience>().HasOne<UniversePack>().WithMany().HasForeignKey(x => x.PackId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PackAudience>().HasOne<AudienceProfile>().WithMany().HasForeignKey(x => x.AudienceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PackCharacter>().HasOne<UniversePack>().WithMany().HasForeignKey(x => x.PackId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PackCharacter>().HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Character>().HasOne<Universe>().WithMany().HasForeignKey(x => x.UniverseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CharacterRevision>().HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CharacterAlias>().HasOne<Character>().WithMany().HasForeignKey(x => new { x.CharacterId, x.UniverseId }).HasPrincipalKey(x => new { x.Id, x.UniverseId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CharacterRelationship>().HasOne<Character>().WithMany().HasForeignKey(x => x.FromCharacterId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CharacterRelationship>().HasOne<Character>().WithMany().HasForeignKey(x => x.ToCharacterId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PresentationProfile>().HasOne<CharacterRevision>().WithMany().HasForeignKey(x => x.CharacterRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PresentationProfile>().HasOne<ContentLocale>().WithMany().HasForeignKey(x => x.LocaleId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PresentationProfile>().HasOne<AudienceProfile>().WithMany().HasForeignKey(x => x.AudienceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ContentItem>().HasOne<Universe>().WithMany().HasForeignKey(x => new { x.UniverseId, x.TenantId }).HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ContentItem>().HasOne<Subject>().WithMany().HasForeignKey(x => new { x.SubjectId, x.UniverseId }).HasPrincipalKey(x => new { x.Id, x.UniverseId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ContentItem>().HasOne<AudienceProfile>().WithMany().HasForeignKey(x => new { x.AudienceId, x.UniverseId }).HasPrincipalKey(x => new { x.Id, x.UniverseId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ContentItem>().HasOne<ContentLocale>().WithMany().HasForeignKey(x => x.LocaleId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ContentRevision>().HasOne<ContentItem>().WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CoreAssetReference>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.ContentRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WorkflowRun>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.ContentRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EpisodeCastEntry>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.ContentRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EpisodeCastEntry>().HasOne<CharacterRevision>().WithMany().HasForeignKey(x => x.CharacterRevisionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<EpisodeCastEntry>().HasOne<PresentationProfile>().WithMany().HasForeignKey(x => x.PresentationProfileId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PresentationProfile>().HasOne<CoreAssetReference>().WithMany().HasForeignKey(x => x.VisualAssetReferenceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PresentationProfile>().HasOne<CoreAssetReference>().WithMany().HasForeignKey(x => x.VoiceAssetReferenceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CoreOperation>().HasOne<ContentItem>().WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CoreEvent>().HasOne<ContentRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict);
        ConfigureSources(m);
        ConfigureCorpus(m); ConfigureEvidence(m); ConfigureKnowledge(m); ConfigureAi(m); ConfigureWorkflows(m); ConfigureLessons(m);
    }
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (ChangeTracker.Entries().Any(e => e.State == EntityState.Deleted && e.Entity is LessonProfile or LessonBundle or LessonMemory or LessonVariant or LessonCommand)) throw new InvalidOperationException("Lesson history cannot be deleted.");
        foreach (var e in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (e.State == EntityState.Modified && e.Entity is ContentRevision or CharacterRevision or UniversePack or PackTerm or PackSubjectType or SourceRule or EditorialPolicy or PackAudience or PackCharacter or EpisodeCastEntry or PresentationProfile)
                throw new InvalidOperationException("Revisions, pack versions and frozen cast entries are immutable.");
            ValidateSourceChange(e);
            ValidateCorpusChange(e); ValidateEvidenceChange(e); ValidateKnowledgeChange(e); ValidateAiChange(e); ValidateWorkflowChange(e); ValidateLessonChange(e);
            if (e.Entity is CoreRecord record) record.UpdatedUtc = DateTimeOffset.UtcNow;
            foreach (var p in e.Properties)
                if (p.CurrentValue is DateTimeOffset instant) p.CurrentValue = instant.ToUniversalTime();
        }
        foreach (var e in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (e.Entity is CoreAssetReference asset &&
                !await (from r in Revisions
                        join c in Contents on r.ContentId equals c.Id
                        where r.Id == asset.ContentRevisionId && c.TenantId == asset.TenantId
                        select r.Id).AnyAsync(ct))
                throw new RuntimeValidationException("Asset reference must belong to its revision's tenant.");
            if (e.Entity is WorkflowRun run &&
                !await (from r in Revisions
                        join c in Contents on r.ContentId equals c.Id
                        where r.Id == run.ContentRevisionId && c.TenantId == run.TenantId
                        select r.Id).AnyAsync(ct))
                throw new RuntimeValidationException("Workflow must belong to its revision's tenant.");
            if (e.Entity is CharacterRelationship relation &&
                !await (from a in Characters
                        join b in Characters on a.UniverseId equals b.UniverseId
                        where a.Id == relation.FromCharacterId && b.Id == relation.ToCharacterId
                        select a.Id).AnyAsync(ct))
                throw new RuntimeValidationException("Character relationships must stay in one universe.");
            if (e.Entity is PresentationProfile presentation)
            {
                var universe = await (from r in CharacterRevisions
                                      join c in Characters on r.CharacterId equals c.Id
                                      where r.Id == presentation.CharacterRevisionId
                                      select c).SingleOrDefaultAsync(ct);
                if (universe is null || !await Audiences.AnyAsync(a => a.Id == presentation.AudienceId && a.UniverseId == universe.UniverseId, ct))
                    throw new RuntimeValidationException("Presentation audience must belong to its character's universe.");
                foreach (var assetId in new[] { presentation.VisualAssetReferenceId, presentation.VoiceAssetReferenceId }.Where(id => id.HasValue))
                    if (!await (from a in Assets
                                join u in Universes on a.TenantId equals u.TenantId
                                where a.Id == assetId && u.Id == universe.UniverseId
                                select a.Id).AnyAsync(ct))
                        throw new RuntimeValidationException("Presentation assets must belong to the character's tenant.");
            }
            if (e.Entity is EpisodeCastEntry cast &&
                !await (from r in Revisions
                        join c in Contents on r.ContentId equals c.Id
                        join character in Characters on c.UniverseId equals character.UniverseId
                        join cr in CharacterRevisions on character.Id equals cr.CharacterId
                        join p in Presentations on cr.Id equals p.CharacterRevisionId
                        where r.Id == cast.ContentRevisionId && cr.Id == cast.CharacterRevisionId && p.Id == cast.PresentationProfileId
                        select r.Id).AnyAsync(ct))
                throw new RuntimeValidationException("Frozen cast must use matching character/presentation revisions in the content universe.");
        }
        return await base.SaveChangesAsync(ct);
    }
}
public static class CoreServices
{
    public static IServiceCollection AddContentCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextFactory<CoreDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("ContentOS"),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "core")).EnableSensitiveDataLogging(false));
        services.AddScoped<ContentOS.Application.IContentCore, ContentCoreStore>();
        services.AddScoped<ContentOS.Application.ICanonicalCorpus, CanonicalCorpus>(); services.AddScoped<ContentOS.Application.IEvidenceEngine, EvidenceEngine>();
        services.AddScoped<ContentOS.Application.ISourceRegistry, SourceRegistry>();
        services.AddScoped<ContentOS.Application.ISourceRightsGuard, SourceRightsGuard>();
        services.AddScoped<KnowledgeService>();
        services.AddScoped<ContentOS.Application.IKnowledgeAdministration>(s => s.GetRequiredService<KnowledgeService>());
        services.AddScoped<ContentOS.Application.IKnowledgeRetriever>(s => s.GetRequiredService<KnowledgeService>());
        services.AddScoped<ContentOS.Application.IKnowledgeIndexWorker>(s => s.GetRequiredService<KnowledgeService>());
        return services;
    }
}
public sealed class CoreDesignFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<CoreDbContext>()
        .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__ContentOS") ??
            "Server=localhost;Database=GeetaStudio_Dev;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "core")).Options);
}

