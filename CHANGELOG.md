# Changelog

All notable changes to this mod will be documented in this file.

## [0.1.1] — 2026-04-06

### Fixed
- HarmonyX property-lookup warnings for `SteamLobbyController.isLobbyOwner` and `lobbyID` — changed to field-only reflection access in `HostUpdatePlayerCount_Prefix` to eliminate warnings without altering behavior

### Changed
- `Reflect.cs`: added `GetFieldValue()` and `GetFieldBool()` helpers for field-only lookups that bypass property search and avoid HarmonyX warnings

## [0.1.0] — 2026-04-06

### Added
- Full Steam lobby player cap override (default 8, configurable 4–32)
- `SteamLobbyController.CreateLobby` transpiler — replaces hardcoded `4` with configurable max
- `SteamLobbyController.OnReceiveLobbyData` transpiler — replaces hardcoded `4` with configurable max
- `SteamLobbyController.HostUpdatePlayerCount` prefix — updates lobby joinability and `numPlayers` data based on configured cap
- `LobbyUtility.CanInvite` prefix — allows invites up to configured max instead of stock 4
- `JoinGameRow.Initialize` postfix — updates lobby browser UI text from `x/4` to `x/MaxPlayers`
- `JoinGameRow.OnJoinGame` prefix — blocks join attempt on version mismatch
- IL signature tracking for `CreateLobby` and `OnReceiveLobbyData` — detects game updates via pattern similarity and falls back gracefully
- Config file at `BepInEx/config/LumaPlayerLimit.cfg`
- `Reflect.cs` — centralized reflection helper for static/instance field/property/method access
- `TranspilerHelpers.cs` — IL stack simulation, pattern matching, and signature tracking for safe constant replacement
- `Core.cs` — shared `PatchNamedMethod` helper

### Changed
- Full rewrite from BepInEx template into functional Harmony mod for Luma Island
