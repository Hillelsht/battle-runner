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
