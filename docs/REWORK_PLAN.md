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
| 2 | Done — verified with 2 players: two full matches back to back (random round order, hidden round results, final reveal, return to lobby, rematch with reset scores and a single game manager) |
| 3 | Done — verified with 2 players: two 4-round matches with no freeze, ready timeout with an AFK player, racing time limit (time-up standings), grab/throw seen by the client, join by code also enters the host's Steam lobby, leaving and rejoining through the Steam lobby (invite path) |
| 4 | Done — Mines and Tilefall rebuilt; verified with 2 players (plates and tiles identical on both, safe/mine presses, turn timer auto-press, elimination, tile collapse → fall → checkpoint respawn, finish trigger, time-up standings by progress) |
| 5 | Done — Bomb Rush and Pistol Round added; verified with 2 players in one 4-round match with all four minigames (bombs from conveyors, fuse and contact explosions, grab/throw, knock-off elimination to the spectator bench; turn timer auto-fire, blanks then the live round, knock-off elimination), no errors |
| Demo | Done — 5 minigames (Key Without Lock added), themed arenas, spawns spread by player count, main menu customizer (body/hands/head colour + hat, synced), settings (audio, sensitivity, invert Y, display), Esc pause menu, synthesized SFX and music, nameplates, unified HUD, host max players, Windows build. See `docs/DEMO.md` |

