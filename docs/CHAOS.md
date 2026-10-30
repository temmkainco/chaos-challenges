# Chaos (buffs, debuffs, hazards)

The GDD's Streamer Mode lets viewers vote on chaos from a website. That site does not exist yet. Today a random director fires chaos, and the host turns it on in the host options: **Off**, **Normal** or **Wild**. The vote API will only need one host-side call:

```csharp
ChaosDirector.Request(ChaosModifier modifier, float seconds = 12f); // returns false if it can't run now
```

A request is announced first ("BLACKOUT IN 3..." plus a countdown on the HUD) and starts 3 seconds later, so players can react. A new request replaces one that is still waiting.

`MinigameController.Current.SupportsChaos(modifier)` says what the running round accepts, so the vote page can list only those. `TwistTitle`, `TwistDescription` and `TwistKind` give the round's own twist, for the vote button.

## Shared modifiers (every round)

| Modifier | Kind | Effect |
|---|---|---|
| LowGravity | buff | Floaty jumps |
| SpeedRush | buff | Everyone 1.5x faster |
| SuperJump | buff | Much higher jumps |
| Giants | buff | Big contestants (not in Mines) |
| SlowMotion | debuff | Everyone slower |
| Tiny | debuff | Small contestants (not in Mines) |
| ReversedControls | debuff | Controls are inverted |
| IceFloor | debuff | Slippery ground |
| Blackout | debuff | The lights go out |
| MeteorShower | hazard | Rocks fall near contestants |

Console Round only takes chaos the pixel game can feel: SpeedRush, SlowMotion, ReversedControls and Blackout (TV static) in every cartridge, plus LowGravity and SuperJump in the cartridges that jump (Flap Dash, Pixel Runner, Sky Hopper). Body chaos (meteors, giants, tiny, ice) is off because everyone is seated.

## Twists (`ChaosModifier.Twist`, one per round)

| Round | Twist | Kind | Effect |
|---|---|---|---|
| Tilefall | CROSSWIND | hazard | A gale pushes across the lanes and flips direction every 4 s |
| Mines | DOUBLE DARE | hazard | The next contestant presses two plates |
| Bomb Rush | BOMB BONANZA | hazard | Bombs rain from the sky all over the arena |
| Key Hunt | MASTER KEY | buff | A big golden key that opens any door appears; it vanishes when the twist ends |
| Prize Grab | PRIZE STORM | buff | Golden prizes rain down |
| Hex Drop | EARTHQUAKE | hazard | Random tiles on every floor crack by themselves |
| Hot Potato | BOMB HOP | hazard | The bomb jumps to a random contestant every 3 s |
| Ring Out | SHRINK RAY | hazard | The ring shrinks to half its size, then grows back |
| Cannon Barrage | BROADSIDE | hazard | All cannons but two fire at once |
| Color Code | MIND GAMES | debuff | The called colour is written in another colour (read the word) |
| Console Round | MIRROR MODE | debuff | The TV picture flips left to right |

The random director picks the round's twist about a quarter of the time.
