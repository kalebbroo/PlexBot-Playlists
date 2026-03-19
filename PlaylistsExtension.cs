using Discord;
using Microsoft.Extensions.DependencyInjection;
using PlexBot.Core.Discord.Embeds;
using PlexBot.Core.Extensions;
using PlexBot.Core.Services.LavaLink;
using PlexBot.Core.Services.Music;
using PlexBot.Utils;
using PlexBotPlaylists.Data;
using PlexBotPlaylists.Services;

namespace PlexBotPlaylists;

/// <summary>PlexBot extension that allows Discord users to create, manage, and play custom playlists</summary>
public class PlaylistsExtension : Extension
{
    public override string Id => "playlists";
    public override string Name => "PlexBot Playlists";
    public override string Version => "1.0.0";
    public override string Author => "PlexBot";
    public override string Description => "Create and manage custom playlists with role-based limits";

    public override void RegisterServices(IServiceCollection services)
    {
        string dbPath = GetConfig("dbPath", "Data/playlists.db");
        string fullDbPath = Path.IsPathRooted(dbPath)
            ? dbPath
            : Path.Combine(AppContext.BaseDirectory, dbPath);

        services.AddSingleton(new PlaylistRepository(fullDbPath));
        services.AddSingleton(sp => new PlaylistService(
            sp.GetRequiredService<PlaylistRepository>(), Id));
    }

    protected override Task<bool> OnInitializeAsync(IServiceProvider services)
    {
        // Register the custom playlist music provider
        PlaylistService playlistService = services.GetRequiredService<PlaylistService>();
        MusicProviderRegistry registry = services.GetRequiredService<MusicProviderRegistry>();
        registry.RegisterProvider(new CustomPlaylistProvider(playlistService));
        Logs.Info("Playlists extension: Registered custom playlist provider");

        // Register the "Save to Playlist" button on the visual player
        DiscordButtonBuilder buttonBuilder = services.GetRequiredService<DiscordButtonBuilder>();

        buttonBuilder.RegisterButton(
            id: "pl_save",
            flags: ButtonFlag.VisualPlayer,
            priority: 55, // After kill button (priority 70 is row 2, this fits on row 2)
            factory: _ => new ButtonBuilder()
                .WithEmote(new Emoji("\uD83D\uDCBE")) // 💾
                .WithCustomId("pl:save_current")
                .WithStyle(ButtonStyle.Secondary));

        Logs.Info("Playlists extension: Registered save-to-playlist button on visual player");
        return Task.FromResult(true);
    }

    public override Task ShutdownAsync()
    {
        Logs.Info("Playlists extension shutting down");
        return Task.CompletedTask;
    }
}
