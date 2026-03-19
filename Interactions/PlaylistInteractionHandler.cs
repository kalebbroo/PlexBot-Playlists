using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using PlexBot.Core.Discord.Embeds;
using PlexBot.Core.Models.Media;
using PlexBot.Core.Services;
using PlexBot.Core.Services.LavaLink;
using PlexBot.Utils;
using PlexBotPlaylists.Commands;
using PlexBotPlaylists.Models;
using PlexBotPlaylists.Services;

namespace PlexBotPlaylists.Interactions;

/// <summary>Handles all button, select menu, and modal interactions for the playlists extension</summary>
public class PlaylistInteractionHandler(
    PlaylistService playlistService,
    IPlayerService playerService) : InteractionModuleBase<SocketInteractionContext>
{
    private const int TracksPerPage = 10;

    // ─── Playlist selection from main panel ───

    [ComponentInteraction("pl:select")]
    public async Task HandlePlaylistSelect(string[] values)
    {
        await DeferAsync();
        try
        {
            if (values.Length == 0) return;
            string playlistId = values[0];
            await ShowPlaylistDetails(playlistId, 1);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error in playlist select: {ex.Message}");
            await ModifyWithError("Failed to load playlist details.");
        }
    }

    // ─── Create playlist modal ───

    [ComponentInteraction("pl:create")]
    public async Task HandleCreateButton()
    {
        ModalBuilder modal = new ModalBuilder()
            .WithTitle("Create New Playlist")
            .WithCustomId("pl:create_submit")
            .AddTextInput("Name", "pl_name", TextInputStyle.Short,
                placeholder: "My Awesome Playlist", required: true, maxLength: 50)
            .AddTextInput("Description", "pl_desc", TextInputStyle.Paragraph,
                placeholder: "Optional description...", required: false, maxLength: 200)
            .AddTextInput("Public? (yes/no)", "pl_public", TextInputStyle.Short,
                placeholder: "yes", required: false, maxLength: 3, value: "yes");

        await RespondWithModalAsync(modal.Build());
    }

    [ModalInteraction("pl:create_submit")]
    public async Task HandleCreateSubmit(SocketModal modal)
    {
        await DeferAsync(ephemeral: true);
        try
        {
            var components = modal.Data.Components.ToList();
            string name = components.First(c => c.CustomId == "pl_name").Value?.Trim() ?? "";
            string description = components.FirstOrDefault(c => c.CustomId == "pl_desc")?.Value?.Trim() ?? "";
            string publicInput = components.FirstOrDefault(c => c.CustomId == "pl_public")?.Value?.Trim() ?? "yes";
            bool isPublic = publicInput.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                            publicInput.Equals("y", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(name))
            {
                await FollowupAsync(components: ComponentV2Builder.Error("Invalid Name", "Playlist name cannot be empty."), ephemeral: true);
                return;
            }

            SocketGuildUser guildUser = Context.Guild.GetUser(Context.User.Id);
            var (playlist, error) = playlistService.CreatePlaylist(
                Context.Guild.Id, Context.User.Id, guildUser, name, description, isPublic);

            if (error is not null)
            {
                await FollowupAsync(components: ComponentV2Builder.Error("Cannot Create", error), ephemeral: true);
                return;
            }

            await FollowupAsync(components: ComponentV2Builder.Success("Playlist Created",
                $"**{playlist!.Name}** has been created. Use the \uD83D\uDCBE button on the player to add tracks!"), ephemeral: true);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error creating playlist: {ex.Message}");
            await FollowupAsync(components: ComponentV2Builder.Error("Error", "Failed to create playlist."), ephemeral: true);
        }
    }

    // ─── Playlist detail view ───

    private async Task ShowPlaylistDetails(string playlistId, int page)
    {
        UserPlaylist? playlist = playlistService.GetPlaylist(playlistId);
        if (playlist is null)
        {
            await ModifyWithError("Playlist not found.");
            return;
        }

        bool isOwner = playlist.OwnerId == Context.User.Id;
        int totalPages = Math.Max(1, (playlist.Tracks.Count + TracksPerPage - 1) / TracksPerPage);
        page = Math.Clamp(page, 1, totalPages);

        // Build track list for current page
        StringBuilder trackList = new();
        if (playlist.Tracks.Count == 0)
        {
            trackList.Append("No tracks yet. Use the \uD83D\uDCBE button on the player to add tracks!");
        }
        else
        {
            int start = (page - 1) * TracksPerPage;
            int end = Math.Min(start + TracksPerPage, playlist.Tracks.Count);
            for (int i = start; i < end; i++)
            {
                PlaylistTrack t = playlist.Tracks[i];
                trackList.AppendLine($"**{i + 1}.** {t.Title} - {t.Artist} ({t.DurationDisplay})");
            }
        }

        // Buttons
        ComponentBuilder components = new();

        // Row 1: Play controls
        components.WithButton("Play", $"pl:play:{playlistId}:1", ButtonStyle.Success,
            emote: new Emoji("\u25B6\uFE0F"), row: 0);
        components.WithButton("Shuffle", $"pl:shuffle:{playlistId}:1", ButtonStyle.Primary,
            emote: new Emoji("\uD83D\uDD00"), row: 0);

        if (isOwner)
        {
            components.WithButton("Delete", $"pl:delete_confirm:{playlistId}", ButtonStyle.Danger,
                emote: new Emoji("\uD83D\uDDD1\uFE0F"), row: 0);
        }

        // Row 2: Management (owner only) + navigation
        if (isOwner)
        {
            components.WithButton("Edit", $"pl:edit:{playlistId}", ButtonStyle.Secondary,
                emote: new Emoji("\u270F\uFE0F"), row: 1);

            if (playlist.Tracks.Count > 0)
            {
                components.WithButton("Remove Track", $"pl:remove_menu:{playlistId}:{page}", ButtonStyle.Secondary,
                    emote: new Emoji("\u2796"), row: 1);
            }
        }

        components.WithButton("Back", "pl:back", ButtonStyle.Secondary,
            emote: new Emoji("\u2B05\uFE0F"), row: 1);

        // Row 3: Pagination (if needed)
        if (totalPages > 1)
        {
            components.WithButton("Prev", $"pl:page:{playlistId}:{Math.Max(1, page - 1)}",
                ButtonStyle.Secondary, disabled: page <= 1, row: 2);
            components.WithButton($"{page}/{totalPages}", "pl:noop", ButtonStyle.Secondary,
                disabled: true, row: 2);
            components.WithButton("Next", $"pl:page:{playlistId}:{Math.Min(totalPages, page + 1)}",
                ButtonStyle.Secondary, disabled: page >= totalPages, row: 2);
        }

        string subtitle = $"{playlist.TrackCount} tracks | Created {playlist.CreatedAt:MMM d, yyyy}";
        if (!playlist.IsPublic) subtitle += " | Private";

        MessageComponent cv2 = PlaylistCommands.BuildPlaylistPanel(
            playlist.Name, subtitle, trackList.ToString().TrimEnd(), components);

        await Context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = cv2;
            msg.Embed = null;
            msg.Flags = MessageFlags.ComponentsV2;
        });
    }

    // ─── Play / Shuffle ───

    [ComponentInteraction("pl:play:*:*")]
    public async Task HandlePlay(string playlistId, string pageStr)
    {
        await DeferAsync(ephemeral: true);
        await PlayPlaylist(playlistId, shuffle: false);
    }

    [ComponentInteraction("pl:shuffle:*:*")]
    public async Task HandleShuffle(string playlistId, string pageStr)
    {
        await DeferAsync(ephemeral: true);
        await PlayPlaylist(playlistId, shuffle: true);
    }

    private async Task PlayPlaylist(string playlistId, bool shuffle)
    {
        try
        {
            UserPlaylist? playlist = playlistService.GetPlaylist(playlistId);
            if (playlist is null)
            {
                await FollowupAsync(components: ComponentV2Builder.Error("Not Found", "Playlist not found."), ephemeral: true);
                return;
            }
            if (playlist.Tracks.Count == 0)
            {
                await FollowupAsync(components: ComponentV2Builder.Info("Empty", "This playlist has no tracks."), ephemeral: true);
                return;
            }

            List<Track> tracks = playlist.Tracks.Select(PlaylistService.ToTrackModel).ToList();
            if (shuffle)
            {
                Random rng = new();
                tracks = [.. tracks.OrderBy(_ => rng.Next())];
            }

            await playerService.AddToQueueAsync(Context.Interaction, tracks);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error playing playlist: {ex.Message}");
            await FollowupAsync(components: ComponentV2Builder.Error("Playback Error", "Failed to play playlist."), ephemeral: true);
        }
    }

    // ─── Delete with confirmation ───

    [ComponentInteraction("pl:delete_confirm:*")]
    public async Task HandleDeleteConfirm(string playlistId)
    {
        await DeferAsync();
        UserPlaylist? playlist = playlistService.GetPlaylist(playlistId);
        if (playlist is null)
        {
            await ModifyWithError("Playlist not found.");
            return;
        }

        ComponentBuilder components = new();
        components.WithButton("Yes, Delete", $"pl:delete:{playlistId}", ButtonStyle.Danger, row: 0);
        components.WithButton("Cancel", "pl:back", ButtonStyle.Secondary, row: 0);

        MessageComponent cv2 = PlaylistCommands.BuildPlaylistPanel(
            "Confirm Delete",
            $"Are you sure you want to delete **{playlist.Name}**?",
            $"This will permanently remove the playlist and its {playlist.TrackCount} tracks.",
            components);

        await Context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = cv2;
            msg.Embed = null;
            msg.Flags = MessageFlags.ComponentsV2;
        });
    }

    [ComponentInteraction("pl:delete:*")]
    public async Task HandleDelete(string playlistId)
    {
        await DeferAsync();
        try
        {
            var (success, error) = playlistService.DeletePlaylist(playlistId, Context.User.Id);
            if (!success)
            {
                await ModifyWithError(error ?? "Failed to delete playlist.");
                return;
            }

            // Return to main list after deletion
            await ReturnToMainPanel();
        }
        catch (Exception ex)
        {
            Logs.Error($"Error deleting playlist: {ex.Message}");
            await ModifyWithError("Failed to delete playlist.");
        }
    }

    // ─── Edit playlist modal ───

    [ComponentInteraction("pl:edit:*")]
    public async Task HandleEditButton(string playlistId)
    {
        UserPlaylist? playlist = playlistService.GetPlaylist(playlistId);
        if (playlist is null)
        {
            await DeferAsync();
            await ModifyWithError("Playlist not found.");
            return;
        }

        ModalBuilder modal = new ModalBuilder()
            .WithTitle("Edit Playlist")
            .WithCustomId($"pl:edit_submit:{playlistId}")
            .AddTextInput("Name", "pl_name", TextInputStyle.Short,
                value: playlist.Name, required: true, maxLength: 50)
            .AddTextInput("Description", "pl_desc", TextInputStyle.Paragraph,
                value: playlist.Description, required: false, maxLength: 200)
            .AddTextInput("Public? (yes/no)", "pl_public", TextInputStyle.Short,
                value: playlist.IsPublic ? "yes" : "no", required: false, maxLength: 3);

        await RespondWithModalAsync(modal.Build());
    }

    [ModalInteraction("pl:edit_submit:*")]
    public async Task HandleEditSubmit(string playlistId, SocketModal modal)
    {
        await DeferAsync(ephemeral: true);
        try
        {
            var components = modal.Data.Components.ToList();
            string name = components.First(c => c.CustomId == "pl_name").Value?.Trim() ?? "";
            string description = components.FirstOrDefault(c => c.CustomId == "pl_desc")?.Value?.Trim() ?? "";
            string publicInput = components.FirstOrDefault(c => c.CustomId == "pl_public")?.Value?.Trim() ?? "yes";
            bool isPublic = publicInput.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                            publicInput.Equals("y", StringComparison.OrdinalIgnoreCase);

            var (success, error) = playlistService.UpdatePlaylist(playlistId, Context.User.Id, name, description, isPublic);
            if (!success)
            {
                await FollowupAsync(components: ComponentV2Builder.Error("Update Failed", error ?? "Unknown error."), ephemeral: true);
                return;
            }

            await FollowupAsync(components: ComponentV2Builder.Success("Updated", $"Playlist **{name}** has been updated."), ephemeral: true);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error editing playlist: {ex.Message}");
            await FollowupAsync(components: ComponentV2Builder.Error("Error", "Failed to update playlist."), ephemeral: true);
        }
    }

    // ─── Remove track ───

    [ComponentInteraction("pl:remove_menu:*:*")]
    public async Task HandleRemoveMenu(string playlistId, string pageStr)
    {
        await DeferAsync();
        UserPlaylist? playlist = playlistService.GetPlaylist(playlistId);
        if (playlist is null)
        {
            await ModifyWithError("Playlist not found.");
            return;
        }

        int page = int.TryParse(pageStr, out int p) ? p : 1;
        int start = (page - 1) * TracksPerPage;
        int end = Math.Min(start + TracksPerPage, playlist.Tracks.Count);

        SelectMenuBuilder menu = new SelectMenuBuilder()
            .WithPlaceholder("Select a track to remove")
            .WithCustomId($"pl:remove_track:{playlistId}:{page}")
            .WithMinValues(1)
            .WithMaxValues(1);

        for (int i = start; i < end; i++)
        {
            PlaylistTrack t = playlist.Tracks[i];
            string label = $"{i + 1}. {t.Title}";
            if (label.Length > 100) label = label[..97] + "...";
            string desc = $"{t.Artist} ({t.DurationDisplay})";
            if (desc.Length > 100) desc = desc[..97] + "...";
            menu.AddOption(label, i.ToString(), desc);
        }

        ComponentBuilder components = new();
        components.WithSelectMenu(menu);
        components.WithButton("Cancel", $"pl:page:{playlistId}:{page}", ButtonStyle.Secondary, row: 1);

        MessageComponent cv2 = PlaylistCommands.BuildPlaylistPanel(
            "Remove Track", $"Select a track to remove from **{playlist.Name}**", null, components);

        await Context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = cv2;
            msg.Embed = null;
            msg.Flags = MessageFlags.ComponentsV2;
        });
    }

    [ComponentInteraction("pl:remove_track:*:*")]
    public async Task HandleRemoveTrack(string playlistId, string pageStr, string[] values)
    {
        await DeferAsync();
        try
        {
            if (values.Length == 0) return;
            int trackIndex = int.Parse(values[0]);

            var (success, error) = playlistService.RemoveTrack(playlistId, Context.User.Id, trackIndex);
            if (!success)
            {
                await ModifyWithError(error ?? "Failed to remove track.");
                return;
            }

            int page = int.TryParse(pageStr, out int p) ? p : 1;
            await ShowPlaylistDetails(playlistId, page);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error removing track: {ex.Message}");
            await ModifyWithError("Failed to remove track.");
        }
    }

    // ─── Pagination ───

    [ComponentInteraction("pl:page:*:*")]
    public async Task HandlePage(string playlistId, string pageStr)
    {
        await DeferAsync();
        int page = int.TryParse(pageStr, out int p) ? p : 1;
        await ShowPlaylistDetails(playlistId, page);
    }

    // ─── Back to main list ───

    [ComponentInteraction("pl:back")]
    public async Task HandleBack()
    {
        await DeferAsync();
        await ReturnToMainPanel();
    }

    // ─── No-op (for disabled page counter button) ───

    [ComponentInteraction("pl:noop")]
    public async Task HandleNoop()
    {
        await DeferAsync();
    }

    // ─── Save current track to playlist (from visual player button) ───

    [ComponentInteraction("pl:save_current")]
    public async Task HandleSaveCurrent()
    {
        await DeferAsync(ephemeral: true);
        try
        {
            // Get the current playing track
            if (await playerService.GetPlayerAsync(Context.Interaction, false) is not CustomLavaLinkPlayer player ||
                player.CurrentItem is not CustomTrackQueueItem currentItem)
            {
                await FollowupAsync(components: ComponentV2Builder.Error("No Track", "No track is currently playing."), ephemeral: true);
                return;
            }

            // Get user's playlists
            List<UserPlaylist> playlists = playlistService.GetUserPlaylists(Context.Guild.Id, Context.User.Id);
            if (playlists.Count == 0)
            {
                await FollowupAsync(components: ComponentV2Builder.Info("No Playlists",
                    "You don't have any playlists yet. Use `/playlists` to create one first!"), ephemeral: true);
                return;
            }

            // Build select menu of playlists
            SelectMenuBuilder menu = new SelectMenuBuilder()
                .WithPlaceholder($"Save \"{Truncate(currentItem.Title ?? "Track", 40)}\" to...")
                .WithCustomId("pl:save_to")
                .WithMinValues(1)
                .WithMaxValues(1);

            foreach (UserPlaylist pl in playlists.Take(25))
            {
                menu.AddOption(
                    Truncate(pl.Name, 100),
                    pl.Id.ToString(),
                    $"{pl.TrackCount} tracks");
            }

            ComponentBuilder components = new();
            components.WithSelectMenu(menu);

            await FollowupAsync(components: ComponentV2Builder.InfoWithComponents(
                "Save to Playlist",
                $"Choose a playlist for **{currentItem.Title}** by {currentItem.Artist}",
                components), ephemeral: true);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error showing save menu: {ex.Message}");
            await FollowupAsync(components: ComponentV2Builder.Error("Error", "Failed to load playlists."), ephemeral: true);
        }
    }

    [ComponentInteraction("pl:save_to")]
    public async Task HandleSaveTo(string[] values)
    {
        await DeferAsync(ephemeral: true);
        try
        {
            if (values.Length == 0) return;
            string playlistId = values[0];

            // Get the current playing track
            if (await playerService.GetPlayerAsync(Context.Interaction, false) is not CustomLavaLinkPlayer player ||
                player.CurrentItem is not CustomTrackQueueItem currentItem)
            {
                await FollowupAsync(components: ComponentV2Builder.Error("No Track", "No track is currently playing."), ephemeral: true);
                return;
            }

            // Convert to Track model
            Track track = new()
            {
                Id = currentItem.SourceTrack.Id,
                Title = currentItem.Title ?? "Unknown",
                Artist = currentItem.Artist ?? "Unknown",
                Album = currentItem.Album ?? "",
                PlaybackUrl = currentItem.Url ?? "",
                ArtworkUrl = currentItem.Artwork ?? "",
                DurationMs = currentItem.SourceTrack.DurationMs,
                DurationDisplay = currentItem.Duration ?? "",
                SourceSystem = currentItem.SourceTrack.SourceSystem,
                SourceKey = currentItem.SourceTrack.SourceKey
            };

            var (success, error) = playlistService.AddTrack(playlistId, Context.User.Id, track);
            if (!success)
            {
                await FollowupAsync(components: ComponentV2Builder.Error("Cannot Add", error ?? "Unknown error."), ephemeral: true);
                return;
            }

            UserPlaylist? playlist = playlistService.GetPlaylist(playlistId);
            await FollowupAsync(components: ComponentV2Builder.Success("Track Saved",
                $"**{track.Title}** added to **{playlist?.Name ?? "playlist"}**"), ephemeral: true);
        }
        catch (Exception ex)
        {
            Logs.Error($"Error saving track: {ex.Message}");
            await FollowupAsync(components: ComponentV2Builder.Error("Error", "Failed to save track to playlist."), ephemeral: true);
        }
    }

    // ─── Helpers ───

    private async Task ReturnToMainPanel()
    {
        ulong guildId = Context.Guild.Id;
        ulong userId = Context.User.Id;
        SocketGuildUser guildUser = Context.Guild.GetUser(userId);

        List<UserPlaylist> playlists = playlistService.GetUserPlaylists(guildId, userId);
        int limit = playlistService.GetPlaylistLimit(guildUser);
        int count = playlists.Count;
        string limitText = limit == 0 ? "unlimited" : $"{count} of {limit}";

        ComponentBuilder components = new();

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

        bool canCreate = limit == 0 || count < limit;
        components.WithButton("Create New", "pl:create", ButtonStyle.Success,
            emote: new Emoji("\u2795"), disabled: !canCreate, row: 1);

        MessageComponent cv2 = PlaylistCommands.BuildPlaylistPanel(
            "Your Playlists",
            $"You have **{limitText}** playlists",
            playlists.Count == 0 ? "No playlists yet. Create one to get started!" : null,
            components);

        await Context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = cv2;
            msg.Embed = null;
            msg.Flags = MessageFlags.ComponentsV2;
        });
    }

    private async Task ModifyWithError(string message)
    {
        await Context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = ComponentV2Builder.Error("Error", message);
            msg.Embed = null;
            msg.Flags = MessageFlags.ComponentsV2;
        });
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        return text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength - 3), "...");
    }
}
