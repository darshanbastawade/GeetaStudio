using System.Security.Cryptography;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;

namespace ContentOS.Foundation.Tests;

// Permission applies only to this original test sentence, never to scripture or a third-party work.
internal static class SourceTestFixture
{
    public static readonly byte[] Bytes = Encoding.UTF8.GetBytes("Original ContentOS test fixture: a traveller shares a cup of tea.");
    public static readonly string Checksum = Convert.ToHexString(SHA256.HashData(Bytes));
    public static async Task<(Guid Work, Guid Edition, Guid Snapshot)> CreateAsync(ISourceRegistry sources, CoreScope owner, Guid universe, bool approved = true)
    {
        var work = await sources.RegisterAsync(owner, universe, new(Guid.NewGuid(), "test-" + Guid.NewGuid().ToString("N"), "Original test sentence", "Synthetic"), CancellationToken.None);
        var edition = await sources.EditionAsync(owner, universe, work, new(Guid.NewGuid(), "v1", "ContentOS test fixture author", null, "ContentOS test harness", "en-IN", "sentence 1"), CancellationToken.None);
        if (approved)
        {
            var permission = await sources.PermissionAsync(owner, universe, edition, new(Guid.NewGuid(), "fixture:original-test-sentence-permission",
                ["en-IN"], ["IN"], ["Text", "Json"], true, true, true, true, true, "Credit: ContentOS original test fixture"), CancellationToken.None);
            var detail = await sources.InspectAsync(owner, universe, work, CancellationToken.None);
            await sources.ReviewPermissionAsync(owner, universe, permission, new(detail.Permissions.Single().RowVersion, "Approved"), CancellationToken.None);
        }
        var snapshot = await sources.ImportAsync(owner, universe, edition, new(Guid.NewGuid(), "original.txt", Bytes, Checksum, "fixture:original-test-sentence", approved), CancellationToken.None);
        var imported = await sources.InspectAsync(owner, universe, work, CancellationToken.None);
        await sources.ReviewSnapshotAsync(owner, universe, snapshot, new(imported.Snapshots.Single().RowVersion, "Approved"), CancellationToken.None);
        return (work, edition, snapshot);
    }
}
