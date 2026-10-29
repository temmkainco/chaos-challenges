# Don't Stop the ParTV — Demo

A party game show for 2–6 players online. One match = every minigame once, in random order. Round results stay secret until the final reveal.

## How to play

1. **Main menu** — *Customize* your contestant (body, hands and head colour, hat; saved and used in every match), adjust *Settings*, then *Host* (public/private, max players 2–6), *Join By Code*, or *Join Random*.
2. **Lobby** — the room code is in the top-right corner. Invite Steam friends through the Steam overlay (Shift+Tab); friends can also use "Join Game" from your profile. Press **E** on the big red button when everyone is in.
3. **Intermission** — the TV explains the next minigame. Press *Ready*; the round starts by itself after 30 s.
4. **Results** — after each round you only see that it is over; the totals are revealed, last place to first, after the final round. Then everyone is back in the lobby for a rematch.

**Controls:** WASD move · Mouse look · Space jump · E interact / grab / throw / fire · Esc pause (Resume, Settings, Leave match).

## Minigames

| Minigame | Arena | Goal |
|---|---|---|
| **Mines** | Game-show island | Take turns pressing plates; a mine launches you off the stage. 12 s per turn, or the show presses for you. Last one standing wins. |
| **Tilefall** | Sky course | Race across lanes of tiles that crack under you (faster when crowded). Fall and you restart from your last checkpoint row. |
| **Bomb Rush** | Scrapyard | Conveyors deliver bombs: grab, throw, blast rivals off the stage. Sudden death doubles the bombs at the end. |
| **Pistol Round** | Desert standoff | Six chambers, one live round. Aim at a rival and pull the trigger: click… or BANG. |
| **Key Without Lock** | Vault warehouse | Find a key that fits the vault door. Duds shatter; escape before the others. |

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
