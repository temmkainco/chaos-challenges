# Don't Stop the ParTV — Demo

A party game show for 2–6 players online. One match = every minigame once, in random order. Round results stay secret until the final reveal.

## How to play

1. **Main menu** — *Customize* your contestant (body, hands and head colour, hat, face, neck and back wearables; saved and used in every match), adjust *Settings*, then *Host* (public/private, max players 2–6), *Join By Code*, or *Join Random*.
2. **Lobby** — the room code is in the top-right corner. Pick your **player colour** top left (**C** for the next free one; it's unique, shown as a ring at your feet and on your name, and used in every minigame). **L** opens tonight's games (the host can take games in and out there). Invite Steam friends through the Steam overlay (Shift+Tab); friends can also use "Join Game" from your profile. Press **E** on the big red button when everyone is in.
3. **Each round** — the curtain stays down until everyone has loaded, then the rules card (over an empty arena) is the ready check; contestants appear for the countdown, then go.
4. **Results** — after each round the board shows every contestant with a "?" badge; the totals are revealed, last place to first, after the final round. Then everyone is back in the lobby for a rematch.

**Controls:** WASD move · Mouse look · Space jump · E interact / grab / throw / fire · Esc pause (Resume, Settings, Leave match).

## Minigames

| Minigame | Arena | Goal |
|---|---|---|
| **Mines** | Game-show island | Take turns pressing plates; a mine launches you off the stage. 12 s per turn, or the show presses for you. Last one standing wins. |
| **Tilefall** | Sky course | Race across lanes of tiles; a tile drops soon after you first step on it. Standing on the scenery counts as off the course. Fall and you restart from your last checkpoint row. |
| **Bomb Rush** | Pirate islands | Bombs float down onto everyone's island: grab, throw, blast rivals off theirs. Each bomb glows in its owner's colour; the fuse pulses faster near the end (no numbers). |
| **Pistol Round** | Desert standoff | Six chambers, one live round. Aim at a rival and pull the trigger: click… or BANG. |
| **Key Hunt** | Prison at night | A key's colour says nothing about its door: try keys on the doors to find the one each opens. Fakes crumble; escape before the others. |

### Arena layouts

Each round draws one layout for its arena (shown on the rules card as **MAP** and in the round splash). The host picks it with the round (`NetworkGameManager.RoundSeed`), so every peer builds the same one; `MinigameController.Variants` lists the names.

| Minigame | Layouts |
|---|---|
| Hex Drop | Classic Stack · Tall Tower (4 smaller floors) · Twin Peaks (two islands, crossed floors) · Donut · Pyramid · Spokes · Swiss Hex |
| Color Code | Square · Cross · Islands (hop between quarters) · Diamond |
| Ring Out | Classic Ring · Square Ring · Pillars |
| Hot Potato | Open Stage · Pillars · Crossroads (low walls) |
| Tilefall | Classic · Swiss Cheese (more holes) · Stepping Stones (checkerboard) · Snake (a path swinging side to side) · Zigzag |
| Bomb Rush | Islands · Close Islands · Crate Islands (island wars: everyone on their own island) |
| Prize Grab | Open Stage · Hurdles · Towers |
| Mines | Classic · Wide Field · Danger Zone |

### Pack 2 (ten more games)

| Minigame | Type | Layouts |
|---|---|---|
| Spin Sweep | last one standing: jump the sweeping bars | Two Bars · Cross · Counter-Spin |
| Block Party | last one standing: find the gap in the sliding walls | Classic · Narrow · Crossfire |
| Log Roll | last one standing on spinning logs | Big Log · Twin Logs · Speed Log |
| Lava Rise | parkour climb above the rising lava (stones, beams, pillars, springs) | Spiral · Twin Spirals · Scatter · Figure Eight |
| King of the Hill | points: hold the moving zone alone | Open Stage · Pillars · Shrinking Zone |
| Paint Party | points: paint the most tiles | Square · Ring · Islands |
| Tail Tag | grab a tail, hold it at the bell | Open Stage · Pillars · Hedges |
| Door Dash | race through real and fake doors (fewer real ones further on; a fake stuns you) | Classic · Fake Out · Long Hall · Wide Hall · Sprint |
| Kart Kup | kart race with item boxes (banana, boost, shockwave; the item shows in a HUD slot) | Grand Prix · Chicane · Esses · Hairpins · Serpent |
| Sky Rings | race: fly a speeder through the gates in order | Island Loop · Space Eight · Cloud Circuit |

Console Round cartridges: Snake Pit (hold a direction to speed up), Flap Dash, Pixel Runner, Brick Bash, Sky Hopper, Astro Blast, Frog Hop, Stack Tower.

Vehicles are a movement mode of the contestant (`MinigameController.Vehicle`, `PlayerMovement.Drive/Fly`), so they are predicted and networked like running; `RiderVisual` seats the character in a kart or plane and steers the camera behind it. Points games derive from `ScoreMinigameController`, races from `RaceMinigameController`.

### Locations

No minigame is played on a bare stage: every arena is set in a place built around it at load time (`MinigameController.Theme`, `ArenaTheme`), and each game has its own places, one per layout (table `ArenaTheme.ByScene`). The studio set goes; the stage, if the game uses one, is recoloured to fit. Places: pirate cove (ocean, sand islands, ships), castle courtyard (curtain wall, towers, siege engines), snowy village (toy train, decorated tree, snowmen), graveyard at night (crypts, iron fence, lamps), forest valley (campsite, mesas), town square (houses, market, mills), candy land (giant treats, chocolate rivers), sky islands (cloud sea, floating blocks), race circuit (grandstands, pits), colosseum (stands with a crowd), volcano (lava rivers, glowing peaks) and outer space. Each has its own sky gradient, fog, sun and ambient light. Scenery has no colliders and is placed outside the play area, measured from what the game built. Models come from `ArenaModels` (see `docs/CREDITS.md`). Key Hunt's warehouse is dressed as a prison at night; the western street (Pistol Round) and sky course (Tilefall) keep their own scenes. Every play area without its own walls has invisible walls round it (`MinigameController.UsesBoundary`).

## Building

`File > Build Settings` (Windows x64) or the scripted build used during development writes to `chaos-build/Demo/` (git-ignored). `steam_appid.txt` (480 = Spacewar test app) is copied next to the exe for local testing only.

## Steam release checklist

- [ ] Real App ID: replace `AppId_t.Invalid` in `SteamManager.RestartAppIfNecessary` and do **not** ship `steam_appid.txt`.
- [ ] Set the company name in Player Settings (currently `DefaultCompany`).
- [ ] Upload rich presence localization (`steam_display` tokens) so friends see "Playing Bomb Rush (round 2/5)".
- [ ] Store page assets (capsules, screenshots, trailer); the app icon is `Assets/_Project/Sprites/AppIcon.png`.
- [ ] Playtest with 3–6 real players on separate Steam accounts (invites, "Join Game", mid-match leaves).
- [ ] Photon Fusion: production App ID and CCU plan for the demo traffic.
