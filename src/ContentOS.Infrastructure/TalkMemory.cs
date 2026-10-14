using System.Collections.Concurrent;
namespace ContentOS.Infrastructure;

public sealed class TalkMemory
{
    private sealed record Entry(DateTimeOffset ExpiresUtc, (Guid TurnId, string Question, string Answer)[] Turns);
    private readonly ConcurrentDictionary<Guid, Entry> sessions = new();
    internal (Guid TurnId, string Question, string Answer)[] Read(Guid session, DateTimeOffset now)
    { if (sessions.TryGetValue(session, out var e) && e.ExpiresUtc > now) return e.Turns; sessions.TryRemove(session, out _); return []; }
    internal void Add(Guid session, DateTimeOffset expires, Guid turn, string question, string answer)
    {
        foreach (var x in sessions.Where(x => x.Value.ExpiresUtc <= DateTimeOffset.UtcNow)) sessions.TryRemove(x.Key, out _);
        if (sessions.Count >= 1000 && !sessions.ContainsKey(session)) return;
        sessions.AddOrUpdate(session, new Entry(expires, [(turn, question, answer)]), (_, old) => new(expires, old.Turns.Append((turn, question, answer)).TakeLast(3).ToArray()));
    }
    internal void Forget(Guid session) => sessions.TryRemove(session, out _);
}

