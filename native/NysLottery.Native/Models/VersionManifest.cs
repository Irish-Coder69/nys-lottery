namespace NysLottery.Native.Models;

public sealed class VersionManifest
{
    public string? Version { get; set; }
    public string? ReleaseDate { get; set; }
    public string? Notes { get; set; }
    public string? DownloadUrl { get; set; }
    public string? MinimumSupportedVersion { get; set; }
}
