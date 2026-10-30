# Show Rework — design plan

Goal: feel like a TV game show (GDD: "Welcome to the show", unseen announcer, rules shown "just like in Pummel Party"), with minigames that are won by skill, readable at a glance, each on its own decorated set.

## What we learned
- **Pummel Party:** minigames are short, skill/coordination based and learnable; the luck lives in the board and items, not in the minigames. Players complain about agency when outcomes are random. Rules screen with a looping preview and controls before each game, then straight into play. HUD is tiny; players are identified by colour and a name over the head.
- **Party game design in general:** simple controls anyone can pick up, the goal visible at a glance, fewer elements on screen. Randomness is fine as *spice* (who goes first, a modifier) but the result should come from what players did.
- **GDD references:** Tilefall is a long purple sky highway with walls, wedges, swinging robot hammers, glowing cracks and a GOAL arch; Key Without Lock is a white multi-level set with purple walkways, ladders and many coloured doors with symbol plates and keys everywhere; Console Round is a cosy dark living room with retro TVs. The current arenas are one octagon platform reskinned five times.

## Controls (all minigames)
- **E**: grab what is in front of you (nearest grabbable in a cone, outlined while targeted); with something held, **E drops it** at your feet. In-game actions (try a key at a door, fire) also stay on E.
- **Hold LMB**: wind up a throw, the arc preview shows where it lands; the mouse aims (camera pitch = throw angle); **release** throws with the charged force. RMB or E while charging cancels.
- Held items sit in the right hand (hand bone), not above the head.
- Everything grabbable has an outline that turns on when it is the target.
- Nameplates follow the head bone, keep a constant on-screen size, use the player colour and are hidden for yourself.

## Presentation
- HUD only when it matters: a small timer chip, the minigame's one key number, and short announcer banners ("ELIMINATED!", "YOUR TURN") that slide in and out. Controls are shown on the intermission TV and as a 5-second hint, not permanently.
- Each round opens like a TV segment: studio sting, camera fly-over of the set, announcer line, countdown. Round end: freeze, "ROUND OVER" sting, players shown on the set, then the secret-results board.

## Less random minigames
- **Mines** → deduction: safe plates you step on show how many mines touch them (minesweeper rules); the turn timer stays. Skill = reading the numbers.
- **Pistol Round** → quick-draw duel: everyone faces off, "DRAW!" fires after a random wait, first accurate shot at a rival scores; shooting early jams your gun for the round.
- **Key Without Lock** → observation: every door shows a symbol, every key has one; only matching keys open a door. No hidden duds.
- **Bomb Rush** and **Tilefall** are already skill based; they gain from aimed throws and set dressing.

## Sets (own environment per minigame)
- Mines: TV studio stage with audience stands, light rigs and a big floor grid.
- Tilefall: sky highway per the reference (walls, wedges, hammers, GOAL arch, clouds below).
- Bomb Rush: scrapyard pit with conveyor belts on raised ledges and junk piles.
- Pistol Round: western main street (saloon fronts, barrels, cacti) at sunset.
- Key Without Lock: white multi-level vault with walkways, ladders and coloured symbol doors.

## Status
- Done: controls (cone targeting, outlines, hand-held items, aimed charged throw, drop), nameplates, lean HUD with announcer banners, Mines numbers, quick-draw Pistol Round, colour-matched keys, a round end that is only a banner (full board only for the final reveal), and a set per minigame (saved under a `Set` object in each scene; the old octagon is kept but switched off where a set replaces it).
- Next: round intro (camera fly-over of the set + announcer line), interactive set hazards (Tilefall hammers that knock players), Console Round.

## Order of work
1. Controls: targeting + outlines, hand attach, aimed charged throw, drop. Nameplates.
2. HUD reduction and announcer banners.
3. Minigame rule changes (Mines numbers, quick-draw, symbol keys).
4. Sets, one per minigame.
