using System.Net.Http;
using System.Text.Json;
using NysLottery.Native.Models;

namespace NysLottery.Native.Services;

public sealed class VersionManifestService
{
    private static readonly HttpClient Http = new();

    public async Task<VersionManifest?> GetLatestAsync(string manifestUrl, CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(manifestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<VersionManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public static bool IsNewer(string? remoteVersion, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(remoteVersion))
        {
            return false;
        }

        return CompareVersions(remoteVersion, currentVersion) > 0;
    }

    private static int CompareVersions(string a, string b)
    {
        var aParts = a.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var bParts = b.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var length = Math.Max(aParts.Length, bParts.Length);

        for (var i = 0; i < length; i++)
        {
            var av = i < aParts.Length ? aParts[i] : 0;
            var bv = i < bParts.Length ? bParts[i] : 0;

            if (av > bv)
            {
                return 1;
            }

            if (av < bv)
            {
                return -1;
            }
        }

        return 0;
    }
}
