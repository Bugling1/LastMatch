# Last Match

A reverse match-3 prototype. You are the gem that woke up, and an AI player is
trying to match you off the board. Survive until it runs out of moves.

Open `last-match.html` in a browser to play. No build step, no dependencies
beyond two Google Fonts.

## Modes

Six levels plus Endless. Levels unlock in order and progress is saved in the
browser.

| Level | Board | Twist |
| --- | --- | --- |
| 1 Warm-up | 7x7, 4 colors | Learn the dodge |
| 2 Sentience | 8x8, 5 colors | Faster hand |
| 3 The Pit | 8x8 with holes | Gravity works per column segment |
| 4 Cold Storage | 9x8, caged corners | Cages open on a timer or when a neighbour clears; cross blasters spawn |
| 5 Fog Bank | 9x9, fogged corners | Fogged gems are hidden and unmatchable until revealed; diagonal blasters spawn |
| 6 Seeker Swarm | 9x9, holes and cages | Seekers home in on you when they go off |
| Endless | 8x8 | Infinite gems and moves, cages close in over time, chase the best score |

## Controls

- Arrow keys, WASD, tap or swipe: swap yourself with a neighbour
- 1 / 2 / 3: use a power-up
- P: pause, Esc: pause or back to the menu
- M: mute

## Specials

Four in a row makes a row or column blaster, an L or T makes a bomb, five makes
a color bomb, six makes a cross blaster. Swapping two specials together sets off
a combo: two blasters make a cross, bomb plus blaster clears three rows and
three columns, two bombs make a 5x5 mega blast. Later levels also spawn diagonal
blasters and seekers from the supply.

## Tuning

All knobs are constants at the top of the script in `last-match.html`:

| Constant | What it does |
| --- | --- |
| `AI_SPEED` | Global multiplier on AI pace, on top of each level's `speed`. |
| `AI_PACE` | Base timings in seconds: first move delay, think gap, telegraph hover, ramp length, aggression. |
| `LEVELS` | One entry per level: size, colors, AI move budget, gem supply, speed, board mask, unlock timer, special spawn rates. |
| `SCORE` | Points per second survived, per dodge, per AI swipe, per combo, per unlock. |
| `METER_TIME` | Seconds of survival per power-up. |

Board masks use `#` for an open cell, `.` for a hole, `L` for a caged gem and
`H` for a fogged gem.
