# 08 — Progression: the talent tree

## What was wrong with stat points

Three stats — Damage, Health, Cooldown — each bought with flat points at +2 / +15 / −4%
per point. The problem was not that there were only three. It was that **all three only
mattered during the boss fight**, which is roughly fifteen seconds. Nothing a player
bought changed the forty seconds of *running* that is most of the game, so progression
was attached to a loop it did not reward.

## The tree

Three branches of three tiers, one point per node, twelve nodes total.

| | Tier 1 | Tier 2 — pick one | Tier 3 — capstone |
|---|---|---|---|
| **Warlord** | Keen Edge, +4 Might | Cleave, +15% Might · **or** · Executioner, +40% spell | Annihilation |
| **Warden** | Thick Hide, +25 Vigor | Bulwark, +1s shield · **or** · Bramble, −35% pack cost | Undying |
| **Zealot** | Avarice, +12% from gates | Zeal, +12% speed · **or** · Fortune, +30% rare loot | Multiplication |

Tier 2 requires its branch's tier 1; tier 3 requires two nodes in the branch. **The two
tier-2 nodes exclude each other** — that is where build identity comes from, and it means
a fully-invested branch costs three points, not four.

**Zealot is the branch that fixes the original flaw.** Gate yield, run speed, enemy
resist and loot fortune all pay off on the road, not at the boss.

## New stat axes

`GateYield`, `RunSpeed`, `EnemyResist`, `ShieldDuration`, `SpellPower`, `Fortune` join
the original three. All start at zero, so a player who has spent nothing plays exactly
the game they played before.

Gate yield deserves a note: it amplifies **what a gate gained**, not its printed value.
A `+10` at 20% yield gives 12; a `×2` on 50 force gains 50 and so gives 60 rather than
120. One rule for both operators, and a gate that *costs* force is untouched — yield is a
reward, not a shield.

## How it composes

Talents emit `StatModifier`s into the same `StatSheet.Resolve` path gear uses, so a node
and an affix stack exactly as two affixes do. There is no second set of rules to keep in
step, which is the whole reason to route them through one pipe.

## How a stat reads

`Core/Stats/StatFormat.cs` is the single place that decides units, because the first
version decided them in two places with two different rules and shipped both wrong.

There are **two independent reasons** to print a percentage, and getting one right while
getting the other wrong is how this broke twice, in opposite directions:

| stat is a fraction | kind is `Percent` | correct | broken by |
|---|---|---|---|
| no | no | `+2 Might` | — |
| no | **yes** | `+5% Might` | choosing units by the **stat** alone |
| **yes** | no | `+1% Focus` | choosing units by the **kind** alone |
| **yes** | **yes** | `+15% Focus` | — |

`ModifierKind.Flat` vs `Percent` says how a modifier *composes* — added into the base, or
multiplied over the total (`final = (base + flat) × (1 + pct)`) — so a `Percent` modifier
is a percentage whatever units the stat carries. Separately, `Cooldown`, `GateYield`,
`RunSpeed`, `EnemyResist`, `SpellPower` and `Fortune` are stored as fractions of 1, so a
**flat** `0.01` on Cooldown is one percent. **A plain number is correct only when both are
absent**, which is why `Affix` requires the kind rather than defaulting it.

Choosing by kind alone printed the Ember Talisman as `+0.01 Focus`. Choosing by stat alone
printed seven of the fifteen shipped items — every Percent affix on Might or Vigor — as
`+0.05 Might`. `ShieldDuration` is the one run-axis stat that is genuinely absolute; the
docs on `StatIds` call it "extra seconds", and a test pins it.

`Cooldown` is also stored **positive** as a reduction (both `SpellSystem` and
`ShieldSystem` compute `1 − min(0.6, Cooldown)`), so higher is better and it must never
print with a leading minus — the old line rendered a 12% bonus as `Focus -12 %`, reading
as a penalty.

Zero is also collapsed onto *positive* zero before formatting. .NET renders negative zero
as `-0`, and a hard-coded minus in front of a base Cooldown of 0 produced `Focus -0 %` on
the main menu.

Both bugs were found from device screenshots, not from tests, and both now have tests.

## Taking it back

Every choice is reversible, free and unlimited. A learned talent is still a live button:
the first tap arms the undo and says which talent it will forget, the second spends it
and hands the point back. **FORGET ALL** empties the tree in the same two taps. Both arms
expire after four seconds, so backgrounding the app mid-decision never leaves a one-tap
undo waiting on resume.

