using LiteDB;

namespace PlexBotPlaylists.Models;

/// <summary>Represents a user-created playlist stored in LiteDB</summary>
public class UserPlaylist
{
    /// <summary>LiteDB document ID</summary>
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    /// <summary>Discord guild (server) this playlist belongs to</summary>
    public ulong GuildId { get; set; }

    /// <summary>Discord user ID of the playlist owner</summary>
    public ulong OwnerId { get; set; }

    /// <summary>Display name of the playlist</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Tracks in this playlist</summary>
    public List<PlaylistTrack> Tracks { get; set; } = [];

    /// <summary>When the playlist was created</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the playlist was last modified</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Whether this playlist is visible to other users in autocomplete</summary>
    public bool IsPublic { get; set; } = true;

    public int TrackCount => Tracks.Count;
}

/// <summary>A track entry within a user playlist</summary>
public class PlaylistTrack
{
    /// <summary>Track display title</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Artist name</summary>
    public string Artist { get; set; } = string.Empty;

    /// <summary>Album name</summary>
    public string Album { get; set; } = string.Empty;

    /// <summary>Direct streaming URL for Lavalink resolution</summary>
    public string PlaybackUrl { get; set; } = string.Empty;

    /// <summary>Album artwork URL</summary>
    public string ArtworkUrl { get; set; } = string.Empty;

    /// <summary>Track duration in milliseconds</summary>
    public long DurationMs { get; set; }

    /// <summary>Formatted duration string (e.g., "3:45")</summary>
    public string DurationDisplay { get; set; } = string.Empty;

    /// <summary>Origin system (plex, external, etc.)</summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>Provider-specific key for re-fetching track details</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>When this track was added to the playlist</summary>
    public DateTimeOffset AddedAt { get; set; }
}
