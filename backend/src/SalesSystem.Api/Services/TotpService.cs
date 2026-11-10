using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace SalesSystem.Api.Services;

/// <summary>Six-digit, 30-second authenticator codes; secrets are encrypted before database storage.</summary>
public sealed class TotpService(IDataProtectionProvider dataProtection, TimeProvider clock)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("SalesSystem.Mfa.v1");
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public (string Secret, string ProtectedSecret) NewSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return (ToBase32(bytes), _protector.Protect(Convert.ToBase64String(bytes)));
    }

    public bool TryVerify(string? protectedSecret, string? code, long? lastUsedStep, out long usedStep)
    {
        usedStep = 0;
        if (string.IsNullOrWhiteSpace(protectedSecret) || string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        if (code.Length != 6 || code.Any(c => c is < '0' or > '9')) return false;

        byte[] secret;
        try { secret = Convert.FromBase64String(_protector.Unprotect(protectedSecret)); }
        catch (Exception e) when (e is CryptographicException or FormatException) { return false; }

        var nowStep = clock.GetUtcNow().ToUnixTimeSeconds() / 30;
        for (var step = nowStep - 1; step <= nowStep + 1; step++)
        {
            if (step <= lastUsedStep) continue;
            var expected = CodeForStep(secret, step);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(code))) continue;
            usedStep = step;
            return true;
        }
        return false;
    }

    public static string EnrollmentUri(string username, string secret)
    {
        const string issuer = "Enjaz & Tamkeen";
        var label = Uri.EscapeDataString($"{issuer}:{username}");
        return $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
    }

    private static string CodeForStep(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0f;
        var value = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static string ToBase32(ReadOnlySpan<byte> bytes)
    {
        var result = new StringBuilder((bytes.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                result.Append(Alphabet[(buffer >> bits) & 31]);
            }
        }
        if (bits > 0) result.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return result.ToString();
    }
}
