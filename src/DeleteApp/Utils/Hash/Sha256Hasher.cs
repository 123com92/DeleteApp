using System.Security.Cryptography;
using System.Text;

namespace DeleteApp.Utils.Hash;

public static class Sha256Hasher
{
    public static string HashFile(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        var sb = new StringBuilder(hash.Length * 2);

        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}