Notes:
- Fusion 2.0.8 editor needed a patch for Unity 6000.4 (`Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs`, marked `[chaos-challenges patch]`). Updating the Fusion SDK overwrites it; a newer SDK likely has its own fix.
- Sessions use `Assets/_Project/Prefabs/SessionRunner.prefab` (runner + scene manager + physics), instantiated per session by `FusionNetworkRunner`.
- The scene manager is `SessionSceneManager` (Fusion's default plus one change): runtime-spawned objects are destroyed one frame before the next scene starts loading. Fusion's default destroys them in the same frame, which deadlocks the Unity 6000.4 Editor about half the time (main thread waits for the PersistentManager lock inside the Editor-only `AssetChangedNotifier` while holding the object lock; the loading thread holds the PersistentManager lock and waits for the object lock). That was the "host freezes at 0 % CPU on a scene load" bug seen since Phase 2; found with a native stack scan using the Editor's own `Unity_x64.pdb`.
- Match flow lives in `NetworkGameManager` (phase, roster, round order, scene-load handshake). Points come from `PlacementScoring` (one table; ties share the better place).
- Minigame framework (`MinigameController`): networked `Phase` (Waiting → Countdown → Playing → Finished) and one `PhaseTimer` for the start countdown and the time limit (`IsTimeLimited` / `GameDuration` in the definition; the last 10 s show on the countdown UI). Minigames end with `FinishRound(placements)` or `FinishRound(standings)` (groups tie), override `GetTimeUpStandings` and `HandlePlayerLeft`. UI events come from the replicated phase, not RPCs. Input is gated by the networked `Player.ControlsLocked`.
- Spawning: `MinigamePlayerSpawner` for every minigame scene, `LobbyPlayerSpawner` for the lobby, both on `BasePlayerSpawner`, which gives each player their own spawn point (by roster slot during a match).
- Interaction: `NetworkInputData.LookDirection` + Interact button; the host raycasts and applies it (`PlayerInteraction`). The owner only plays local feedback (`ILocalInteractable`). `GrabbableObject` is host-authoritative (kinematic while held, thrown along the look direction); `AuthorityHandler` (Shared mode only) is gone.
- Intermission starts by itself after 30 s even if someone never presses ready.
- Steam (`SteamLobby`, `SteamInviteListener`): every session is mirrored into a Steam lobby (public → public, private → friends-only) that stores the Fusion code and game version. Clients joining by code or the public list enter it too, so their friends can join through them. Joining from Steam works three ways, all through `FusionNetworkRunner.JoinSteamLobbyAsync`: overlay invite (`GameLobbyJoinRequested_t`), "Join Game" from the friends list (rich presence `connect`, `GameRichPresenceJoinRequested_t`), and launching the game from either (`+connect_lobby <id>` on the command line). The lobby is closed (not joinable) during a match, so late invites get a clear message instead of a hang. Rich presence sets `status`, `connect` and `steam_player_group(_size)`. In the lobby, `I` opens the Steam invite dialog. The cursor is freed while the Steam overlay is open.
- Errors are shown in `MessagePanel` (`Prefabs/UI/Message_Panel.prefab`, bound in `ProjectContext`): failed host/join, invite failures, and disconnect reasons.
- Before a Steam release (needs the real App ID): replace `AppId_t.Invalid` in `SteamManager.RestartAppIfNecessary`, remove `steam_appid.txt` (480 = Spacewar test app) from the shipped build, and upload rich presence localization so `steam_display` can replace the plain `status` text in the friends list.
- Testing with the CLI: helper scripts live in `Library/claude-test/` (not versioned). Never recompile while Play mode is running; it breaks Zenject and Steam until the Editor restarts. Real Steam invites need two Steam accounts on two machines; Multiplayer Play Mode runs both players on one account, so when the client leaves the Steam lobby the shared account leaves it and Steam deletes it (the host's invites then stop working in that test only).
- Mines (`MinesMinigameController`, `Minefield`, `ExplodeButton` on `Prefabs/Minigames/Mines/MinePlate.prefab`): 4×4 to 7×7 field of arcade buttons (grows with players), exact mine count (30 %), mine positions known only to the host, plate states replicated and plates built locally on every peer. A pressed plate cannot be pressed again. 12 s turn timer (`TurnBasedMinigameController`, reusable for Pistol Round) presses a random plate for AFK players. A mine pops up, explodes (`LowPolyExplosion`) and launches the player, who is benched at their spawn point. When only mines are left, a new field is dealt. The arena TV shows the turn, the countdown and the mines left.
- Tilefall (`TilefallMinigameController`, `TileCourse`, `TileView`; was Racing, scene `TilefallMinigame.unity`): 6 lanes × 18 rows of procedural chamfered tiles (`ProceduralMeshes`) between a start and a finish platform, ~15 % holes, white checkpoint rows every 6 rows. Tiles lose health while players stand on them (1.4 s alone, +75 % speed per extra player), shake and redden, collapse and come back after 4 s. Falling locks you for 1.5 s and puts you on your last checkpoint row. 90 s limit; on time-up the rest rank by the furthest row reached. Finished players may stay on the course to sabotage others.
- Shared: `MinigameController.HandlePlayerFell` (kill zones ask the minigame first), `PlayerMovement.Launch` (knockback), `BasePlayerSpawner.Teleport`, spawn points now set the player's facing.
- Bomb Rush (`BombRushMinigameController`, `Bomb` on `Prefabs/Minigames/BombRush/Bomb.prefab`, `ConveyorBelt`; scene `BombRushMinigame.unity`): four conveyors feed bombs into the arena (faster with more players, capped at 2 per player). A bomb lights when it lands or is grabbed (7 s fuse, pulses redder and faster), can be grabbed and thrown with `GrabbableObject`, and a thrown bomb goes off when it reaches another player. Blasts launch everyone within 5 m (the holder takes the full blast). Falling off the stage puts you on the spectator bench; last one standing wins, survivors tie on time-up (120 s).
- Pistol Round (`PistolRoundMinigameController`, `Revolver`; scene `PistolRoundMinigame.unity`): contestants stand on pedestals (`Player.MovementLocked`: can look and press E, cannot walk). On your turn, aim at a rival (sphere cast along the look direction; a red marker shows your target) and press E. Six chambers, one live round: a blank clicks and the turn passes, the live round knocks the target off their pedestal and out, then the revolver is reloaded. 10 s turn timer fires at a random rival.
- Shared in Phase 5: `MinigameController.Current`, `HandlePrimaryAction` (minigames can take the E press), `GetStatusText` on every minigame (the arena TV shows it), and a ballistic knockback in `PlayerMovement.Launch` (the character controller's braking used to cancel launches within a few ticks).
- New network prefabs need Fusion's prefab table refreshed (`NetworkProjectConfig.fusion` reimport) before they can be spawned; Unity does this on the next import, but a prefab created by script in the same session may need a manual reimport.
- Demo pass: `KeyLockMinigameController` (+ `VaultKey`, `VaultDoor`), arena themes (scrapyard, desert, vault) via props from the asset packs, `MinigamePlayerSpawner` spreads contestants evenly over spawn points, Bomb Rush sudden death. Cosmetics: `CosmeticsCatalog` (Resources/Cosmetics), `LocalLook` (PlayerPrefs), `PlayerLook` tints the three submeshes of `player_parts.asset` (body/hands/head split by bone) and attaches a hat; `CustomizePanel` in the main menu with a live preview. UI: `MinigameHud`, `LobbyHud`, `SettingsPanel`, `PauseMenu` (bound in ProjectContext; also owns cursor locking), `Nameplate`. Audio: `SoundSynth` builds all SFX and the music loop in code, `Sfx` plays them (no audio files in the project). Invites go through the Steam overlay only (no in-game key).
- Still open: a public game browser, announcer voice lines, more hats/outfits, controller support.

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

## Phase 4 — Port existing minigames (done)
- **Mines:** exact mine count, a pressed tile cannot be pressed again, turn timer (auto-pick when AFK), leavers handled.
- **Racing → Tilefall:** tiles crack under players, collapse faster with more players on them, fallen players respawn on a secondary path after a delay.

## Phase 5 — New minigames (done)
- **Bomb Rush:** conveyors deliver bombs, grab and throw them, fuse timer, explosion knocks back / eliminates, last one standing wins.
- **Pistol Round:** turn-based; the current holder aims at another player and fires. Possible twist to discuss: a revolver chamber with blanks so every shot is a gamble.

## Demo pass (done)
- **Key Without Lock** as the fifth minigame; arena themes (Scrapyard, Desert, Vault); spawns spread by player count.
- Character customizer in the main menu (body, hands, head colour, hat), synced to every match.
- Settings (audio, sensitivity, invert Y, fullscreen), Esc pause menu, synthesized SFX; Steam invites through the overlay only.

## UI rework (done)
Cartoon style to match the characters: bright palette with an ink outline instead of dark purple, rounded shapes instead of squares.
- Palette: ink `#2E1A47`, cream `#FFF6E3`, lavender `#E8DDFB`, yellow `#FFC93C`, orange `#FF8A3D`, sky `#5BC0EB`, mint `#4FD1A1`, pink `#FF7EB6`, tomato `#FF5E5B`, grape `#B79CF2`.
- Generated 9-slice sprites in `Sprites/UI`: `ui_panel` (rounded card), `ui_button` (pill with a bottom lip), `ui_frame` (rounded picture frame), `ui_circle`, `ui_track`, `ui_knob`, `ui_arrow`.
- Comic Relief Bold everywhere; ink-outlined text through shared materials (`ComicRelief-Bold SDF Outline Thin/Thick`).
- `ButtonJuice`: buttons grow on hover and squash on press.
- Main menu panels are modal cards over a dimmed menu; intermission TV uses a cream rules card and framed preview; the results board is a screen overlay with one card per contestant.

## Later
- Announcer (text + voice hooks, holographic UI icon), final reveal show.
- Console Round.
- Streamer Mode (web voting for Chaos Modifiers), stats, achievements.
