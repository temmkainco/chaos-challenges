# Overhaul plan — party-game quality bar

## GDD essentials we keep
"Don't Stop the ParTV": contestants in a chaotic TV game show, rounds = minigames, an unseen over-the-top announcer, rules shown before each round "like Pummel Party", and **hidden results until the final reveal** (Exile "6 rooms" format). WASD + mouse, Space jump, E interact. 2–6 players, Photon Fusion host mode.

## Audit — what exists, what is broken

| Area | Today | Problem |
|---|---|---|
| Movement (`PlayerMovement`) | Constant speed 5, instant start/stop, jump only when grounded, launch = scripted fly | No acceleration curve, no coyote time, no jump buffer, weak air control, hits feel scripted, not physical |
| Grab/throw (`PlayerInteraction`, `GrabbableObject`) | Shared: cone targeting, outline, hand attach, hold-LMB charge + arc preview, E drops | Recent and only probe-tested; pickup has no grab animation/reach; throw has no "at feet" assist; player-vs-player shove missing |
| HUD | Small timer/status chip, fading hint, announcer banner | Timer still top-centre; no round-name splash; styles differ between HUD, intermission TV and board |
| Flow | Lobby → Intermission **scene** → minigame **scene** → Intermission scene … (every step a full networked single-scene load) | Hitches and blank frames on every transition; intermission is a separate 3D scene with a TV; no intro, no arena reveal |
| Rules screens | Text rules + a preview **screenshot** on the intermission TV | Pictures show the arena, not how to play (controls, objective, what eliminates you, scoring) |
| Arenas | Five sets just built from Pandazole props + primitive boxes | Floors/walls/hammers are cubes; some sets end in the void; spawns are a fixed ring of 6 points |
| Minigames | Mines (turn-based deduction), Tilefall, Bomb Rush, Pistol (quick-draw), Key Without Lock (colour match) | Pistol: cut per brief. Tilefall hammers are decoration. Keys: you can throw rivals' keys; not "needle in a haystack" |
| Menu/branding | Cartoon UI kit, plain-text title | No real logo; site has only key art with the logo in the corner |
| Performance | Bombs/keys/effects spawned with `Runner.Spawn`/`Instantiate`; `FindFirstObjectByType` in some per-frame paths | No pooling; some per-frame allocations (string building, LINQ) |

## The 5 minigames
1. **Bomb Rush** (keep, retune): faster pickup, throws aimed at feet, shorter fuses, stronger readable knockback, pooled bombs.
2. **Tile Rush** (Tilefall reworked): real hammer models that sweep the course and knock players with physics, plus spinning bars and pushers; checkpoints stay.
3. **Key Hunt** (Keys/Doors redesign, rules below).
4. **Mines** (keep as the "think" round, faster turns, numbers stay).
5. **New round — pick one (pitches):**
   - **Prize Grab** *(recommended)*: prizes rain onto the studio floor; grab one, carry it to your own podium to bank it. Carrying slows you, rivals can tackle-knock it out of your hands. Most points in 75 s wins. Uses grab/throw, scales 2–6, always busy.
   - **Spotlight**: a roaming studio spotlight; score every second you stand in it alone; shove others out. King-of-the-hill.
   - **Ring Out**: shrinking round stage, shove and throw cushions to knock others off; last one standing.

### Key Hunt ruleset (proposal)
- 4 doors per player on the walls (numbered plates), ~5 keys per door scattered everywhere (shelves, floor, walkways): lots of keys, each opens exactly one door.
- Grab a key (E), walk to any door, press E to try it. Wrong door: the key stays in your hand and its handle glows **warmer/colder** depending on how close its own door is, so searching is skill, not luck.
- Keys **cannot be thrown or destroyed**; a key you hold is yours alone (tinted your colour, nobody can take it). Drop it with E and it is free again.
- Right door: it opens for you, you go through and score; the key is used. First through scores most; placements by escape order; time limit ends the round.

## Round flow (one persistent studio, arenas load additively)
`Lobby` → **Show intro** (announcer splash "Round 2/5: BOMB RUSH", studio sting) → **Rules card** (animated illustration + controls + win/lose, everyone presses Ready, auto after 15 s; next arena preloads additively meanwhile) → **Arena reveal** (3 s camera fly-over) → **3-2-1-GO** → play → **finish freeze + "ROUND OVER!"** → **round recap** (see question on results) → next round. Final: full reveal board, last to first, then back to the lobby.
Tech: keep one network scene for the show and load arenas additively through Fusion (`LoadSceneMode.Additive`), unload the previous one behind the rules card, so no blank loads.

## Art
- Replace boxes with low-poly models: Kenney (CC0) and Quaternius (CC0) packs for platformer pieces, hammers, studio gear; existing Pandazole props where they fit. `CREDITS.md` lists every pack and licence.
- Every arena floats in a studio world: framed edges, rails/bumpers, light rigs, stands, backdrop.
- Spawns generated per arena and player count (even spacing, raycast onto valid floor, clear of geometry), tested for 2 and 6 players.

## Decisions (approved)
- 5th round: **Prize Grab**. Pistol Round is removed.
- Round end: totals stay hidden (GDD), but a **podium for that round** (1st/2nd/3rd) is shown.
- CC0 packs (Kenney, Quaternius) may be downloaded; each is listed in `CREDITS.md`.
- Logo: **recreated in-engine** from the key art (pink "DON'T STOP THE PAR", big orange "TV", orange retro TV with a peeking contestant).

## Order of work
1. Shared controller (accel/decel, air control, coyote, jump buffer, physical hit reactions) + grab/throw polish → test 2p/6p.
2. HUD: corner timer, round-name splash.
3. Flow + rules cards (additive loading, ready-up, illustrations).
4. Arenas/art.
5. Minigames (Bomb Rush, Tile Rush, Key Hunt, Mines, new round; remove Pistol).
6. Menu/branding (logo).
7. Performance pass (profiling, pooling, allocations).
