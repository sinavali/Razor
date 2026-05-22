using System.Text;
using System.Text.Json;

namespace Chronos.Samples.Plugins.Adapters.MT5;

/// <summary>
/// Lightweight length‑prefixed JSON messaging protocol over TCP.
/// </summary>
internal static class Mt5MessageProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Reads a complete message (4‑byte little‑endian length prefix + JSON).</summary>
    public static async Task<string> ReadMessageAsync(Stream stream, CancellationToken ct)
    {
        byte[] lengthBuffer = new byte[4];
        await stream.ReadExactlyAsync(lengthBuffer, ct).ConfigureAwait(false);
        int length = BitConverter.ToInt32(lengthBuffer, 0);
        if (length <= 0 || length > 10 * 1024 * 1024)
            throw new InvalidOperationException($"Invalid message length: {length}");

        byte[] jsonBuffer = new byte[length];
        await stream.ReadExactlyAsync(jsonBuffer, ct).ConfigureAwait(false);
        return Encoding.UTF8.GetString(jsonBuffer);
    }

    /// <summary>Writes a message with length prefix.</summary>
    public static async Task WriteMessageAsync(Stream stream, string json, CancellationToken ct)
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);
        await stream.WriteAsync(lengthBytes, ct).ConfigureAwait(false);
        await stream.WriteAsync(jsonBytes, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Serialises an object to JSON.</summary>
    public static string Serialize(object obj) => JsonSerializer.Serialize(obj, JsonOptions);

    /// <summary>Deserialises JSON to the given type.</summary>
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions);
}