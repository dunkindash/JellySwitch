<div align="center">

# JellySwitch — User Switcher & Quick Connect Helper for Jellyfin

Admin-only tools to impersonate users and authorize Quick Connect codes.

Supports Jellyfin 10.10.x

</div>

---

## Features

- Impersonate any user in a new tab (ephemeral session) via official Quick Connect
- Authorize a 6-character Quick Connect code for any user
- Admin-only access; all actions are logged

## Install via Plugin Catalog

Add this repository in Jellyfin:

1. Dashboard → Plugins → Repositories → Add
2. Name: JellySwitch
3. URL: https://dunkindash.github.io/JellySwitch/manifest.json
4. Plugins → Catalog → search "User Switcher" → Install
5. Restart Jellyfin

Notes:
- Enable GitHub Pages (Settings → Pages → Build from main, folder: /docs)
- Releases automatically update `docs/manifest.json`

<!-- Manual install no longer recommended; install via catalog URL above. -->

## Configuration & Usage

- Dashboard → Plugins → User Switcher
- Search for a user, click Impersonate to open a new tab as that user
- Enter a device Quick Connect code + select a user → Authorize

## Development

Requirements: .NET 8 SDK, Jellyfin 10.10.x

Build:
```bash
 dotnet restore src/UserSwitcher.csproj
 dotnet publish src/UserSwitcher.csproj -c Release -o artifacts/publish
```
Local test: copy `artifacts/publish/` to your Jellyfin plugins folder under `TechBrew.UserSwitcher/` and restart.

## Release Automation

- Merge PRs into `main` (protected branch). Label PR with `release:minor` or `release:major` to bump version; default is patch.
- Auto Tag workflow creates `vX.Y.Z` tag on merge.
- Release workflow builds the zip and publishes `docs/manifest.json` for the catalog.

## Security

- Endpoints require Admin and enforce per-request checks
- Actions logged with admin, target user, timestamp

---

For the detailed product requirements, see `docs/PRD.md`.
