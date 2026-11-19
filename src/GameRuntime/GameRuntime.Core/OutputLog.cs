using System.Security.Cryptography;
using System.Text;

namespace GameRuntime;

/// <summary>The game's output: one line per event, hashed as it is written so two runs can be compared by one value.</summary>
public sealed class OutputLog(TextWriter writer)
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    /// <summary>Writes a line that counts toward <see cref="Finish"/>.</summary>
    public void Line(string text)
    {
        writer.WriteLine(text);
        _hash.AppendData(Encoding.UTF8.GetBytes(text + "\n"));
    }

    /// <summary>Writes the hash of every line so far and returns it. Nothing may be logged afterwards.</summary>
    public string Finish()
    {
        var value = Convert.ToHexString(_hash.GetHashAndReset());
        writer.WriteLine("output-sha256=" + value);
        return value;
    }
}
