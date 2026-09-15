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


---

# The gate that promised a man it could not deliver

> *"at the first level the adding +1 doesn't add anything, multiplication does. At the next
> levels addition works."*

Exactly true, and the cause was a disagreement between two functions that were supposed to
describe the same event.

`GateMath.Headcount` — which writes the number on the gate's sign — promoted any positive sub-1
delta to `1`, because a gate reading "+0" in front of a player about to gain from it is a lie
about the mechanic. `GateMath.ApplyGate` applied no such floor. So at the seed muster of five men
a weight-1 recruit gate signed itself **+1** and moved the army by 2.6% of itself — 0.13 of a man
— which three separate floors downstream then discarded:

| floor | file |
|---|---|
| `Math.Floor` under a thousand | `Core/Stats/StatFormat.cs:160` |
| `(int)forceCount` | `Core/Crowd/CrowdMath.cs:175` |
| `Math.Floor(log2(...))` | `Core/Run/StandingArmy.cs:81` |

Computed from the shipped constants, a weight-1 recruit first moves an integer army at **39 men**.
Multiply escaped only because 30% of five is already more than one — which is precisely the
asymmetry that was reported. And `RecruitWeight` is a flat 1 (`ChunkLayouts.cs:109`), so the
opening of the game is where weight-1 gates live.

Measured over twelve recruit gates from the seed:

```
before   5, 5, 5, 5, 6, 6, 6, 7, 7, 8, 8, 9      <- the first four did nothing at all
after    6, 8, 11, 12, 14, 17, 18, 20, 23, 24, 26, 29
```

## The rule

**A gate moves the army by at least its weight in men.** `ApplyGate` takes the floor `Headcount`
already promised, and `Headcount` is now *derived from* `ApplyGate` rather than computed beside it
— so the sign and the mechanic are one function and cannot drift apart again.

The floor is the gate's **weight** rather than a flat one man so that a heavier gate is still
visibly heavier at the bottom of the curve. At five men a Ladder chunk reads 6, 7, 8 for its three
gates instead of three identical ticks.

It stops binding at 39 men **for every weight** — the weight cancels, since both the floor and the
share scale linearly in it — so nothing past the opening is touched. Measured on the real
generator, round 0 now runs **5 → 43** with fourteen of twenty-five decisions visibly moving the
number; the other eleven are lane-dodges, where the best lane is correctly empty. The 62-round
campaign walk in `StandingArmyTests` is unchanged.

`Talents.PackBite` takes the same floor, and for the same reason: it was computing its gross
straight off `Factor` and so skipped what a red gate gets, and a pack and a red gate are the same
threat wearing different clothes. Resist mitigates down toward one man but never through it —
**zero is reserved for a shattered pack**, which is the whole fantasy of that talent.


---

# Champions on the road

> *"can we add smaller bosses — semibosses to be found during levels — periodically are met and
> fought against. This should also make the game a little bit more complicated."*

A champion blocks one lane. The army grinds it down while it winds up and swings, and **the road
never stops** — that comes free, because `TrackController` already pins a fighting squad at
`frontZ + EngageGap` while the road scrolls underneath it.

## Why it is a third type and not one of the two that exist

- **`Melee` fixes its outcome at construction.** `Allies` and `Enemies` are readonly and
  `SurvivingAllies` is pure arithmetic, so the whole clash is decided before the first frame of it
  is drawn. That is right for a squad the army rolls over, and *impossible* for a fight whose
  result depends on whether the player raises a shield three quarters of a second from now.
  Editing `Melee` to carry mutable health would put a decision inside a struct whose termination
  and conservation properties are pinned by tests that have nothing to do with champions.
- **`BossSim` is the other end of the scale**: an act-long HP curve, affixes, archetypes and a
  six-hundred-line state machine, for something on screen for four seconds.

So `Core/Run/Elite` sits *beside* `Melee` rather than inside it — the same separation
`BossAffixes` keeps from `BossSim`, for the same reason.

## Two answers, and they pay the same

A swing is answerable by the **shield** or by **not being in its lane** — the lane is re-read every
frame, so leaving mid-fight genuinely works. Both answers cost exactly nothing. A champion with two
answers that cost the same as an ambush gate with none would be a chore rather than a threat, and
an answer that only half works teaches the player not to bother finding it.

| | |
|---|---|
| wind-up to the first swing | 1.05 s |
| between swings | 1.45 s |
| readable window | **0.55 s** |
| a landed swing | 11% of the army per weight |
| killing it | 16% per weight |

The readable window is **wider than a boss's telegraph, not narrower**. A boss fight is the only
thing on screen and the player is looking straight at it; a champion arrives while the road is
still moving, gates are still coming and the player is steering. The warning has to survive that.