Removal is **leaf-first**. A node of tier T could only have been bought with T−1 others
beside it, so pulling one out from under a capstone would leave the capstone standing on
nothing. `SkillTree.UnlearnBlockedReason` refuses those taps and *names the deepest node
that has to come off first* — pointing at the middle node instead would send the player
down a dead end. A property test takes every talent that can legally be taken across all
three branches and peels the build apart one node at a time with no respec available,
proving there is no reachable build a player can get stuck holding.

Free respec is the right call at this depth: the tree is met three talents in, long
before it can be read, so a build that cannot be walked back is a trap rather than a
choice. Charging for it is a lever worth pulling only once commitment means something.

## Migration

Schema **v4**. Points already spent on the old three stats are **refunded as unspent
points**: the old stats have no faithful mapping onto talents, and a free respec is the
honest trade rather than guessing an equivalent build.

---

# Save slots

Three independent saves, chosen on the first screen. Each slot is its own file
(`profile_0.sav` … `profile_2.sav`) with its own level, talents, gear **and tutorial
progress** — the coach re-latches on every slot switch, so a fresh slot is coached and a
veteran slot is not.

Erase is per-slot and takes two taps, with the armed state expiring after four seconds so
that backgrounding the app mid-decision cannot leave a one-tap wipe waiting on resume.

**Adopting the old save.** Every build before slots wrote a single `profile.sav`. The
first time slot 1 is opened it *moves* that file in rather than ignoring it, so an
existing player finds their game where they expect it. Slot file names deliberately
differ from the legacy name: sharing it would mean erasing slot 1 deletes the file the
adoption still looks for.

**Nothing is loaded until a slot is chosen.** The bootstrap starts on an empty
placeholder profile and `SaveProfile` is a no-op while no slot is active — otherwise the
placeholder would be written over whichever file the service happened to point at.

A note on the recommendation: this genre's convention is one cloud-synced profile per
device, and slots are a console-RPG idea. They were built because the project asked for
them; cloud save is still the right answer to "don't lose my game" and is needed for
monetization regardless.


# The difficulty curve, and the bug that made it two bugs

The player's report was *"the game is too easy"*. It was, and it was also unfinishable, and
both had one cause.

## The treadmill

`BossSim.BossHp` compounded a per-level growth on the **round** index. A boss is fought once
per **act**, and acts are three to five rounds. At the authored 0.25–0.29 that is about **3.0×
more health between one fight and the next** — against a player who grows 1.2–1.7× and, once
force hits the soft cap, **1.06×**.

Modelled across sixteen acts with the shipped numbers:

| act | boss HP | player dps | time to kill |
|---:|---:|---:|---:|
| 0 | 625 | 45 | **9 s** |
| 5 | 159,595 | 434 | 245 s |
| 8 | 2,397,537 | 975 | 27 min |
| 10 | 17,095,486 | 1,229 | **2 h 35 min** |

The first boss dies in nine seconds. The act-10 boss cannot be killed at all. No value of the
growth number fixes both, because the two ends need it to point in opposite directions.

## Why the player's curve changes shape

There is no gear that scales with level: the pool is fifteen fixed items. Talents are a finite
tree. The only endless source of power is paragon, which is **linear**. And the force reaching a
boss roughly doubles per act until `SoftCap` stops it — after which it is flat.

So the player's growth per act runs 1.68, 1.50, 1.40, 1.33, 1.28, 1.25, 1.22, 1.20, and then
**1.06 forever**. No fixed exponent can track a curve that changes shape like that.

## Pricing the boss against what the player provably has

`BossHp` now takes an **act** index and is scaled by two things the game already knows:

- the **army it will face**, through the same `CrowdFactor` the player's damage is multiplied
  by, evaluated at `ExpectedForceAtAct`. When the soft cap flattens the army it flattens the
  boss too, automatically and for the same reason;
- the **stat points handed out** by that act, which is arithmetic `BalanceSettings` already
  fixes.

**Gear and talents are deliberately excluded.** They are the player's edge: a player who invests
in them beats the curve, which is the incentive the whole loop is built on. Modelling them would
price that reward away.

