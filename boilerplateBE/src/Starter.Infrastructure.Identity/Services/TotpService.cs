using System.Security.Cryptography;
using System.Text;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Identity.Services;

/// <summary>
/// TOTP service implementation using RFC 6238 algorithm with HMAC-SHA1.
/// </summary>
public class TotpService : ITotpService
{
    private const int SecretLength = 20;
    private const int TotpDigits = 6;
    private const int TotpPeriodSeconds = 30;
    private const int AllowedDriftSteps = 1;
    private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        var secretBytes = new byte[SecretLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(secretBytes);
        return Base32Encode(secretBytes);
    }

    public string GetQrCodeUri(string email, string secret, string issuer)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&digits={TotpDigits}&period={TotpPeriodSeconds}";
    }

    public bool ValidateCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != TotpDigits)
            return false;

        var secretBytes = Base32Decode(secret);
        var currentTimeStep = GetCurrentTimeStep();

        for (var i = -AllowedDriftSteps; i <= AllowedDriftSteps; i++)
        {
            var computedCode = ComputeTotp(secretBytes, currentTimeStep + i);
            if (string.Equals(computedCode, code, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public List<string> GenerateBackupCodes(int count = 8)
    {
        var codes = new List<string>(count);
        using var rng = RandomNumberGenerator.Create();

        for (var i = 0; i < count; i++)
        {
            var bytes = new byte[5];
            rng.GetBytes(bytes);
            var code = Convert.ToHexString(bytes)[..8].ToUpperInvariant();
            codes.Add(code);
        }

        return codes;
    }

    public string HashBackupCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.ToUpperInvariant()));
        return Convert.ToBase64String(bytes);
    }

    private static long GetCurrentTimeStep()
    {
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return unixTimestamp / TotpPeriodSeconds;
    }

    private static string ComputeTotp(byte[] secret, long timeStep)
    {
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % (int)Math.Pow(10, TotpDigits);
        return otp.ToString().PadLeft(TotpDigits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder();
        var bitBuffer = 0;
        var bitsInBuffer = 0;

        foreach (var b in data)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                var index = (bitBuffer >> bitsInBuffer) & 0x1F;
                sb.Append(Base32Chars[index]);
            }
        }

        if (bitsInBuffer > 0)
        {
            var index = (bitBuffer << (5 - bitsInBuffer)) & 0x1F;
            sb.Append(Base32Chars[index]);
        }

        return sb.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        base32 = base32.TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        var bitBuffer = 0;
        var bitsInBuffer = 0;

        foreach (var c in base32)
        {
            var val = Base32Chars.IndexOf(c);
            if (val < 0) continue;

            bitBuffer = (bitBuffer << 5) | val;
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                output.Add((byte)((bitBuffer >> bitsInBuffer) & 0xFF));
            }
        }

        return output.ToArray();
    }
}