Swings are taken from a **closed form over elapsed time** rather than a countdown that resets, so a
dropped frame can neither skip a swing nor fire one twice — there is a test for exactly that, with
one enormous hitch.

## The spell hurts it; it does not delete it

`ClearAmbushesAhead` releases any unresolved pack it finds with no filter, so a champion reusing
`EnemyPackBehaviour` would have been **one-shot by a flick for free**. That had to be a decision
rather than an accident of reuse: deleting it makes the spell strictly better than fighting and
removes the decision the champion exists to pose. A spell takes half its health — a real answer,
turning a fight you might lose into one you will win, without making the champion a formality.

A spell can also land *before* the army arrives, and that must not start the fight: a champion
pinned to the army's front from forty metres away would be swinging at a player who has not reached
it. `EnsureChampion` builds its fight exactly once, so an early spell is not undone by the army
then arriving and resetting its health.

## One extra draw call, deliberately

Uniform scale is the animation bus: `CrowdInstanced` decodes the walk phase out of the 0.44–0.50
window, because the matrix is the only per-instance channel there is. A champion at 1.45× would
decode a nonsense stride. It gets its own `Graphics.RenderMesh` — **one draw** against the four
instanced draws the whole road costs and a ceiling of 120.

It is drawn as the **banner** silhouette, the one shape in the set that breaks the skyline, because
at 26 m a soldier is about ten pixels tall and anything at chest height is invisible. Bigger *and* a
different outline, so a champion is never mistaken for a squad that happens to be close. It leans
back as it winds up and drives forward as it swings — the same grammar the boss uses, on purpose:
the player has already learned what a rearing body means.

## The generator, and the silent failure it nearly had

`ChunkShape.Champion` is the ninth shape. `SequenceFor` picked with a bare `% 8u`, so a ninth shape
would have been authored, wired, tested and then **never generated in a single round**, with every
existing test still green. It is now `% ChunkShapes.Count`, and two tests hold it: one that a
champion appears within two hundred rounds, and one that *every* member of the enum can be
generated — so the next shape added cannot repeat the bug.

A champion chunk holds one champion and nothing inside the reaction gap either side of it. The
recruit gate it does carry sits 16 m earlier and in a different lane, which gives the player
somewhere to be if they choose to dodge — so the dodge is a decision with a payoff rather than a
hole in the round.

---

# Four characters

> *"my mane figure - yellow soldier is very boring, at the very beginning give an option to choose
> several main characters before the game starts, each with his/her individual looks and skills and
> fighting dynamics, think through the options, suggest"*

The report was literally true. `HeroVisual` was handed `ProceduralMeshes.Unit` — the same spear
soldier every body in the crowd is — at 1.35× with a gold tint, chosen in `GameBootstrap` before a
save slot even exists. The leader of the army was one of the army with the brightness turned up,
identical for every player forever.

## What makes four heroes a choice rather than four paint jobs

Each carries three things, and the third is the one that matters:

1. a **silhouette** — broad, thin, crouched, skeletal
2. a **stat block**, which is a nudge and nothing more
3. **one rule** that changes how the run is played

| | look | stats | the rule |
|---|---|---|---|
| **Warden** | broad, tower shield, crested helm, gold on blue | +Health, +ShieldDuration, +1 shield charge | a **blocked** ambush converts 30% of what it would have cost into recruits |
| **Ashcaller** | tall, thin, hooded, staff and orb, ember | +SpellPower, +Cooldown, +1 spell charge | the spell sweeps **1.35×** further |
| **Houndmaster** | crouched under a fur mantle, two hounds, green | +GateYield, +Magnetism, +RunSpeed | **recruit gates only** pay +0.35 extra yield |
| **Revenant** | skeletal, broken crown, torn standard, violet | +Damage, +SecondWind, −Health | 34% of every loss walks back 0.9 s later |

The rules are deliberately spread across the four verbs the game already has — the shield, the
spell, the gates, and losing men — so choosing a hero chooses which part of the existing game you
lean on, rather than adding a fifth system nobody asked for. Stats are modest **on purpose**: a hero
who starts 40% ahead is the hero everyone picks, and a test asserts that no hero is strictly better
than another on every axis.

**Not the rally arch, for the Houndmaster.** A bonus on the multiply would compound with itself
across a run and be worth an order of magnitude by the finish — the same trap that killed the ×2
gate. A flat extra share on the adds is worth a steady few per cent a round and nothing more.

**A block, not a dodge, for the Warden.** Stepping out of a champion's lane costs it nothing and so
earns nothing; the rule is about standing there and taking it. Paying out for a dodge would hand the
bonus to every hero who simply steered well.

**The delay is the point, for the Revenant.** Paid on the same frame, a refund is indistinguishable
from the loss having been smaller. One debt and one timer rather than a queue: two ambushes half a
second apart return as one wave, and a list of pending refunds would allocate inside the run loop to
make a difference nobody can see.