What is left for the authored number is a **pressure** of a few per cent per act, meaning exactly
"a little harder than the last one" and nothing else. Hence `PerLevelGrowth` 0.25–0.29 becoming
0.05–0.07: not the same quantity made smaller, a different quantity.

Measured from the shipped code, for a player with **nothing but stat points**:

```
a0 17.1s  a1 20.9  a2 24.2  a3 24.1  a4 26.6  a5 31.5  a6 22.9  a7 28.8
a8 34.4   a9 34.2  a10 38.8 a11 47.3 a12 30.7 a13 39.7 a14 48.8 a15 48.5
```

A steady rise from 17 s to 48 s across sixteen acts, with the six-act sawtooth being the
archetypes' own base health. The first boss is **1.8× harder** than the nine seconds that drew
the complaint, and nothing is a wall. `TheWholeCampaignStaysInsideAPlayableWindow` walks all
sixteen and fails outside 5–60 s.

## The army was worth almost nothing

`1 + log10(1 + force)`. Across the entire game — sixty men at the first boss, a hundred thousand
at the cap — that factor moves **2.79 to 6.00**. The thing the player spends every second of
every run on paid **2.15× in total**, and a hundredfold army paid 1.7×.

The first replacement was a single power law, and **the project's existing diminishing-returns
test rejected it**: a single power law has a constant ratio between decades, and the `1 +` damps
the small end rather than the large one, so the curve *accelerates*. The comment written
alongside it claimed the opposite, plausibly and wrongly, and only the test knew.

Two segments, with the exponent **dropping** at a two-thousand-man knee — 0.40 below, 0.18 above
— diminish by construction, which is doc 01 R4's requirement that gear stay the long-term lever.
The weight is solved from one anchor: the factor at sixty men is held at 2.80 against the old
2.79, so no early fight is quietly made easier. What changes is the top.

| force | old | new |
|---:|---:|---:|
| 60 | 2.79 | 2.80 |
| 2,000 | 4.30 | 8.32 |
| 100,000 | 6.00 | 15.80 |

A hundredfold army now pays **3.5×**.

## Blows that can kill you

A blow took a fixed fraction of whatever is **left**, so every blow is smaller than the last and
the sequence approaches zero without reaching it. Measured: **24 unblocked blows** to wipe ten
thousand men, **30** for a hundred thousand. At a four-second attack interval that is a hundred
seconds of ignoring every telegraph, in a fight lasting seventeen. The shield, the timing game,
the entire reason a boss telegraphs, could be skipped and the player would still win.

It was backwards in shape too: a **bigger army survived more blows**, so playing the run well
bought passive safety as well as damage.

Blending a tenth of the army that *walked in* into the basis costs **eight** blows instead of
twenty-four, and makes the count nearly independent of army size — the army buys damage, Health
and the shield buy survival. Health mitigates exactly as before: eighteen blows at 150 Health.

With `referenceForce == force` the new overload is algebraically the old formula, so all five
existing `BossHit_*` tests are untouched rather than rewritten.


## The affixes, and the window they broke

The campaign window test was written against the **plain** boss, and that made it nearly
worthless: `Colossal` multiplies health by 1.40, and the affix rotation lands one on act 15.
Folding `BossAffixes.For(act)` into the test immediately failed it at **67.9 s** — a curve that
passed comfortably and would have shipped a sixty-eight-second boss fight.

The pressures come down from 0.042–0.070 to 0.042–0.062, and the Hollow Leech's base from 1360
to 1320. The pressure compounds on the act index, so easing it touches **only the late slope** —
act 0 is `(1 + p)⁰ = 1` either way and keeps every second of its new difficulty.

The curve that ships, measured, for a player with nothing but stat points:

```
a0 17s  a1 21  a2 24(Frenzied)  a3 23(Armoured)  a4 26  a5 30(Vampiric)
a6 22(Haunted)  a7 27  a8 45(Colossal)  a9 30(Frenzied)  a10 36(Armoured)
a11 44  a12 28(Vampiric)  a13 36(Haunted)  a14 44  a15 57(Colossal)
```

The Colossal champions at acts 8 and 15 are the spikes, which is what a champion should be: a
noticeably longer fight, not a differently-coloured one.

## One thing the plan asserted that measurement did not support

The plan claimed the multiply gates were mispriced — "a ×3's prize is proportional to force
while its price is linear in round", making the multiply effectively free at depth. Modelled
against the generator, with the multiplies compounding as a real run does:

