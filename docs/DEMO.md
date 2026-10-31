# Don't Stop the ParTV — Demo

A party game show for 2–6 players online. One match = every minigame once, in random order. Round results stay secret until the final reveal.

## How to play

1. **Main menu** — *Customize* your contestant (body, hands and head colour, hat, face, neck and back wearables; saved and used in every match), adjust *Settings*, then *Host* (public/private, max players 2–6), *Join By Code*, or *Join Random*.
2. **Lobby** — the room code is in the top-right corner. Invite Steam friends through the Steam overlay (Shift+Tab); friends can also use "Join Game" from your profile. Press **E** on the big red button when everyone is in.
3. **Intermission** — the TV explains the next minigame. Press *Ready*; the round starts by itself after 30 s.
4. **Results** — after each round the board shows every contestant with a "?" badge; the totals are revealed, last place to first, after the final round. Then everyone is back in the lobby for a rematch.

**Controls:** WASD move · Mouse look · Space jump · E interact / grab / throw / fire · Esc pause (Resume, Settings, Leave match).

## Minigames

| Minigame | Arena | Goal |
|---|---|---|
| **Mines** | Game-show island | Take turns pressing plates; a mine launches you off the stage. 12 s per turn, or the show presses for you. Last one standing wins. |
| **Tilefall** | Sky course | Race across lanes of tiles that crack under you (faster when crowded). Fall and you restart from your last checkpoint row. |
| **Bomb Rush** | Scrapyard | Conveyors deliver bombs: grab, throw, blast rivals off the stage. Sudden death doubles the bombs at the end. |
| **Pistol Round** | Desert standoff | Six chambers, one live round. Aim at a rival and pull the trigger: click… or BANG. |
| **Key Without Lock** | Vault warehouse | Find a key that fits the vault door. Duds shatter; escape before the others. |

### Arena layouts

Each round draws one layout for its arena (shown on the rules card as **MAP** and in the round splash). The host picks it with the round (`NetworkGameManager.RoundSeed`), so every peer builds the same one; `MinigameController.Variants` lists the names.

| Minigame | Layouts |
|---|---|
| Hex Drop | Classic Stack · Tall Tower (4 smaller floors) · Twin Peaks (two islands, crossed floors) · Donut |
| Color Code | Square · Cross · Islands (hop between quarters) · Diamond |
| Ring Out | Classic Ring · Square Ring · Pillars |
| Cannon Barrage | Full Circle · Crossfire (two batteries) · Firing Line (one side) |
| Hot Potato | Open Stage · Pillars · Crossroads (low walls) |
| Tilefall | Classic · Swiss Cheese (more holes) · Stepping Stones (checkerboard) |
| Bomb Rush | Open Floor · Crates · Pillars (cover blocks thrown bombs) |
| Prize Grab | Open Stage · Hurdles · Towers |
| Mines | Classic · Wide Field · Danger Zone |

### Pack 2 (ten more games)

| Minigame | Type | Layouts |
|---|---|---|
| Spin Sweep | last one standing: jump the sweeping bars | Two Bars · Cross · Counter-Spin |
| Block Party | last one standing: find the gap in the sliding walls | Classic · Narrow · Crossfire |
| Log Roll | last one standing on spinning logs | Big Log · Twin Logs · Speed Log |
| Lava Rise | climb above the rising lava | Spiral · Twin Spirals · Scatter |
| King of the Hill | points: hold the moving zone alone | Open Stage · Pillars · Shrinking Zone |
| Paint Party | points: paint the most tiles | Square · Ring · Islands |
| Tail Tag | grab a tail, hold it at the bell | Open Stage · Pillars · Hedges |
| Door Dash | race through real and fake doors | Classic · Fake Out · Long Hall |
| Kart Kup | 3-lap kart race (you drive a kart) | Oval · Clover · Long Oval |
| Sky Rings | points: fly a plane through rings | Big Loop · Slalom · Tower |

Vehicles are a movement mode of the contestant (`MinigameController.Vehicle`, `PlayerMovement.Drive/Fly`), so they are predicted and networked like running; `RiderVisual` seats the character in a kart or plane and steers the camera behind it. Points games derive from `ScoreMinigameController`, races from `RaceMinigameController`.

Scoring: each round awards points by place (ties share the better place); the most points after all rounds wins.

## Building

`File > Build Settings` (Windows x64) or the scripted build used during development writes to `chaos-build/Demo/` (git-ignored). `steam_appid.txt` (480 = Spacewar test app) is copied next to the exe for local testing only.

## Steam release checklist

- [ ] Real App ID: replace `AppId_t.Invalid` in `SteamManager.RestartAppIfNecessary` and do **not** ship `steam_appid.txt`.
- [ ] Set the company name in Player Settings (currently `DefaultCompany`).
- [ ] Upload rich presence localization (`steam_display` tokens) so friends see "Playing Bomb Rush (round 2/5)".
- [ ] Store page assets (capsules, screenshots, trailer); the app icon is `Assets/_Project/Sprites/AppIcon.png`.
- [ ] Playtest with 3–6 real players on separate Steam accounts (invites, "Join Game", mid-match leaves).
- [ ] Photon Fusion: production App ID and CCU plan for the demo traffic.
