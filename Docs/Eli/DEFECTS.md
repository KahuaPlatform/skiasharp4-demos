# Eli — Defect history

Every defect found between "the design was approved" and "the game shipped", who found it, and what
the fix was. Kept because the *how it was found* column turned out to be the interesting part: the
two that mattered most to how the game actually plays were found by playing it and by looking at a
trace, not by the build succeeding.

Companion to [DESIGN.md](DESIGN.md) (the plan, plus an `As built` section covering the same
divergences from the design's point of view) and [Design-prompt.md](Design-prompt.md) (the Phase 1
prompt).

## Summary

| # | Defect | Found by | Found how | Severity | Status |
|---|---|---|---|---|---|
| 1 | A just-harpooned enemy still killed the digger | AI | Headless sim harness | High — unavoidable death | Fixed |
| 2 | Attract bot charged into contact, losing all 3 lives in ~20 s | AI | Attract-mode trace | Medium — poor demo loop | Fixed |
| 3 | **Digger could walk into the sky and cross the whole map without digging** | **User** | **Playing / reading the design** | **High — trivialised every level** | Fixed |
| 4 | Flow field flooded the sky band, joining every tunnel | AI | Investigating #3 | High — gutted the phasing AI and Level 4 | Fixed |
| 5 | Dig-speed penalty barely applied (~115 px/s vs an intended 64) | AI | Investigating #3 | Medium — digging felt like walking | Fixed |
| 6 | Solution GUID duplicated from Koa | AI | Pre-flight check on the copied scaffold | Low — latent IDE confusion | Fixed |
| 7 | **Digger and enemies walked straight through settled boulders** | **User** | **Playing / reading the code** | **High — a core obstacle wasn't an obstacle** | Fixed |
| 8 | Attract bot stalled, firing into a dirt wall | AI | Flakiness sweep (12 runs) | Medium — *regression introduced by the fix for #2* | Fixed |

Nothing was found by the compiler. **Every build — desktop and wasm, zero warnings — passed at every
point in this list**, including while defects 3, 4, 5 and 7 were live. Worth stating plainly: a green
build said nothing about any of these.

Two patterns worth pulling out:

