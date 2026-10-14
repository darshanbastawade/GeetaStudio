using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static class CustomerRules
{
    public static void Require(bool valid, string message) { if (!valid) throw new RuntimeValidationException(message); }
    public static string Hash(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    public static string Json<T>(T value) => JsonSerializer.Serialize(value);
    public static T Read<T>(string json) { try { return JsonSerializer.Deserialize<T>(json) ?? throw new RuntimeValidationException("Invalid customer evidence."); } catch (JsonException) { throw new RuntimeValidationException("Invalid customer evidence JSON."); } }
    public static void Key(string value) => Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 100 && value.All(x => char.IsAsciiLetterOrDigit(x) || "-_.:".Contains(x)), "Bounded identifier required; no personal data or URLs.");
    public static void Label(string value) => Require(!string.IsNullOrEmpty(value) && value.Length <= 80 && !value.Any(char.IsControl), "Short profile label required.");
    public static void Credits(long value) => Require(value is > 0 and <= 1000000000, "Credits must be positive bounded integer units.");
    public static void Future(DateTimeOffset value) => Require(value > DateTimeOffset.UtcNow && value <= DateTimeOffset.UtcNow.AddYears(2), "Expiry must be in the next two years.");
    public static long RefundCredits(long credits, long refunded, long price) { Require(price > 0 && refunded >= 0 && refunded <= price, "Refund outside original price."); return (long)((decimal)credits * refunded / price); }
}