| round | step | force | Fork toll | share |
|---:|---:|---:|---:|---:|
| 0 | 6 | 41 | 20 | 48.7% |
| 5 | 6 | 95 | 40 | 42.1% |
| 20 | 6 | 257 | 100 | 38.9% |
| 30 | 6 | 365 | 140 | 38.3% |

The share is **near-constant**, because `AddValue` and the banked force both scale linearly in
the round index — they move together. There is a mild decay across thirty rounds and a larger
one *within* a round (48% at step 6 against 18% at step 12), and the latter is arguably the
point: a later chunk should pay better.

So no change was made. This is the third time in this increment that a measurement contradicted
the plan it was meant to implement — the per-stone tone, the single power law, and this — which
is an argument for the measuring, not against the planning.

---

# The standing army

> *"Now every round the crowd shrinks to minimum. I want the crowd to never shrink
> throughout the full game. And at the same time I want it to become more difficult. Like
> in an RPG game — once you achieved a certain level or crowd size, you never lose it."*

## What the game did, and why it read as losing

Every round called `CrowdController.ResetRun(level.StartingForce, 0f)` with
`StartingForce = 5`. A player who fought the act-one boss with 2,884 men began the next
round with five. Nothing was saved between rounds except the round number, the gear and the
tree — the army itself, which is the number on screen for the entire forty seconds of play
and the thing every decision in the run is about, was thrown away and re-mustered each time.

That is the genre's normal shape and it is not a bug. It is also, read as an RPG, a game
that takes your level away at the end of every quest.

## The measurement that decided the design

Carrying the army over is one line. The reason it is not a one-line change is what happens
next, and it was worth simulating before committing to anything. Walking the real generator
with the real gate arithmetic, an army carried unbroken between rounds against the *old*
absolute gates:

| round | start | peak | round ratio |
|---|---|---|---|
| 0 | 5 | 229 | 45.8× |
| 5 | 91,736 | 99,917 | 1.09× |
| 10 | 99,999 | 99,999 | **1.00×** |
| 20 | 99,999 | 99,999 | **1.00×** |

The soft cap of 100,000 is reached by **round five**, and every round after it is flat:
every gate on the road stops doing anything measurable. And the cap is not even the whole
problem. Because an add gate paid an absolute headcount, a round entered with 2,461 men was
worth **1.9×** against the 45× the same round pays at five men. Long before the ceiling, the
gate stops being an event.

So a continuous army needs proportional gates, and proportional gates need no cap. The two
changes are one change.

## A gate is a share

`GateMath` is now:

| gate | factor | at 47 men | at 63 billion |
|---|---|---|---|
| recruit `+` | ×1.026 per weight | +1 | +1.64B |
| rally `×` | ×1.30 at weight 2 | +14 | +18.9B |
| ambush `−`, top of a round | ×0.875 | −6 | −7.88B |
| ambush `−`, deep in a long round | ×0.409 | −16 | −21.8B |

The player still sees headcounts. A share is the mechanism; `GateMath.Headcount` resolves it
against the army actually approaching, so the sign on a gate reads `+340` and `−1.3K`
exactly as it did when those numbers were authored. What used to be the authored value is
now a small **weight**.

**The `×2` arch did not survive, and that was measured too.** Keeping literal doubling and
simulating sixty rounds, every lane-choice quality from 0.70 to 1.00 lands within one order
of magnitude of the same colossal number: rally gates dominate so completely that nothing
else the player does is detectable. Skill expression and the `×2` arch are the same trade,
and the arch lost. It is a ×1.30 now — still worth nearly seven recruit gates, still the
gate you steer for, still an arch rather than a crowd.

## Two numbers, and the difference between them is the design

`StandingArmy` holds the whole promise:

- **Banked** — the army as it stands, carried unbroken from the last round.
- **BestEver** — the largest army ever fielded, a high-water mark that only rises.
  `Floor` is 55% of it, and a round can never start below that floor.

So a disastrous round costs real ground — up to 45% of a career — and no sequence of
disastrous rounds can put the player back at the beginning. Neither number alone does this:
Banked without BestEver is a game that can ruin you permanently, and BestEver without Banked
is a game where the last round did not matter.

