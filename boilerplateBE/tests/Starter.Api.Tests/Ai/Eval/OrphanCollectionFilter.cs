namespace Starter.Api.Tests.Ai.Eval;

/// <summary>
/// Identifies Qdrant collections that belong to prior eval-harness runs so the
/// test fixture can reap them without touching real tenants.
/// </summary>
/// <remarks>
/// The harness names its synthetic tenants with v7 (time-ordered) GUIDs, whereas
/// production tenants are seeded with v4 (random) GUIDs. Parsing the GUID version
/// lets us filter harness-only collections safely.
/// </remarks>
internal static class OrphanCollectionFilter
{
    private const string Prefix = "tenant_";

    public static bool TryParseHarnessCollectionAge(
        string collectionName, out DateTimeOffset createdAt)
    {
        createdAt = default;
        if (!collectionName.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var hex = collectionName[Prefix.Length..];
        if (!Guid.TryParseExact(hex, "N", out var guid)) return false;

        var bytes = guid.ToByteArray(bigEndian: true);
        var version = (bytes[6] & 0xF0) >> 4;
        if (version != 7) return false;

        long ms =
            ((long)bytes[0] << 40) |
            ((long)bytes[1] << 32) |
            ((long)bytes[2] << 24) |
            ((long)bytes[3] << 16) |
            ((long)bytes[4] << 8)  |
             (long)bytes[5];
        createdAt = DateTimeOffset.FromUnixTimeMilliseconds(ms);
        return true;
    }
}
