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