A floor of 100% was considered and rejected. It would make every loss notional, the army
restored in full at the next round's start, nothing in the run able to cost anything — which
is not an RPG level, it is an invulnerability, and the difficulty asked for alongside would
have had nowhere to land.

## The campaign, measured from the shipped code

Sixty-two rounds — the whole of the sixteen authored acts — walked with the real generator,
steering at a fixed quality between the worst lane and the best at every decision:

| lane quality | round 30 | round 62 | permanent floor | rank |
|---|---|---|---|---|
| 1.00 | 1.2×10²¹ | 3.6×10⁵⁸ | 2.0×10⁵⁸ | 153 |
| 0.90 | 8.27T | 6.5×10²⁰ | 3.6×10²⁰ | 77 |
| **0.85** | **574M** | **20.1T** | 11.1T | 42 |
| 0.80 | 134K | 662K | 396K | 18 |
| 0.72 | 25 | 38 | 78 | 3 |
| 0.65 | 0.9 | 1.3 | 6.9 | 1 |

**Break-even sits at about 0.72.** Below it the army does not grow, and the floor is what
stops that from being a spiral — a player at 0.65 holds rank 1 rather than being ruined, and
one at 0.80 holds rank 18 for twenty rounds until they play better or buy stats. That is the
difficulty, arriving where it was aimed: the ambush weight and the depth ramp both climb
while the recruit share does not, so an average line stops being enough.

It is also why par collapses. `EstimateParForce` at `ParKeepFraction = 0.6` falls from 5.5×
the incoming army at round 0 to 1.5× at round 15 and bottoms out after that. That is not an
error in the estimate; it is the same fact seen from the other end. Revives are consequently
sized against the army that walked in, not against par — a revive sized off par would have
handed a deep-run player fewer men than they started the round with.

## Force is a double

At the measured growth of a competent player a `long` (9.2×10¹⁸) overflows around **round 52**
— inside the authored content, so this is not a theoretical ceiling. A `double` holds the
same campaign with two hundred rounds to spare, is exact below 2⁵³ (nine quadrillion, far
past any headcount a player reads individually), and needs no saturation reasoning at all.
An army of ten billion men does not need to be exact to the man.

The HUD reads it through `StatFormat.Army`: exact below a thousand, three significant figures
and a suffix above, out to Decillion, with scientific notation past the end of the table
rather than a lie about the magnitude.

## The boss, re-priced again

`ExpectedForceAtAct` — `60 × 2.2^act`, capped — was a closed-form guess at what a competent
player carries. It was reasonable while the army reset every round and the cap put a ceiling
over the whole game. With the army continuous it cannot be fair to anyone: a player at 0.80
and one at 0.90 end eight orders of magnitude apart, and a boss priced for a ladder between
them is a formality for one and a wall for the other.

So the boss is priced against **the army that actually walks into it**, through the same
`CrowdFactor` the player's damage is multiplied by. The two cancel exactly, and the result is
a much stronger property than the one it replaces:

> The fight lasts about as long whether the player arrives with four hundred men or four
> hundred trillion. The run decides whether you *arrive*; the shield, the spell and the affix
> decide whether you *win*.

Gear and talents stay outside the model, so they still show up as a shorter fight — that is
the player's edge and the whole incentive of the loop.

The roster was re-based for the new anchor (bases ×0.60, pressures eased to 0.036–0.051),
because with `CrowdFactor` cancelling on both sides the old bases landed the first fight at
29 s and the act-8 Colossal at 76 s. Measured from the shipped code, for a player with
nothing but stat points:

```
17s 21 24* 22* 25 29* 21* 26 43* 29* 33* 39 26* 33* 40 51*      (* = affix)
```

## Migration

Schema v6 adds `ArmyBanked` and `ArmyBestEver` and **deliberately leaves them at zero**,
which `StandingArmy` already reads as "muster the seed". Seeding the army from
`CurrentLevelIndex` was the obvious alternative and is wrong: under v5 a round's force was a
function of that round alone, so a save at round twenty says nothing whatsoever about how
large that player's army got. Inventing a number from it would hand some players a rank they
never earned and take one from others. Gear, tree and round are all kept; only the army
starts from the seed, and one round of good play is worth a great deal at the bottom of a
proportional curve.

---

# The abilities: magazines, and what they act on

> *"Now the spell doesn't destroy enemy packs and shield doesn't block against them, only
> against bosses I think. I want shield and spell to work against all, including gates can be
> eliminated by the spell. And a magazine of spells instead of one at a time with skill nodes
> to add charges and the skills to add the shield counts and the shield active time."*

