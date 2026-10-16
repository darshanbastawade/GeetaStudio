using System.Data;
using Microsoft.EntityFrameworkCore;
namespace ContentOS.Infrastructure;

public sealed partial class TrendWorkbench
{
    private async Task<T> Project<T>(Func<Task<T>> projection, CancellationToken ct)
    {
        var owns = db.Database.CurrentTransaction == null;
        if (owns) db.ChangeTracker.Clear();
        await using var tx = owns ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var value = await projection();
        if (tx != null) await tx.CommitAsync(ct);
        return value;
    }
}
