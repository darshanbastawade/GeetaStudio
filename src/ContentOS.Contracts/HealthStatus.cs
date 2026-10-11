namespace ContentOS.Contracts;

/// <summary>Version 1 liveness response. No dependency diagnostics or credentials.</summary>
public sealed record HealthStatus(string Service, string Status, DateTimeOffset CheckedAtUtc);

/// <summary>Development-only deterministic demonstration; never generated content.</summary>
public sealed record DevelopmentStatus(string Mode, bool IsFixture, string Message);
