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

## Migration

Schema **v4**. Points already spent on the old three stats are **refunded as unspent
points**: the old stats have no faithful mapping onto talents, and a free respec is the
honest trade rather than guessing an equivalent build.