- **Both defects the user found (#3, #7) were "something that should be solid wasn't."** Neither was
  reachable from the design document, which described both correctly in prose. #7 was live in the
  design *and* survived the first verification pass, because every check written for boulders
  exercised the falling branch — the one state that had collision.
- **One fix caused the next defect.** #8 is a regression introduced by the fix for #2, and it was only
  caught by running the suite repeatedly rather than once — see
  [Flaky checks](#flaky-checks--found-by-repetition-not-by-a-single-run).

---

## 1 — A just-harpooned enemy still killed the digger

**Found by:** AI, via the headless sim harness during Phase 2 verification.
**Severity:** High. An unavoidable death with no counterplay.

The design says "being touched by an un-inflated enemy kills you". Implemented literally, contact
detection skipped an enemy only when `Pinned && Inflation > 0f`. That left two holes:

- On the frame the harpoon strikes, `Inflation` is still `0`, so a monster you had *already speared*
  killed you at point-blank range.
- The hole reopened every time `Inflation` decayed back to exactly zero just before the harpoon
  detached.

The harness surfaced it as `pumping bursts an enemy :: live 4 -> 4`, and a frame-by-frame trace showed
`lives=2` on frame 0 — the digger was dying before the harpoon could act.

**Fix:** `GameWorld.HandleEnemyContact` ([GameWorld.cs:696](../../Source/Eli/Eli/Game/GameWorld.cs#L696))
now skips any enemy with `Pinned == true`, full stop. A harpooned enemy is neutralised for as long as
it stays on the hook; `DetachHarpoon` clears `Pinned`, so it turns lethal again the instant it works
loose. A *phasing* enemy is still lethal, as designed.

**Regression cover:** two harness checks — `a pinned enemy never costs a life` and
`point-blank harpoon does not cost a life`.

---

## 2 — Attract bot charged into contact

**Found by:** AI, from a 20-second attract-mode trace.
**Severity:** Medium. Not a correctness bug; a bad demo loop.

The design specified only *when* the bot fires ("an enemy within `AutoFireRange` along its facing
axis"). With nothing telling it to stop, the bot walked into whatever it was aiming at. Trace over
20 s: deaths at t≈3 s, t≈5 s, t≈19 s — all three lives gone, score 0.

**Fix:** `AutoStandoffRange = 76f` ([GameWorld.cs:60](../../Source/Eli/Eli/Game/GameWorld.cs#L60)); once
a target is lined up inside harpoon reach the bot holds its ground and shoots
([GameWorld.cs:846](../../Source/Eli/Eli/Game/GameWorld.cs#L846)). Same 20 s window afterwards:
**0 deaths, 1300 points, 3 of 4 monsters cleared.**

---

## 3 — The digger could walk into the sky *(found by the user)*

**Found by:** the user, on seeing the game run — *"does the player move into the sky? that doesn't
seem correct - they should at least be pinned to the surface"*.
**Severity:** High. Trivialised every level.

The design's own solidity table had `Sky` as **open** to the digger, described as "the open surface
band above the dirt: walkable, never diggable". That read as harmless on paper. In play it was not:

```
after 2s of UP    : cell=(6,0) tile=Sky        <- row 0, the very top of the map
after 10s of RIGHT: cell=(42,0) tile=Sky       <- 36 columns crossed, digging=False
```

The digger climbed a full cell clear of the ground and then ran the **entire field width at walk
speed without digging at all** — a free highway over every level, bypassing the one mechanic the game
is about.

This is the defect the design phase should have caught and didn't. The review gate read the sentence
"walkable, never diggable" and did not ask what a walkable connected band across the top of the map
does to a game about tunnelling.

**Fix:** `Sky` is solid to all three predicates —
`IsBlockedForDigger` ([Field.cs:88](../../Source/Eli/Eli/Game/Field.cs#L88)),
`IsBlockedForEnemy` ([Field.cs:105](../../Source/Eli/Eli/Game/Field.cs#L105)) and
`IsBlockedForHarpoon` ([Field.cs:116](../../Source/Eli/Eli/Game/Field.cs#L116)). The sky band is now
scenery drawn above the field; the topmost dirt row is the surface and the digger is pinned to it,
which is also what the arcade original does. `Renderer.IsSolid`
([Renderer.cs:143](../../Source/Eli/Eli/Game/Renderer.cs#L143)) follows, so digging open the surface
gets the same glowing edge as any other tunnel wall.

**Regression cover:** `digger cannot climb into the sky` and
`traversing the surface still costs dig speed` (74 px/s, not 132).

---

## 4 — The flow field flooded the sky band

**Found by:** AI, while investigating #3. A second-order consequence the user's report exposed.
**Severity:** High, and invisible without instrumenting it.

`Field.IsWalkable` is defined as the inverse of `IsBlockedForEnemy`, so an open sky band was open to
the **flow field** as well:

```
sky cells walkable by ENEMIES / flow field: 84 of 88
```

A connected 84-cell corridor across the top of the map joined every tunnel to every other tunnel.
Two mechanics quietly stopped working:

- **The phasing AI.** An `Uhane` phases when `!Pathing.Reachable(e.Pos)` — when there is no tunnel
  route to the digger. With the sky joining everything, almost nothing was ever unreachable, so the
  game's headline "one enemy type can leave the tunnels" behaviour fired far less than intended.
- **Level 4 "Papakū".** Its whole design is four bedrock-walled quadrants "connected ONLY across the
  surface band" — i.e. you must *dig* across the top. Enemies simply walked over it instead, and
  `Nohu` (which cannot phase) were no longer quadrant-locked.

**Fix:** covered by #3 — `Sky` solid in `IsBlockedForEnemy` closes it. Now `0 of 88`.

**Regression cover:** `sky is closed to the flow field`.

---

## 5 — The dig-speed penalty barely applied

**Found by:** AI, from the trace taken while verifying #3.
**Severity:** Medium. Digging felt like walking, which is the core mechanic.

The verification trace for the sky fix showed the digger crossing 36 columns of virgin dirt in 10 s —
115 px/s, against a `DigSpeed` constant of 64 and a `WalkSpeed` of 132. Digging was running at ~87 %
of walking speed.

Cause: `MoveDigger` probed a **fixed pixel distance** ahead of the body,
`Digger.Radius + 2f` = 12.9 px. A cell half-width is 16 px, so the probe never reached the next cell —
it just re-read the cell the digger had *already carved to `Tunnel` that same frame*, and reported
"not dirt".

This one is a good example of a defect that a passing test hid: the original harness check was
`walking a tunnel beats digging`, which was *true* (144 vs 121 px/s) and green, while the tunable it
was supposed to protect was off by 80 %.

**Fix:** the test is cell-based — "is the next cell along my facing still `Dirt`?"
([GameWorld.cs:288](../../Source/Eli/Eli/Game/GameWorld.cs#L288)). Measured afterwards:

| | Before | After | Constant |
|---|--:|--:|--:|
| Dig (virgin dirt) | ~115 px/s | **74.6 px/s** | `DigSpeed` 64 |
| Walk (own tunnel) | 132 px/s | 132.0 px/s | `WalkSpeed` 132 |
| Walk : dig ratio | 1.15 | **1.77** | intent ≈ 2.06 |

The residual gap (74.6 against a constant of 64) is real and intentional: the digger's body carves the
cell ahead ~8.7 px before its centre crosses the boundary, so the last stretch of each cell runs at
walk speed. That "break through" tail is good feel, and the tunable table in
[DESIGN.md](DESIGN.md#tunables--starting-values) now records the effective rate alongside the constant
rather than pretending they are the same number.

**Regression cover:** three checks that assert *magnitude*, not just ordering —
`walking a carved tunnel runs at WalkSpeed`, `digging is materially slower than walking`,
`dig rate tracks the DigSpeed tunable`.

---

## 6 — Solution GUID duplicated from Koa

**Found by:** AI, pre-flight check on the copied scaffold.
**Severity:** Low, latent.

`Source/Eli/` is a copy of `Source/Koa/`, so `Eli.sln` initially carried Koa's project GUID
`{A4F0148C-…}`. Harmless to the build (each demo has its own solution), but it confuses IDEs when both
solutions are open and would have been an odd thing to debug later.

**Fix:** fresh GUID `{CA8165B2-9FAB-4F5D-B2F0-DEFC11CD5B3C}` ([Eli.sln:6](../../Source/Eli/Eli.sln#L6)).

---

## 7 — Digger and enemies walked through settled boulders *(found by the user)*

**Found by:** the user — *"can the player and enemies pass through a boulder? Shouldn't they be
solid?"*
**Severity:** High. A boulder is one of the game's three obstacles, and it wasn't one.

Boulders are entities, and every collision test for them lived inside `StepFallingBoulder` — the
*falling* branch, where a boulder crushes what it hits. A `Settled` or `Wobbling` boulder had **no
collision of any kind**. Measured: the digger walked centre-on-centre through one and out the far side.

```
digger vs settled boulder: endX=1062.1  boulderX=592.0  closest gap=-25.6 px  => PASSED CLEAN THROUGH
```

There was a second half to it. Once the digger tunnelled through the boulder's cell, that cell became
`Tunnel`, so `IsWalkable` opened it up and the **flow field routed the swarm through the boulder** too.

This one survived the first verification pass, and the reason is instructive: four boulder checks
already existed (`starts Settled`, `losing support starts the wobble`, `wobble elapses into a fall`,
`the boulder actually fell`) and all four passed. Every one of them exercised the falling state — the
one state that *had* collision. Nothing tested the state a boulder spends most of its life in.

**Fix:** `Field` gained a one-cell boulder-occupancy overlay
([Field.cs:47](../../Source/Eli/Eli/Game/Field.cs#L47),
[:85](../../Source/Eli/Eli/Game/Field.cs#L85)) folded into all three solidity predicates. Boulders stay
entities — the design's reason for that (a tile cannot hold a sub-cell Y while one falls) is unchanged;
the overlay is only how a cell-aligned entity makes itself felt by the tile-based predicates. So:

- `MoveCircle` stops the digger and enemies flush against the boulder's face — measured closest gap is
  now **+1.3 px**, touching rather than overlapping;
- because `IsWalkable` is the inverse of `IsBlockedForEnemy`, the flow field routes the swarm *around*
  it, with the corridor either side still open;
- the cell is released the instant the boulder starts falling
  ([GameWorld.cs:615](../../Source/Eli/Eli/Game/GameWorld.cs#L615)) — from then on it crushes rather
  than blocks — and the vacated cell becomes `Tunnel`, leaving the void it tore out of the earth as a
  usable passage.

**Regression cover:** seven checks, deliberately covering the *settled* state the original four
missed — `digger cannot walk through a settled boulder`, `boulder cell is closed to the flow field`,
`the corridor either side of it stays open`, `a settled boulder occupies its cell`,
`a falling boulder releases its cell`, `it leaves a passage where it tore free`, and
`a falling boulder still crushes the digger` to prove the fix didn't break what already worked.

---

## 8 — Attract bot stalled, firing into a dirt wall *(a regression from fixing #2)*

**Found by:** AI, by running the suite 12 times instead of once.
**Severity:** Medium. Self-inflicted — introduced by the fix for defect #2.

The standoff added in #2 halted the bot whenever a target was "aligned", which was only a dot product
on direction. It had no notion of whether the shot could land. A monster on the far side of an undug
wall satisfied it, so the bot would stop and fire into dirt indefinitely: one sampled session moved
**13 px in 20 seconds**.

It also ignored phasing monsters entirely — they are excluded from targeting but still kill on contact
— so standing still was a good way to be picked off by a ghost.

**Fix:** the halt now requires a shot that can actually be landed — target inside `HarpoonMaxLength`,
a clear line of sight sampled in half-cell steps (`HarpoonPathClear`,
[GameWorld.cs:885](../../Source/Eli/Eli/Game/GameWorld.cs#L885)), and no phasing monster closing
(`GhostClosing`, [GameWorld.cs:905](../../Source/Eli/Eli/Game/GameWorld.cs#L905)). The separate
`AutoStandoffRange` constant was deleted; the harpoon's own reach is the natural bound.

Across five 20-second sessions afterwards: **scores in 5/5, survives 5/5, 5000 points, 131 cells dug.**

Death attribution over 20 sessions confirms the residual deaths are fair fights, not a logic trap:

| Cause | Deaths |
|---|--:|
| Tunnel monster | 20 |
| Phasing ghost | 1 |
| Falling rock | 0 |

---

## Flaky checks — found by repetition, not by a single run

The verification suite passed, then failed, then passed again on identical code. `GameWorld` seeds a
clock-based `static Random`, so no attract session is reproducible. Running the suite **20×** exposed
four unstable checks — every one of them a bad *assertion*, not a bug:

| Flaky check | Why it was wrong | Now |
|---|---|---|
| `attract bot survives 20s` | Demanded a stochastic outcome. Losing three lives and dropping back to Title is *designed* behaviour. | Reported as information, not gated |
| `attract bot moves` | Measured **net displacement**, so a bot that wandered back near its start scored as motionless. | Accumulates path length per tick |
| `attract bot digs` | Per-session floor of 10 cells; a run spent inside Level 3's pre-carved warren legitimately carves less. | Aggregated across sessions |
| `attract bot scores` | Required a kill in 4 of 5 individual 20 s windows. | Aggregate score across sessions > 0 |

The lesson worth keeping: **a check that passes once has not been shown to pass.** Defect #8 was only
visible because the suite was run repeatedly, and it was hiding behind assertions loose enough to go
green anyway.

---

## Not defects — harness artifacts

Three red results during verification were bugs in the *throwaway test harness*, not in the game.
Recorded because each one initially looked like a product defect, and mistaking them for one would
have meant "fixing" correct behaviour:

| Symptom | Actual cause |
|---|---|
| `pumping bursts an enemy :: live 4 -> 4` (second occurrence) | The test teleported the target into solid dirt. It correctly had no tunnel route, correctly started phasing, and a phasing enemy is *deliberately* immune to the harpoon. Rewritten to carve a corridor first. |
| `attract bot digs :: dirt 1061 -> 1069` (dirt *increased*) | A naive before/after count straddled a level advance, which swaps the whole field. Rewritten to accumulate only per-tick decreases. |
| `WALK up: 173.7 px/s` — faster than `WalkSpeed` | The measurement window contained a death and respawn; the teleport back to spawn was counted as displacement. Rewritten to time a window inside the shaft's own length. |
| A boulder test reported the digger both "stopped by the boulder" *and* "ended past" it | The test called `Enemies.Clear()` to isolate locomotion — but an empty enemy list trips `CheckFieldClear`, which silently advanced to Level 2 and respawned the digger, leaving a stale boulder reference from Level 1. Tests now **freeze** enemies rather than removing them. |

## Process note

`Docs/Eli/DESIGN.md` did not exist when implementation was requested — the file at that path was the
**Phase 1 design prompt**, not a design. The two-phase workflow in
[09 – Authoring a New-Game Prompt](../Architecture/09-Authoring-A-New-Game-Prompt.md) had not been run.
Phase 1 was executed first and reviewed before any code was written; the prompt is preserved at
[Design-prompt.md](Design-prompt.md).

The review gate did its job on architecture and caught nothing about behaviour — defect 3 sat in the
approved design as an explicit table entry (`IsBlockedForDigger` / `Sky` / **open**) and was only
caught by watching the game run.
