using System.Net;
using System.Net.Sockets;
using ContentOS.Domain;
namespace ContentOS.Integrations;

// Per-request transport: no proxy, cookies, credentials, redirects, pooling or SDK retries.
// Resolve and validate at connection time, then connect to that exact IP, retaining HTTPS SNI.
public sealed class RssFeedHttp
{
    public static bool PublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var b = address.GetAddressBytes();
        if (b.Length == 16) return (b[0] & 0xe0) == 0x20 && !(b[0] == 0x20 && b[1] == 1 && (b[2] < 2 || b[2] == 0x0d && b[3] == 0xb8)) && !(b[0] == 0x20 && b[1] == 2) && !(b[0] == 0x3f && b[1] == 0xff && (b[2] & 0xf0) == 0);
        return b[0] is not (0 or 10 or 127) && b[0] < 224 &&
            !(b[0] == 100 && b[1] is >= 64 and <= 127) && !(b[0] == 169 && b[1] == 254) &&
            !(b[0] == 172 && b[1] is >= 16 and <= 31) && !(b[0] == 192 && (b[1] == 168 || b[1] == 0 && b[2] is 0 or 2 || b[1] == 88 && b[2] == 99)) &&
            !(b[0] == 198 && (b[1] is 18 or 19 || b[1] == 51 && b[2] == 100)) && !(b[0] == 203 && b[1] == 0 && b[2] == 113) &&
            !address.Equals(IPAddress.Parse("168.63.129.16"));
    }
    public static async ValueTask<Stream> Connect(string host, int port, Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        Func<IPAddress, int, CancellationToken, ValueTask<Stream>> connect, CancellationToken ct)
    {
        var addresses = await resolve(host, ct);
        if (port != 443 || addresses.Length == 0 || addresses.Any(x => !PublicAddress(x))) throw new RuntimeValidationException("RSS destination is not permitted.");
        // One TCP attempt; never resolve the hostname again after validation.
        return await connect(addresses[0], port, ct);
    }
    public static HttpMessageHandler CreateHandler() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        MaxResponseHeadersLength = 16,
        ConnectCallback = (context, ct) => Connect(context.DnsEndPoint.Host, context.DnsEndPoint.Port,
            (host, token) => Dns.GetHostAddressesAsync(host, token), async (address, port, token) =>
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(new IPEndPoint(address, port), token); return new NetworkStream(socket, ownsSocket: true); }
                catch { socket.Dispose(); throw; }
            }, ct)
    };
}