## The spell could not be aimed

`ClearEnemiesAhead(crowd.CenterZ, 15f)` looked correct and was not. The crowd's leading
plane stands up to `CrowdMath.FrontDepthMax` — **seven metres** — ahead of its centroid, so
seven of the spell's fifteen metres were spent on road the army was already standing on.
What reached ahead was about eight metres: at ten metres a second, **under one second of
travel**, which is less time than it takes to see an ambush and flick at it.

It was not a short-ranged spell. It was a spell that could only ever hit things already too
close to avoid, which is indistinguishable from a spell that does nothing.

It now sweeps from the army's **front**, and the range is 34 m.

## It only ever touched packs

The second half is simpler: `ClearEnemiesAhead` iterated `_activeEnemies` and nothing else.
Since v0.20 a subtract gate is drawn as a crowd of men in the road — so most of the red a
player sees is a *gate*, and gates walked straight through a spell aimed at them. Same for
the shield: `OnEnemyContact` returned early when the shield was up, and `OnGateApplied` had
no such check at all.

From the player's seat those two are the same object, the same colour, doing the same thing.
A shield that stops one and not the other does not read as a rule; it reads as a shield that
does not work.

Both now act on **ambushes** — packs and red gates alike. `ClearAmbushesAhead` destroys
either; a raised shield nullifies either.

And blocking is no longer silent. It used to cost nothing and show nothing, which — with the
old absolute pack cost of a few dozen men against an army of hundreds — was a difference too
small to notice even when it was *not* blocked. Both halves of "the shield doesn't block
against them" were true at once. A block now throws a ring and a burst in the Warden's
colour, and a pack costs a real share of the army.

## A magazine, not a boolean

Both abilities were a single flag on a cooldown: you had it or you did not, and the only stat
that touched it made the wait shorter. That is a fine mobile verb and a poor RPG one, because
there is nothing to spend a point on that changes *how* the ability is used.

`Core/Run/Magazine` gives both a pool of charges with one rule that matters:

> **The refill timer runs whenever the magazine is short, not only when it is empty.**

That is what makes a second charge worth a talent point rather than merely convenient — with
three charges a player can spend two on a dangerous chunk and still have the third when the
next arrives, because the refill did not wait for them to run dry. A magazine that only
started refilling once spent would make the second and third charge strictly worse than the
first.

Widening the magazine does **not** hand out the new charges. Otherwise equipping a charge
talent mid-run would be a free cast, and a stat that pays out on the frame it is applied is
one players learn to toggle rather than to build around.

## What buys it

Two new stats, `SpellCharges` and `ShieldCharges`, both counts of *extra* charges — so a
player who has bought none gets exactly the ability that shipped before.

| branch | tier 2 | tier 4 | keystone |
|---|---|---|---|
| Warlord | **Full Quiver** — hold a second spell | **Arsenal** — hold a third | **Tempest** — two more, 45% echo, +25% Focus |
| Warden | **Doubleguard** — hold a second raise | **Triguard** — hold a third | **Mirror of Thorns** — two more, blocked blows return 90% |

Three points across a whole branch, because a charge is worth far more than a tenth of a
second of uptime and pricing them the same would make duration dead. Shield duration and
cooldown keep their existing lines (Brace, Warded, Bulwark, Aegis; At the Ready is new).

The charge payoff on the keystones is **folded into existing keystones** rather than added as
a fourth. Three mutually exclusive keystones per branch is a design rule with a test on it,
and quietly making it four to fit a new stat in would change the shape of every build in the
game to avoid an edit.

## The HUD

`SetCooldowns` became `SetAbilities`, because "SPELL 2.1s" is the right readout for something
you either have or do not and the wrong one for something you hold a stock of. It reads
`SPELL ^ 2/3` with a dot tail for the refill, and stays exactly `SPELL ^` at capacity 1.

Filled and hollow diamond pips were the first version and were reverted: U+25C6 and U+25C7 are
Geometric Shapes, the HUD draws in Unity's built-in font, and a glyph missing there renders as
a box on device with no way to find that out from here. A count always renders. If a device
screenshot shows it reading poorly mid-run, pips with a bundled font are the fix — not a guess
at what Arial happens to carry.
