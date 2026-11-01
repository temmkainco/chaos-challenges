# Third-party assets

| Pack | Author | Licence | Used for |
|---|---|---|---|
| Racing Kit | Kenney (kenney.nl) | CC0 | Kart Kup track pieces, barriers, finish lights |
| Car Kit | Kenney | CC0 | Karts (driver hidden, our contestant sits in it) |
| Toy Car Kit | Kenney | CC0 | Item boxes, bananas, finish gate |
| Pirate Kit | Kenney | CC0 | Islands theme: ships, sand islands, palms, docks |
| Space Kit | Kenney | CC0 | Space theme and Sky Rings: speeders, gates, meteors, hangars |
| Castle Kit | Kenney | CC0 | Castle courtyard: walls, towers, siege engines |
| Holiday Kit | Kenney | CC0 | Snowy village: snowy trees, presents, toy train, snowmen; cloud puffs |
| Graveyard Kit | Kenney | CC0 | Graveyard: crypts, gravestones, iron fences, lamps, ghosts |
| Nature Kit | Kenney | CC0 | Forest and volcano: trees, flowers, rocks, mesas, campsite |
| Fantasy Town Kit | Kenney | CC0 | Town square: fountains, stalls, lanterns, windmill, watermill |
| Food Kit | Kenney | CC0 | Candy land: giant cakes, ice creams, donuts |
| Platformer Kit | Kenney | CC0 | Sky islands: floating grass blocks, coins, stars |
| Mini Arena | Kenney | CC0 | Colosseum: stands, columns, banners, statues, crowd |
| Platformer Pack | Quaternius | CC0 | Doors, towers, bouncers, hazards |
| Universal Animation Library (Standard, v3.0) | Quaternius (quaternius.itch.io) | CC0 | Character clips: Idle, Walk, Jog, Sprint, JumpStart, Fall, Land, HitChest, HitHead, PushLoop, SpellShoot, Dance, SitIdle, Drive, SwimIdle, Roll |
| Universal Animation Library 2 (Standard, v2.2) | Quaternius | CC0 | Character clips: HitKnockback, IdleFoldArms, GetUp, Throw, WalkCarry, Yes |
| Various Attitudes animations (FREE01) | Motion Cast (Godot Asset Store) | Royalty free (store licence: non-exclusive, perpetual, use and modify) | Emotes: Wave, Laugh, Cheer, Cry, Clap (2.6 s slices; Wave's arm moved off our big head) |
| Lowpoly Asset Bundle | Pandazole | Asset Store licence | Toy bricks, logs, crates, fences, rocks |

Kenney packs live in `Assets/ThirdParty/Kenney/<Kit>/` (FBX + colour map, each with its `License.txt`).

The character clips are copied out of the libraries' Humanoid FBXs (in place, root motion baked) into
`Assets/_Project/Animations/Character/` by `Tools/Character/unity/ual_import.cs` (Unity editor script, run with
`unity command script eval_file`); the FBXs themselves are not in the repository. `Shove.anim` is ours, keyed from
muscle poses by `Tools/Character/unity/build_controller.cs`, which also builds `Player.controller`. The emotes come
from `Tools/Character/unity/emote_import.cs` (Motion Cast's "no root" FBXs, explicit bone map); Dance is the
Quaternius clip. 6-7, Love and Dab have no downloadable equivalent and stay procedural (`EmotePlayer`).

