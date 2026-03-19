using LiteDB;
using PlexBotPlaylists.Models;

namespace PlexBotPlaylists.Data;

/// <summary>LiteDB repository for user playlist CRUD operations</summary>
public class PlaylistRepository : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<UserPlaylist> _playlists;

    public PlaylistRepository(string dbPath)
    {
        string? directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _db = new LiteDatabase(dbPath);
        _playlists = _db.GetCollection<UserPlaylist>("playlists");

        // Create indexes for common queries
        _playlists.EnsureIndex(x => x.GuildId);
        _playlists.EnsureIndex(x => x.OwnerId);
        _playlists.EnsureIndex(x => x.Name);
    }

    /// <summary>Get all playlists owned by a user in a guild</summary>
    public List<UserPlaylist> GetUserPlaylists(ulong guildId, ulong userId)
    {
        return _playlists.Query()
            .Where(p => p.GuildId == guildId && p.OwnerId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToList();
    }

    /// <summary>Get all public playlists in a guild (for autocomplete)</summary>
    public List<UserPlaylist> GetPublicPlaylists(ulong guildId)
    {
        return _playlists.Query()
            .Where(p => p.GuildId == guildId && p.IsPublic)
            .OrderByDescending(p => p.UpdatedAt)
            .ToList();
    }

    /// <summary>Get all public playlists across all guilds</summary>
    public List<UserPlaylist> GetAllPublicPlaylists()
    {
        return _playlists.Query()
            .Where(p => p.IsPublic)
            .OrderByDescending(p => p.UpdatedAt)
            .Limit(100)
            .ToList();
    }

    /// <summary>Get all playlists visible to a user (own + public) in a guild</summary>
    public List<UserPlaylist> GetVisiblePlaylists(ulong guildId, ulong userId)
    {
        return _playlists.Query()
            .Where(p => p.GuildId == guildId && (p.OwnerId == userId || p.IsPublic))
            .OrderByDescending(p => p.UpdatedAt)
            .ToList();
    }

    /// <summary>Get a playlist by its ID</summary>
    public UserPlaylist? GetById(ObjectId id)
    {
        return _playlists.FindById(id);
    }

    /// <summary>Get a playlist by ID string</summary>
    public UserPlaylist? GetById(string id)
    {
        try
        {
            ObjectId objectId = new(id);
            return _playlists.FindById(objectId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Count playlists owned by a user in a guild</summary>
    public int CountUserPlaylists(ulong guildId, ulong userId)
    {
        return _playlists.Count(p => p.GuildId == guildId && p.OwnerId == userId);
    }

    /// <summary>Check if a user already has a playlist with the given name in a guild</summary>
    public bool NameExists(ulong guildId, ulong userId, string name)
    {
        return _playlists.Exists(p =>
            p.GuildId == guildId &&
            p.OwnerId == userId &&
            p.Name == name);
    }

    /// <summary>Insert a new playlist</summary>
    public ObjectId Insert(UserPlaylist playlist)
    {
        return _playlists.Insert(playlist).AsObjectId;
    }

    /// <summary>Update an existing playlist</summary>
    public bool Update(UserPlaylist playlist)
    {
        playlist.UpdatedAt = DateTimeOffset.UtcNow;
        return _playlists.Update(playlist);
    }

    /// <summary>Delete a playlist by ID</summary>
    public bool Delete(ObjectId id)
    {
        return _playlists.Delete(id);
    }

    /// <summary>Delete a playlist by ID string</summary>
    public bool Delete(string id)
    {
        try
        {
            ObjectId objectId = new(id);
            return _playlists.Delete(objectId);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
