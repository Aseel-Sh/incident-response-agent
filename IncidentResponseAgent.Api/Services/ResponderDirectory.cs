using System.Security.Claims;
using System.Text.Json;
using IncidentResponseAgent.Infrastructure.Incidents;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Api.Services;

public sealed record ResponderProfile(string Id, string Name, string? Picture, string? AuditName = null);

// A directory of actual authenticated responders, never an authority for granting access.
public sealed class ResponderDirectory(IOptions<IncidentStorageOptions> options)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(
        string.IsNullOrWhiteSpace(options.Value.IncidentRecordsPath) ? Path.Combine(AppContext.BaseDirectory, "Data", "incidents.json") : options.Value.IncidentRecordsPath))!, "responders.json");

    public async Task<ResponderProfile?> RegisterAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is null && user.Identity?.AuthenticationType == "IncidentDevelopment") id = "development:" + user.Identity.Name;
        if (string.IsNullOrWhiteSpace(id)) return null;
        var picture = user.FindFirstValue("picture");
        if (!Uri.TryCreate(picture, UriKind.Absolute, out var uri) || uri.Scheme != "https") picture = null;
        var profile = new ResponderProfile(id, ResponderDisplayName.FromClaims(user), picture, user.Identity?.Name);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var profiles = await ReadAsync(cancellationToken);
            if (profiles.Find(item => item.Id == id) == profile) return profile;
            profiles.RemoveAll(item => item.Id == id);
            profiles.Add(profile);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(profiles), cancellationToken);
            File.Move(temporary, _path, true);
            return profile;
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<ResponderProfile>> ListAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return (await ReadAsync(cancellationToken)).OrderBy(item => item.Name).ToArray(); }
        finally { _gate.Release(); }
    }

    private async Task<List<ResponderProfile>> ReadAsync(CancellationToken cancellationToken) =>
        File.Exists(_path) ? JsonSerializer.Deserialize<List<ResponderProfile>>(await File.ReadAllTextAsync(_path, cancellationToken)) ?? [] : [];
}
