using Discord.WebSocket;
using PlexBot.Core.Models.Media;
using PlexBot.Utils;
using PlexBotPlaylists.Data;
using PlexBotPlaylists.Models;

namespace PlexBotPlaylists.Services;

/// <summary>Business logic for user playlist management including limits, validation, and model conversion</summary>
public class PlaylistService(PlaylistRepository repository, string extensionId)
{
    private readonly string _extensionId = extensionId;

    /// <summary>Get all playlists owned by a user in a guild</summary>
    public List<UserPlaylist> GetUserPlaylists(ulong guildId, ulong userId)
        => repository.GetUserPlaylists(guildId, userId);

    /// <summary>Get all playlists visible to a user (own + public) for autocomplete</summary>
    public List<UserPlaylist> GetVisiblePlaylists(ulong guildId, ulong userId)
        => repository.GetVisiblePlaylists(guildId, userId);

    /// <summary>Get a playlist by its ID string</summary>
    public UserPlaylist? GetPlaylist(string id) => repository.GetById(id);

    /// <summary>Create a new playlist, enforcing limits</summary>
    /// <returns>The created playlist, or null if limit reached or name exists</returns>
    public (UserPlaylist? Playlist, string? Error) CreatePlaylist(
        ulong guildId, ulong userId, SocketGuildUser user, string name, string description = "", bool isPublic = true)
    {
        // Check name uniqueness
        if (repository.NameExists(guildId, userId, name))
            return (null, $"You already have a playlist named **{name}**.");

        // Check limits
        int limit = GetPlaylistLimit(user);
        if (limit > 0)
        {
            int currentCount = repository.CountUserPlaylists(guildId, userId);
            if (currentCount >= limit)
                return (null, $"You've reached your playlist limit ({limit}). Delete a playlist to create a new one.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        UserPlaylist playlist = new()
        {
            GuildId = guildId,
            OwnerId = userId,
            Name = name.Trim(),
            Description = description.Trim(),
            IsPublic = isPublic,
            CreatedAt = now,
            UpdatedAt = now
        };

        repository.Insert(playlist);
        Logs.Info($"Playlist created: '{name}' by user {userId} in guild {guildId}");
        return (playlist, null);
    }

    /// <summary>Delete a playlist (only by owner)</summary>
    public (bool Success, string? Error) DeletePlaylist(string playlistId, ulong userId)
    {
        UserPlaylist? playlist = repository.GetById(playlistId);
        if (playlist is null)
            return (false, "Playlist not found.");
        if (playlist.OwnerId != userId)
            return (false, "You can only delete your own playlists.");

        repository.Delete(playlist.Id);
        Logs.Info($"Playlist deleted: '{playlist.Name}' by user {userId}");
        return (true, null);
    }

    /// <summary>Update playlist name/description</summary>
    public (bool Success, string? Error) UpdatePlaylist(string playlistId, ulong userId, string? name, string? description, bool? isPublic)
    {
        UserPlaylist? playlist = repository.GetById(playlistId);
        if (playlist is null)
            return (false, "Playlist not found.");
        if (playlist.OwnerId != userId)
            return (false, "You can only edit your own playlists.");

        if (name is not null)
        {
            string trimmed = name.Trim();
            if (trimmed != playlist.Name && repository.NameExists(playlist.GuildId, userId, trimmed))
                return (false, $"You already have a playlist named **{trimmed}**.");
            playlist.Name = trimmed;
        }
        if (description is not null)
            playlist.Description = description.Trim();
        if (isPublic.HasValue)
            playlist.IsPublic = isPublic.Value;

        repository.Update(playlist);
        return (true, null);
    }

    /// <summary>Add a track to a playlist</summary>
    public (bool Success, string? Error) AddTrack(string playlistId, ulong userId, Track track)
    {
        UserPlaylist? playlist = repository.GetById(playlistId);
        if (playlist is null)
            return (false, "Playlist not found.");
        if (playlist.OwnerId != userId)
            return (false, "You can only add tracks to your own playlists.");

        // Check track limit
        int maxTracks = GetMaxTracksPerPlaylist();
        if (maxTracks > 0 && playlist.Tracks.Count >= maxTracks)
            return (false, $"This playlist has reached the track limit ({maxTracks}).");

        // Check for duplicates by playback URL
        if (playlist.Tracks.Any(t => t.PlaybackUrl == track.PlaybackUrl))
            return (false, $"**{track.Title}** is already in this playlist.");

        playlist.Tracks.Add(new PlaylistTrack
        {
            Title = track.Title,
            Artist = track.Artist,
            Album = track.Album,
            PlaybackUrl = track.PlaybackUrl,
            ArtworkUrl = track.ArtworkUrl,
            DurationMs = track.DurationMs,
            DurationDisplay = track.DurationDisplay,
            SourceSystem = track.SourceSystem,
            SourceKey = track.SourceKey,
            AddedAt = DateTimeOffset.UtcNow
        });

        repository.Update(playlist);
        Logs.Debug($"Track '{track.Title}' added to playlist '{playlist.Name}'");
        return (true, null);
    }

    /// <summary>Remove a track from a playlist by index</summary>
    public (bool Success, string? Error) RemoveTrack(string playlistId, ulong userId, int trackIndex)
    {
        UserPlaylist? playlist = repository.GetById(playlistId);
        if (playlist is null)
            return (false, "Playlist not found.");
        if (playlist.OwnerId != userId)
            return (false, "You can only remove tracks from your own playlists.");
        if (trackIndex < 0 || trackIndex >= playlist.Tracks.Count)
            return (false, "Invalid track index.");

        string removedTitle = playlist.Tracks[trackIndex].Title;
        playlist.Tracks.RemoveAt(trackIndex);
        repository.Update(playlist);
        Logs.Debug($"Track '{removedTitle}' removed from playlist '{playlist.Name}'");
        return (true, null);
    }

    /// <summary>Get all public playlists across all guilds (for provider interface)</summary>
    public List<UserPlaylist> GetAllPublicPlaylists()
        => repository.GetAllPublicPlaylists();

    /// <summary>Convert a UserPlaylist to the core Playlist model for playback</summary>
    public static Playlist ToPlaylistModel(UserPlaylist userPlaylist)
    {
        return new Playlist
        {
            Id = userPlaylist.Id.ToString(),
            Title = userPlaylist.Name,
            Description = userPlaylist.Description,
            TrackCount = userPlaylist.TrackCount,
            SourceSystem = "custom",
            SourceKey = $"custom:{userPlaylist.Id}",
            CreatedBy = userPlaylist.OwnerId.ToString(),
            CreatedAt = userPlaylist.CreatedAt,
            UpdatedAt = userPlaylist.UpdatedAt,
            Tracks = userPlaylist.Tracks.Select(ToTrackModel).ToList()
        };
    }

    /// <summary>Convert a PlaylistTrack to the core Track model</summary>
    public static Track ToTrackModel(PlaylistTrack pt)
    {
        return new Track
        {
            Id = pt.SourceKey,
            Title = pt.Title,
            Artist = pt.Artist,
            Album = pt.Album,
            PlaybackUrl = pt.PlaybackUrl,
            ArtworkUrl = pt.ArtworkUrl,
            DurationMs = pt.DurationMs,
            DurationDisplay = pt.DurationDisplay,
            SourceSystem = pt.SourceSystem,
            SourceKey = pt.SourceKey
        };
    }

    /// <summary>Get the playlist limit for a user based on their roles</summary>
    public int GetPlaylistLimit(SocketGuildUser user)
    {
        // Server admins are unlimited
        if (user.GuildPermissions.ManageGuild || user.GuildPermissions.Administrator)
            return 0; // 0 = unlimited

        // Check role overrides — take the highest limit
        int highestLimit = -1;
        foreach (SocketRole role in user.Roles)
        {
            int roleLimit = BotConfig.GetInt($"extensions.{_extensionId}.roles.{role.Id}", -1);
            if (roleLimit >= 0 && roleLimit > highestLimit)
                highestLimit = roleLimit;
        }

        // If a role override was found, use it
        if (highestLimit >= 0)
            return highestLimit;

        // Fall back to default limit
        return BotConfig.GetInt($"extensions.{_extensionId}.defaultLimit", 5);
    }

    /// <summary>Get the count of playlists a user currently has</summary>
    public int GetUserPlaylistCount(ulong guildId, ulong userId)
        => repository.CountUserPlaylists(guildId, userId);

    private int GetMaxTracksPerPlaylist()
        => BotConfig.GetInt($"extensions.{_extensionId}.maxTracksPerPlaylist", 100);
}