Two properties are pinned by tests rather than by reading: **every rule is inert for the three
heroes who lack it** — which is what makes it safe to call `ConvertBlock` and `Owe` from every block
and every loss site unconditionally — and **no answer is ever worth more than the threat it
answers**, or blocking becomes a way to farm and the best play is to stand in front of the largest
ambush on the road.

## The hip line is load-bearing for the hero too

`heroMaterial` is derived from the crowd material and never clears `_BobAmount`, so
`CrowdInstanced` swings the hero's legs about **y = 0.30** exactly as it swings a soldier's —
anything below that line and inside `|x| < 0.18` rotates about the hip every stride.

The four meshes are built from the silhouette numbers in Core rather than from four hand-authored
piles of boxes, because the numbers are the part that has to be *true*. `HeroAbove` stretches and
leans about the **hip** rather than the origin, which is the only pivot that leaves the feet on the
ground and the legs out of the lean.

**A correction, measured after the fact.** This section first claimed the hero was "about forty
pixels tall", carried over from the crowd figure without doing the arithmetic. It is not. The
camera sits at y = 5.5 ten metres behind the crowd centre and the hero stands on the crowd's
leading plane, up to 7 m ahead of it — so it is 10–17 m from the lens, at 1.35×, under a 60°
vertical FOV. On a 1920-tall render that is:

| | height | on screen | width |
|---|---|---|---|
| Warden | 1.45 m | 172 px | 173 px |
| Ashcaller | 2.04 m | 242 px | 148 px |
| Houndmaster | 1.53 m | 181 px | 136 px |
| Revenant | 1.63 m | 193 px | 168 px |

Between **140 and 240 px** depending on how far forward the army's front rank is, not forty. The
closest pair of silhouettes — Warden and Revenant — differ by 72 px sampled across twelve height
bands, so the four are distinguishable by outline alone with a wide margin.

It changes a conclusion rather than just a number: at forty pixels detail is wasted, and at two
hundred it is not. The four meshes are built as if the first were true, which is why they carry
crests, ribs, hounds and banners and nothing smaller. **There is room for more on these than they
currently have**, and that is the honest note for whoever picks them up next.

Measuring the four builds offline caught two faults that read as correct in source:

- **The Ashcaller's staff went through the road.** `AddOrientedBox` takes a centre, and a 1.45-long
  staff placed by grip height spanned y = −0.11 to 1.34. Carried shafts are now given their **butt**
  and a length (`AddShaft`), and every head on a shaft is placed from the same two numbers
  (`OnShaft`), so haft and head cannot come apart.
- **The Warden's shield would have torn in half at a walk.** A 0.30-wide shield centred at x = −0.32
  reaches in to x = −0.17 with its bottom edge at y = 0.16 — inside the leg band, below the hip. The
  two inner bottom corners would have swung with the left leg while the rest of the slab held still.
  It is held at −0.37.

Measured after the fix, all four have exactly **eight** vertices in the swept region, and they are
the leg soles — which is what the shader is there to move. Nothing on any of the four dips below
y = 0. Heights run 1.07 / 1.51 / 1.13 / 1.21, the Ashcaller's staff dominating by a third.

**The army is repainted too**, and that is most of what makes a run feel like a different character:
the leader is one figure among a few hundred, and at 0.47 scale the crowd is what the eye actually
reads. The hero's kind takes over the **majority share** of the archetype mix rather than replacing
all four — an army of one shape is the photocopy the four soldier meshes exist to prevent.

## Where the choice lives

`PlayerProfile` gains `HeroId` **and** `HeroChosen`, and the second field exists because "chose the
Warden" and "was never asked" both store id 0. Without it, every existing player would be sent back
through the character screen on the next launch. The **v7 migration** marks an existing save as
having already chosen — the same reasoning as the v2→v3 tutorial migration, which marks every step
taught rather than coaching someone who has clearly finished learning. A brand-new profile is
created at `SchemaVersion = CurrentVersion`, so no step runs on it and it is offered the choice.

`HeroSelectState` sits between the slot screen and the main menu and is entered only when the
profile still owes a choice. `HeroOutfit.Apply` is the one place the arena is dressed, called from
both `GameContext.ActivateSlot` (which is how a returning player who skips the screen gets their
hero) and from the select screen itself — it reads the choice straight off the profile rather than
taking it as an argument, so there is no way to call it with a hero the save does not hold. It
repaints **after** which the shield ward is told to re-read its resting colours: the ward caches the
colour it has to put back, and repainting under it is exactly how an army would end a run stuck on
the previous hero's tint.

Hero stats compose through `ProfileStatsResolver` with talents, paragon and gear, by one set of
rules — so they also show up in the stat summary the player already reads, and `HeroRoster.StatLine`
prints them through the same formatter gear affixes use.
