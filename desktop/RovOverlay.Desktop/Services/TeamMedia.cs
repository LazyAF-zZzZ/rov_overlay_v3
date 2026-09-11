using System.IO;
using RovOverlay.Desktop.Models;

namespace RovOverlay.Desktop.Services;

// Team logos: where to show them from and how to upload one.
public static class TeamMedia
{
    // Same limit and types as the server (backend/server/domain/media.ts).
    public const long MaxBytes = 4 * 1024 * 1024;

    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp"
    };

    // The version stamp in the query string changes on every upload, so a new logo is
    // never hidden behind a cached old one.
    public static string? LogoUrl(AppServices services, Team team) =>
        team.Logo is { V: > 0, Ext: { Length: > 0 } ext }
            ? services.Url($"/images/team-logos/{Uri.EscapeDataString(team.Id)}.{ext}?v={team.Logo.V}")
            : null;

    public static string Initials(Team team)
    {
        var source = (string.IsNullOrWhiteSpace(team.Tag) ? team.Name : team.Tag).Trim();
        if (source.Length == 0) return "?";
        return (source.Length > 3 ? source[..3] : source).ToUpperInvariant();
    }

    public static async Task UploadLogoAsync(ApiClient api, string teamId, string path)
    {
        if (!Types.TryGetValue(Path.GetExtension(path), out var type))
            throw new ApiException(Loc.T("Team.LogoType"), 415);
        if (new FileInfo(path).Length > MaxBytes)
            throw new ApiException(Loc.T("Team.LogoTooBig"), 413);

        var bytes = await File.ReadAllBytesAsync(path);
        await api.PostBytesAsync<TeamReply>($"/api/teams/{Uri.EscapeDataString(teamId)}/logo", bytes, type);
    }
}
