using PlexBot.Core.Models;
using PlexBot.Core.Models.Media;
using PlexBot.Core.Services.Music;
using PlexBotPlaylists.Models;

namespace PlexBotPlaylists.Services;

/// <summary>Music provider that exposes custom user playlists to the bot's provider system.
/// This allows custom playlists to appear in autocomplete and be played via /playlist.</summary>
public class CustomPlaylistProvider(PlaylistService playlistService) : IMusicProvider
{
    // Note: This provider is guild/user-scoped at query time via context,
    // but the interface doesn't pass user context. We return ALL public playlists
    // for autocomplete and filter by playlist ID for playback.

    public string Id => "playlists";
    public string DisplayName => "Custom Playlists";
    public bool IsAvailable => true;
    public int Priority => 100; // Lower priority than Plex
    public MusicProviderCapabilities Capabilities => MusicProviderCapabilities.Playlists;

    public Task<SearchResults> SearchAsync(string query, CancellationToken cancellationToken = default)
        => Task.FromResult(new SearchResults { Query = query, SourceSystem = "custom" });

    public Task<Track?> GetTrackDetailsAsync(string trackKey, CancellationToken cancellationToken = default)
        => Task.FromResult<Track?>(null);

    public Task<List<Track>> GetTracksAsync(string containerKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Track>());

    public Task<List<Album>> GetAlbumsAsync(string artistKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Album>());

    public Task<List<Track>> GetAllArtistTracksAsync(string artistKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<Track>());

    /// <summary>Returns all public playlists across all guilds.
    /// The autocomplete handler filters by guild ID from interaction context.</summary>
    public Task<List<Playlist>> GetPlaylistsAsync(CancellationToken cancellationToken = default)
    {
        // Return all playlists - the autocomplete handler has guild context and filters appropriately.
        // This isn't ideal but the IMusicProvider interface doesn't support guild-scoped queries.
        // For small playlist counts this is fine; for scale we'd need an extended interface.
        List<UserPlaylist> allPublic = playlistService.GetAllPublicPlaylists();
        List<Playlist> result = allPublic.Select(PlaylistService.ToPlaylistModel).ToList();
        return Task.FromResult(result);
    }

    /// <summary>Get playlist details by custom:id key</summary>
    public Task<Playlist?> GetPlaylistDetailsAsync(string playlistKey, CancellationToken cancellationToken = default)
    {
        // Strip the "custom:" prefix if present
        string id = playlistKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase)
            ? playlistKey[7..]
            : playlistKey;

        UserPlaylist? userPlaylist = playlistService.GetPlaylist(id);
        if (userPlaylist is null)
            return Task.FromResult<Playlist?>(null);

        return Task.FromResult<Playlist?>(PlaylistService.ToPlaylistModel(userPlaylist));
    }
}
