# Last Match

A reverse match-3 prototype. You are the gem that woke up, and an AI player is
trying to match you off the board. Survive until it runs out of moves.

Open `last-match.html` in a browser to play. No build step, no dependencies
beyond two Google Fonts.

## Controls

- Arrow keys, WASD, tap or swipe: swap yourself with a neighbour
- 1 / 2 / 3: use a power-up
- P or Escape: pause
- M: mute

## Tuning

All knobs are constants at the top of the script in `last-match.html`:

| Constant | What it does |
| --- | --- |
| `AI_SPEED` | Single multiplier for AI pace. 1 is base, 2 is twice as fast. |
| `AI_PACE` | Base timings in seconds: first move delay, think gap, telegraph hover, ramp length, aggression. |
| `AI_MOVES` | Swipes the AI gets per round. Zero left means you win. |
| `SUPPLY_START` | Gems in reserve for refills. When it hits zero the board drains. |
| `METER_TIME` | Seconds of survival per power-up. |
