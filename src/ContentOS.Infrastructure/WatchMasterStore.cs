using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace ContentOS.Infrastructure;

public sealed class WatchMasterStore(RuntimeDbContext db, IConfiguration configuration) : IWatchMasterStore
{
    public async Task<WatchStoredMaster> PutAsync(Guid id, byte[] bytes, string owner, CancellationToken ct)
    {
        if (bytes.Length is < 1 or > 33554432) throw new RuntimeValidationException("Owned WATCH master must be1–32MiB; M1 upload limits are unchanged."); var hash = MediaRules.Hash(bytes); var root = Path.GetFullPath(configuration["Assets:Stores:watch"] ?? throw new RuntimeValidationException("Private WATCH store required.")); Directory.CreateDirectory(root); var path = Path.Combine(root, id.ToString("N") + ".bin");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct); await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.WatchBlob:" + id},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'WATCH storage lock unavailable',1;", ct);
        var old = await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (old != null) { if (old.Owner != owner || old.Sha256 != hash || old.Length != bytes.Length || !File.Exists(path) || new FileInfo(path).Length != bytes.Length || MediaRules.Hash(await File.ReadAllBytesAsync(path, ct)) != hash) throw new RuntimeConflictException("Original WATCH master differs or is unavailable."); return new(id, hash, bytes.Length); }
        if (File.Exists(path)) { if (new FileInfo(path).Length != bytes.Length || MediaRules.Hash(await File.ReadAllBytesAsync(path, ct)) != hash) throw new RuntimeConflictException("Orphan master reconciliation mismatch."); } else { var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; try { await File.WriteAllBytesAsync(tmp, bytes, ct); File.Move(tmp, path, false); } finally { if (File.Exists(tmp)) File.Delete(tmp); } }
        db.Assets.Add(new() { Id = id, Owner = owner, Sha256 = hash, Length = bytes.Length, CreatedUtc = DateTimeOffset.UtcNow }); db.AssetLocations.Add(new() { AssetId = id, StoreKey = "watch" }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(id, hash, bytes.Length);
    }
}
