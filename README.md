# Last Match

A reverse match-3. You are the gem that woke up, and an AI player is trying to
match you off the board. Survive until it runs out of moves.

Open `last-match.html` in a browser to play. No build step, no dependencies
beyond two Google Fonts.

Two builds share the same design:

- `last-match.html`: the web prototype, one file, open it in a browser.
- `unity/`: the native Unity 6 port for Android and iOS. See `unity/README.md`
  for opening, testing and building it.

## Campaign

250 generated levels in ten tiers. Each tier starts at a set level (see the
`from` field in `TIERS`), adds one mechanic and keeps everything before it. Progress and best scores are saved in the browser.

| Levels | Tier | New mechanic |
| --- | --- | --- |
| 1–9 | Basics | Plain matches only. Three in a row clears, nothing more. |
| 10–24 | Blasters | Four in a row makes a row or column blaster. |
| 25–49 | Bombs & Holes | L or T shapes make a 3x3 bomb. Boards get holes. |
| 50–79 | Color Bombs | Five in a row makes a color bomb. Caged gems: matchable, not movable. |
| 80–109 | Fog & Combos | Fogged gems hide their color. Six makes a cross. Swapping two specials combos. |
| 110–139 | Diagonals | Diagonal blasters spawn from the supply. Locks open on a timer. |
| 140–169 | Seekers | Seekers spawn and blast wherever you stand when they go off. |
| 170–199 | Closing Cages | Cages close on random gems during play. Bigger boards. |
| 200–229 | Shapeshift | Your color changes every 15 seconds, with a countdown. |
| 230–250 | Rush Hour | Every 20 seconds the AI doubles its pace for five. |

Endless mode has infinite gems and moves, every mechanic, and a best score.

## Controls

- Arrow keys, WASD, tap or swipe: swap yourself with a neighbour
- 1 / 2 / 3: use a power-up
- P: pause, Esc: pause or back to the menu, M: mute
- Left / right arrows on the menu: change tier page

## Specials

Every special keeps the color of the gem it came from and wears a badge that
shows its blast shape. When it goes off, a beam, ring, streak or flash shows
exactly what it hit. Combos: two blasters make a cross, bomb plus blaster
clears three rows and three columns, two bombs make a 5x5 mega blast.

## Tuning

All knobs are constants at the top of the script in `last-match.html`:

| Constant | What it does |
| --- | --- |
| `AI_SPEED` | Global multiplier on AI pace, on top of each level's generated speed. |
| `AI_PACE` | Base timings in seconds: first move delay, think gap, telegraph hover, ramp length, aggression. |
| `TIERS` | Tier names, descriptions and the mechanic each one adds. |
| `makeLevel(n)` | The generator: board size, colors, AI move budget, supply, speed and mask per level. Deterministic per level number. |
| `HOLE_PATTERNS`, `CAGE_PATTERNS`, `FOG_PATTERNS` | Mask shapes the generator picks from. |
| `SCORE` | Points per second survived, per dodge, per AI swipe, per combo, per unlock. |
| `METER_TIME` | Seconds of survival per power-up. |
