# Chaos Challenges — Rework Plan

Working plan for reworking the existing game and building the GDD minigames
("Don't Stop the ParTV" GDD). Each phase should leave the game playable.

## Decisions

| Topic | Decision |
|---|---|
| Netcode topology | Photon Fusion 2, **Host mode** (host-authoritative; suits physics + competitive play) |
| Round results | **Hidden** during the match; dramatic **final reveal** at the end (GDD "6 rooms" twist) |
| First new minigame | **Bomb Rush** (needs grab/throw, which Key Without Lock reuses later) |
| Racing | Becomes **Tilefall** |
| Multiplayer testing | **Multiplayer Play Mode** package (host + virtual clients in one Editor) |

## Status

| Phase | State |
|---|---|
| 0 | Done — Unity 6000.4.5f1, Multiplayer Play Mode 2.0.2, Pipeline package (CLI Editor control) |
| 1 | Done — verified with Multiplayer Play Mode: host, leave, re-host, private join by code, client leave/rejoin, host leaving sends client to menu with a reason |
| 2 | Next |

Notes:
- Fusion 2.0.8 editor needed a patch for Unity 6000.4 (`Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs`, marked `[chaos-challenges patch]`). Updating the Fusion SDK overwrites it; a newer SDK likely has its own fix.
- Sessions use `Assets/_Project/Prefabs/SessionRunner.prefab` (runner + scene manager + physics), instantiated per session by `FusionNetworkRunner`.
- Max players / public list are supported by the service, but the host menu UI only has the public toggle; disconnect reasons are only logged (no message panel yet).

## Phase 0 — Setup and baseline
- Stop tracking `chaos-build/` and the thesis PDF in git; add both to `.gitignore`.
- Add `com.unity.multiplayer.playmode`; confirm a host + 1–3 clients run from the Editor.
- Open in Unity 6000.2.10f1, confirm a clean compile, note current behavior per scene.

## Phase 1 — Session layer
Replaces `FusionNetworkRunner`.
- Fresh `NetworkRunner` per session (a shut-down runner cannot be restarted); the service itself persists.
- Join by code joins `SessionName` directly instead of searching the cached session list.
- Host config: max players (2–6), public/private (`IsVisible`); public lobby list for the menu.
- Shutdown/disconnect reasons return the player to the main menu with a message.
- Steam lobby creation gets a timeout and stays optional.
- One place for constants (max players, scene references by name instead of build index).

## Phase 2 — Match flow
Rewrite of `NetworkGameManager` / `LobbyManager` flow.
- One networked match state with phases: Lobby → Intermission → Playing → RoundOver → FinalReveal → Lobby.
- Player roster keyed by `PlayerRef`: slot, name, total score, connected flag.
- Match config: number of rounds, random minigame per round without repeats.
- Hidden results: after a round show only "round over" and eliminations; totals revealed at the end, last place to first.
- Clean rematch: match state reset and owned by the session, never duplicated.
- Every scene load goes through one path and waits until all clients have loaded before the round starts.

## Phase 3 — Minigame framework
- Base minigame with networked phase (Countdown, Playing, Finished) and `TickTimer`s for countdown and time limit (uses `GameDuration` / `IsTimeLimited`).
- Input gated inside the simulation (`FixedUpdateNetwork`) by game phase, not by disabling local input actions.
- Minigames report a **placement order** (finish order or elimination order); scoring by placement lives in one table.
- Leaving players are handled everywhere (counted as eliminated / did not finish), so a round can never hang.
- One generic spawner with spawn points instead of per-minigame copies.
- Interaction goes through the networked input struct, processed on the host, instead of RPCs.
- Host-authoritative grab / carry / throw replaces `AuthorityHandler` (which only works in Shared mode).

## Phase 4 — Port existing minigames
- **Mines:** exact mine count, a pressed tile cannot be pressed again, turn timer (auto-pick when AFK), leavers handled.
- **Racing → Tilefall:** tiles crack under players, collapse faster with more players on them, fallen players respawn on a secondary path after a delay.

## Phase 5 — New minigames
- **Bomb Rush:** conveyors deliver bombs, grab and throw them, fuse timer, explosion knocks back / eliminates, last one standing wins.
- **Pistol Round:** turn-based; the current holder aims at another player and fires. Possible twist to discuss: a revolver chamber with blanks so every shot is a gamble.

## Later
- Announcer (text + voice hooks, holographic UI icon), in-game HUD, final reveal show.
- Key Without Lock, Console Round.
- Streamer Mode (web voting for Chaos Modifiers), cosmetics, stats, achievements.
