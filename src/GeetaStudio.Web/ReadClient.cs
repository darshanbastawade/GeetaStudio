using System.Net;
using System.Net.Http.Json;
using ContentOS.Contracts;
namespace GeetaStudio.Web;

public sealed class ReadClient(IHttpClientFactory factory, IConfiguration config)
{
    public async Task<ReadCollectionSummary[]> Directory(CancellationToken ct = default) =>
        await factory.CreateClient("read-public").GetFromJsonAsync<ReadCollectionSummary[]>("api/v1/read", ct) ?? [];
    public async Task<ReadBook?> Book(string key, bool preview, CancellationToken ct = default)
    {
        if (preview)
        {
            if (!Guid.TryParse(config["Read:PreviewCollection"], out var id)) return null;
            var result = await Fetch(factory.CreateClient("read-preview"), "api/v1/read-preview/" + id, ct);
            return result != null && (key.Length == 0 || result.Collection.Key == key) ? result : null;
        }
        return await Fetch(factory.CreateClient("read-public"), "api/v1/read/" + Uri.EscapeDataString(key), ct);
    }
    private static async Task<ReadBook?> Fetch(HttpClient client, string path, CancellationToken ct)
    {
        var result = await client.GetAsync(path, ct); if (result.StatusCode == HttpStatusCode.NotFound) return null;
        result.EnsureSuccessStatusCode(); return await result.Content.ReadFromJsonAsync<ReadBook>(ct);
    }
    public async Task SendEvent(string key, ReadEngagement request, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/read/" + Uri.EscapeDataString(key) + "/events") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Read-Event-Version", "1"); var response = await factory.CreateClient("read-public").SendAsync(message, ct); response.EnsureSuccessStatusCode();
    }
}
