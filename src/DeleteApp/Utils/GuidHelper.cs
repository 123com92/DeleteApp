using System.Security.Cryptography;
using System.Text;

namespace DeleteApp.Utils;

public static class GuidHelper
{
    public static Guid Deterministic(string prefix, string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(prefix + ":" + input));
        return new Guid(hash);
    }
}
