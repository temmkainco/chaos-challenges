# Networking

Photon Fusion 2.0.8, host mode (one player hosts, Photon cloud relay, region `eu`), 64 ticks/s, client and
server send every tick. Contestants are predicted on their owner's machine (`NetworkCharacterController`) and
authoritative on the host.

## Rules for game code (desync)

Anything that can kill, push or block you must be in the same state on every machine **at the same tick**:

| Kind of thing | How | Example |
|---|---|---|
| Moves at a known rate (sweeper, log, lava, walls) | `NetMotion` (value + rate + anchor tick, networked). Evaluate with `AtTick` in simulation, `Render` for visuals. The host only calls `Retarget` when the rate changes. | Spin Sweep, Log Roll, Lava Rise |
| Appears or disappears (tiles) | The host schedules the tick it happens on, far enough ahead that clients know in time. Colliders follow that tick in `IBeforeTick` on every peer (the object is simulated on clients: `MinigameController.SimulatedOnClients` or `Runner.SetIsSimulated`). | Tilefall (`TileCourse._warningSeconds`), Hex Drop, Sweep floor |
| Hits and knockbacks from obstacles | `MinigameController.SimulateContestant`: called from each contestant's own tick, just before it moves. The host runs it for everyone, a client for its own contestant. Use `CoolingDown` for cooldowns (safe under re-simulation); RPCs and effects only on the host. | Sweep bars, Log ridges, Block Party walls, Lava springs, Tilefall hazards |
| Contact actions against another player | `Rewind.PositionSeenBy(actor, target)`: where the target was on the actor's screen (max 0.25 s back). | TailTag grab, Hot Potato pass, shoves |
| Cosmetic motion | Anything local (`Time.time` is fine). | Wagging tails, pulsing glows |

Contestants don't collide with each other (`PhysicsSetup`); `PlayerMovement` eases overlapping contestants
apart (`_separationRadius`, `_separationSpeed`). Hard contacts between predicted and interpolated players were
corrected all the time.

## Diagnostics

- **F3** in game: ping (every player's on the host), fps, and *mispredictions*: how often and how far your own
  predicted position was wrong once the host's data arrived (`PredictionStats`; teleports are not counted).
- `Player.log` gets a `[Net]` line every 30 s in a session and `[Session]` lines for joins, leaves, failed
  connects and why a session ended.
- Latency testing in the editor: `NetworkProjectConfig.fusion` → `NetworkConditions.Enabled` (keep it **off**
  in commits).

## Losing the session

If the host's session ends unexpectedly, the host reopens the lobby under the same code (the match in
progress is lost), and dropped clients retry that code a few times (`FusionNetworkRunner._rejoinAttempts`,
`_rejoinDelay`). Fusion's `ConnectionTimeout` is 20 s.

## Dedicated server: recommendation

**Not migrated yet.** The fixes above remove the desync that was in our code; that would have happened with a
dedicated server too. A dedicated server is still worth it later, for what host mode can't fix:

- The host's frame rate and upload affect everyone; a slow or busy host PC lags the whole lobby.
- The host has zero ping (an advantage in reaction games).
- When the host's game dies, the session dies (now recovered back to the lobby, but the match is lost).

**What changes in code**

1. Start the server with `GameMode.Server` from a headless entry point (command line), build the
   *Linux Dedicated Server* target, and skip rendering, audio and UI in batch mode.
2. No local player on the server: audit code that uses `Runner.LocalPlayer` or assumes the host is playing
   (HUD, lobby, `RPC_Ready`, test hooks).
3. A *party leader* replaces the host for lobby settings, game packs and Start (leader RPCs, checked on the
   server).
4. Creating a lobby asks the hosting provider for a server and gets back a session; codes and Steam invites
   point at it. The server shuts down when everyone has left.

Estimate: a few days of work plus testing.

**Hosting options**

| Option | Cost | Notes |
|---|---|---|
| Edgegap (has a Fusion 2 sample) | about $0.069 per vCPU-hour, billed by the minute, plus $0.10/GB egress; one lobby fits on about 1 vCPU | Starts a server per match near the players; nothing runs while nobody plays. |
| Always-on VPS (e.g. Hetzner) | roughly €5–10/month (not re-checked) | Several lobbies per box; one region; we run and update it ourselves. |
| Photon | Free 100 CCU (one app); 500 CCU about $125/month | Needed in either case: Photon's plans cover the relay, not servers. |

Prices are from vendor pages and third-party comparisons as of October 2026; check them before buying.

Order: playtest these fixes first; if lag still follows the host's machine or lobbies still die, move to
Edgegap.
