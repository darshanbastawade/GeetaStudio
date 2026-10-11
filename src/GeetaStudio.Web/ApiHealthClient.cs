using System.Net.Http.Json;
using System.Text.Json;
using ContentOS.Contracts;

namespace GeetaStudio.Web;

public sealed record ApiHealthResult(bool Reachable, HealthStatus? Health, string Message);

public sealed class ApiHealthClient(HttpClient client)
{
    public async Task<ApiHealthResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await client.GetFromJsonAsync<HealthStatus>("health/live", cancellationToken);
            return health is { Service: "GeetaStudio.Api", Status: "Healthy" }
                && health.CheckedAtUtc.Offset == TimeSpan.Zero
                ? new(true, health, "API reachable")
                : new(false, null, "API returned an unexpected health response. Check the API address and restart the hosts.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, "API check timed out. Start the API and select Check again.");
        }
        catch (HttpRequestException)
        {
            return new(false, null, "API unavailable. Start the API with its Development profile and check Api:BaseAddress.");
        }
        catch (JsonException)
        {
            return new(false, null, "API returned an unreadable health response. Check Api:BaseAddress.");
        }
    }
}
