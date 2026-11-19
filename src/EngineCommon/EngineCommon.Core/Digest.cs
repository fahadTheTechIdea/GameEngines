using System.Security.Cryptography;
using System.Text;

namespace EngineCommon;

/// <summary>The digest every engine save and document carries: SHA-256 of the UTF-8 bytes, upper-case hex.</summary>
public static class Digest
{
    public static string Of(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static string Of(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}