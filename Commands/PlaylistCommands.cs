using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using PlexBot.Core.Discord.Embeds;
using PlexBot.Utils;
using PlexBotPlaylists.Models;
using PlexBotPlaylists.Services;

namespace PlexBotPlaylists.Commands;

/// <summary>Slash command entry point for the playlists extension</summary>
public class PlaylistCommands(PlaylistService playlistService) : InteractionModuleBase<SocketInteractionContext>
{
    /// <summary>Opens the interactive playlists management panel</summary>
    [SlashCommand("playlists", "Create and manage your custom playlists")]
    public async Task PlaylistsCommand()
    {
        await DeferAsync(ephemeral: true);
        try
        {
            ulong guildId = Context.Guild.Id;
            ulong userId = Context.User.Id;
            SocketGuildUser guildUser = Context.Guild.GetUser(userId);

            List<UserPlaylist> playlists = playlistService.GetUserPlaylists(guildId, userId);
            int limit = playlistService.GetPlaylistLimit(guildUser);
            int count = playlists.Count;

            string limitText = limit == 0 ? "unlimited" : $"{count} of {limit}";

            // Build the main panel
            ComponentBuilder components = new();

            // Select menu of existing playlists
            if (playlists.Count > 0)
            {
                SelectMenuBuilder menu = new SelectMenuBuilder()
                    .WithPlaceholder("Select a playlist to manage")
                    .WithCustomId("pl:select")
                    .WithMinValues(1)
                    .WithMaxValues(1);

                foreach (UserPlaylist pl in playlists.Take(25))
                {
                    string desc = pl.TrackCount == 1 ? "1 track" : $"{pl.TrackCount} tracks";
                    if (!string.IsNullOrEmpty(pl.Description))
                        desc += $" | {Truncate(pl.Description, 60)}";
                    menu.AddOption(Truncate(pl.Name, 100), pl.Id.ToString(), desc);
                }

                components.WithSelectMenu(menu);
            }

            // Action buttons
            bool canCreate = limit == 0 || count < limit;
            components.WithButton("Create New", "pl:create", ButtonStyle.Success,
                emote: new Emoji("\u2795"), disabled: !canCreate, row: 1);

            MessageComponent cv2 = BuildPlaylistPanel(
                "Your Playlists",
                $"You have **{limitText}** playlists",
                playlists.Count == 0 ? "No playlists yet. Create one to get started!" : null,
                components);

            await FollowupAsync(components: cv2, ephemeral: true);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error in /playlists command: {ex.Message}");
            await FollowupAsync(components: ComponentV2Builder.Error("Error", "Failed to load playlists."), ephemeral: true);
        }
    }

    /// <summary>Builds a standard playlists panel using CV2</summary>
    internal static MessageComponent BuildPlaylistPanel(string title, string subtitle, string? body, ComponentBuilder components)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(new Color(138, 43, 226)) // MusicColor
            .WithTextDisplay($"## \uD83C\uDFB5 {title}")
            .WithTextDisplay(subtitle);

        if (!string.IsNullOrEmpty(body))
        {
            container.WithSeparator(SeparatorSpacingSize.Small, isDivider: true)
                .WithTextDisplay(body);
        }

        container.WithSeparator(SeparatorSpacingSize.Small, isDivider: true);

        // Add action rows from the component builder
        if (components.ActionRows != null)
        {
            foreach (ActionRowBuilder row in components.ActionRows)
                container.AddComponent(row);
        }

        return new ComponentBuilderV2().WithContainer(container).Build();
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength - 3), "...");
    }
}
