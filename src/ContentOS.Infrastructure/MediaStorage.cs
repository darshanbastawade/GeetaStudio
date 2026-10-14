using System.Security.Cryptography;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace ContentOS.Infrastructure;

public sealed class MediaStorageMover(RuntimeDbContext db, IConfiguration config, IAssetStore store) : IMediaStorageMover
{
    private string Root(string key) { if (key == "primary") return Path.GetFullPath(config["Assets:Root"] ?? throw new RuntimeValidationException("Asset root required.")); if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-z0-9_-]{1,40}$")) throw new RuntimeValidationException("Approved storage key required."); return Path.GetFullPath(config["Assets:Stores:" + key] ?? throw new RuntimeValidationException("Storage mapping is not configured.")); }
    public async Task<string> LocationAsync(Guid id, string actor, CancellationToken ct) { if (!await db.Assets.AnyAsync(x => x.Id == id && x.Owner == actor, ct)) throw new CoreAccessException(); return await db.AssetLocations.Where(x => x.AssetId == id).Select(x => x.StoreKey).SingleOrDefaultAsync(ct) ?? "primary"; }
    public async Task<MediaStoreMoveResult> MoveAsync(Guid id, string actor, string key, CancellationToken ct)
    {
        var bytes = await store.GetAsync(id, actor, ct) ?? throw new CoreAccessException(); var hash = Convert.ToHexString(SHA256.HashData(bytes)); var root = Root(key); Directory.CreateDirectory(root); var target = Path.Combine(root, id.ToString("N") + ".bin");
        if (File.Exists(target)) { if (new FileInfo(target).Length != bytes.Length) throw new RuntimeConflictException("Destination length mismatch; mapping unchanged."); var old = await File.ReadAllBytesAsync(target, ct); if (!old.AsSpan().SequenceEqual(bytes)) throw new RuntimeConflictException("Destination checksum mismatch; mapping unchanged."); }
        else { var tmp = target + "." + Guid.NewGuid().ToString("N") + ".tmp"; try { await File.WriteAllBytesAsync(tmp, bytes, ct); File.Move(tmp, target, false); } finally { if (File.Exists(tmp)) File.Delete(tmp); } }
        var check = await File.ReadAllBytesAsync(target, ct); if (MediaRules.Hash(check) != hash) throw new RuntimeConflictException("Destination verification failed.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"ContentOS.AssetMove:" + id},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Asset move lock unavailable',1;", ct);
        var location = await db.AssetLocations.SingleOrDefaultAsync(x => x.AssetId == id, ct); if (location == null) db.AssetLocations.Add(new() { AssetId = id, StoreKey = key }); else { location.StoreKey = key; location.VerifiedUtc = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(id, key, hash, bytes.Length);
    }
}
