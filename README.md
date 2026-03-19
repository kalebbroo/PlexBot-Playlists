# PlexBot-Playlists

A [PlexBot](https://github.com/your-org/PlexBot) extension that lets Discord users create, manage, and play custom playlists.

## Features

- **Interactive UI** &mdash; Single `/playlists` command opens a rich panel with select menus, modals, and buttons
- **Save from Player** &mdash; A save button on the visual player lets users add the currently playing track to any playlist
- **Role-Based Limits** &mdash; Configure max playlists per user with per-role overrides in `config.fds`
- **Autocomplete Integration** &mdash; Custom playlists appear alongside Plex playlists in the `/playlist` autocomplete
- **Persistent Storage** &mdash; Playlists stored in a LiteDB embedded database, no external services needed
- **Public/Private** &mdash; Playlists can be marked public (visible to all in autocomplete) or private (owner only)
- **Duplicate Detection** &mdash; Prevents adding the same track twice to a playlist

## Installation

1. Clone or download this repository into your PlexBot `Extensions/` directory:
   ```
   cd your-plexbot/Extensions
   git clone https://github.com/your-org/PlexBot-Playlists.git
   ```

2. Add the configuration to your `config.fds`:
   ```yaml
   extensions:
       playlists:
           defaultLimit: 5
           maxTracksPerPlaylist: 100
           dbPath: Data/playlists.db
           roles:
               # Discord role ID: max playlists (0 = unlimited)
               # 123456789012345678: 10
               # 987654321098765432: 0
   ```

3. Restart PlexBot. The extension is automatically discovered, built, and loaded.

## Commands

### `/playlists`
Opens your playlist management panel (ephemeral). From here you can:

- **Create** &mdash; Opens a modal to set name, description, and visibility
- **Select a playlist** &mdash; View tracks, play, shuffle, edit, delete, or remove tracks
- **Pagination** &mdash; Browse through tracks 10 at a time

### Visual Player Save Button
When a track is playing, a save button appears on the player. Click it to choose which playlist to save the current track to.

### `/playlist` Integration
Custom playlists appear in the existing `/playlist` autocomplete dropdown prefixed with `[Custom]`. Select one to play it directly, just like a Plex playlist.

## Limits

| Who | Default |
|---|---|
| Regular users | 5 playlists (configurable via `defaultLimit`) |
| Role overrides | Set per Discord role ID in `roles` section |
| Server admins | Unlimited (ManageGuild or Administrator permission) |
| Tracks per playlist | 100 (configurable via `maxTracksPerPlaylist`) |

Set any limit to `0` for unlimited.

## Configuration Reference

All settings live under `extensions.playlists` in your `config.fds`:

| Key | Type | Default | Description |
|---|---|---|---|
| `defaultLimit` | int | `5` | Max playlists per user (0 = unlimited) |
| `maxTracksPerPlaylist` | int | `100` | Max tracks per playlist (0 = unlimited) |
| `dbPath` | string | `Data/playlists.db` | LiteDB database file path (relative to bot root) |
| `roles.<roleId>` | int | &mdash; | Override limit for a specific Discord role |

## Project Structure

```
PlexBot-Playlists/
├── PlaylistsExtension.cs              # Extension entry point
├── Commands/
│   └── PlaylistCommands.cs            # /playlists slash command
├── Interactions/
│   └── PlaylistInteractionHandler.cs  # Button, modal, select menu handlers
├── Services/
│   ├── PlaylistService.cs             # Business logic & limit enforcement
│   └── CustomPlaylistProvider.cs      # IMusicProvider for autocomplete/playback
├── Models/
│   └── UserPlaylist.cs                # LiteDB document models
└── Data/
    └── PlaylistRepository.cs          # LiteDB CRUD operations
```

## Requirements

- PlexBot 1.0.0+
- .NET 9.0

## License

[MIT](LICENSE)
