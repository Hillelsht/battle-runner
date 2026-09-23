# Three languages

> English, Russian and Hebrew. The hard half is Hebrew, and it is not the vocabulary.

## Why this needed building rather than switching on

The game had no localization facility of any kind: no `com.unity.localization`, no string table, no
locale concept. Every user-facing word was a hardcoded English literal in C#. The one piece of luck
is that `Assets/Scenes/Main.unity` contains no text at all — the whole UI is built procedurally from
`UiFactory` — so every string lived in a `.cs` file rather than scattered across prefabs.

Counted before anything was designed: **~370–380 distinct translatable strings**, unevenly spread.
Around 172 of them are talent names and descriptions in one file, and seven of them have **no string
literal to find** — `LootScreen` interpolated `item.Rarity` and `item.Slot` straight from
`enum.ToString()`, so an extraction pass misses them silently.

## The font came first

Until this work the game shipped no font. `UiFactory` resolved Unity's built-in `LegacyRuntime.ttf`,
which carries Latin and nothing that can be relied on past it. Unity configures no fallback chain, so
a missing codepoint renders as an empty box on device and a build machine cannot tell. The project
had already paid that twice and left the reason in comments: the HUD's pips were deleted and the
tutorial's arrows became ASCII `^` and `v`.

**Arimo**, 316 KB, SIL OFL 1.1 — Latin, Cyrillic, Greek and Hebrew in one file, so there is still
exactly one `Font` and one atlas rather than a fallback chain. Chosen because it is metric-compatible
with Arial, which is what `LegacyRuntime.ttf` already is: the English screens, every one laid out by
eye against fixed pixel widths, move as little as it is possible to move them while changing
typeface at all.

`tooling/fetch_font.py` does not merely download it. It parses the font's own `cmap` table and fails
unless every codepoint the game can render is present. A font that arrived intact and silently
lacked Hebrew is exactly the failure the built-in font would have handed us with no warning.

## Shrink rather than spill

`UiFactory.Label` set `horizontalOverflow = Overflow`, so long text neither wraps nor shrinks — it
runs straight out past the bevelled frame of whatever button it is on. Fine while every string was
English and hand-fitted; **Russian runs 10–30% longer** and would have walked out of every button in
the game. 44 of 61 placements use `UiFactory.Place`, which mixes a normalized centre with a fixed
pixel width.

Widening the buttons is the obvious fix and the wrong one. `SlotSelectScreen` records that exact bug
biting once already, in English: a 640 px button at x=0.44 and a 210 px button at x=0.82 overlapped
by 53 units on a tall phone, and ERASE silently took PLAY's taps. Growing anything grows it into its
neighbour.

So `Label` uses best-fit with **the ceiling set to the size the caller asked for**. English is
unchanged — it already fits, so best-fit never has anything to do. Only a longer translation shrinks,
and only as far as it must.

That ceiling is why `ActionButton` gained a `labelSize` parameter. It built its label at 40 pt and
ten call sites then assigned `fontSize` afterwards; with the ceiling left at 40, best-fit would have
found room and grown them straight back, changing the English UI on screens nobody touched.

## The table

`Core/Text/` — in Core, for the same reason the gate maths is: `noEngineReferences` means the whole
table is checked by `dotnet test` rather than by opening the game and looking.

- **Keyed by an enum.** At this count a mistyped string key is a blank label nobody notices until a
  player finds it. A mistyped enum member does not compile.
- **Authored as `(key, text)` pairs, indexed as an array.** The three tables can be written in
  whatever order reads best, and a key inserted in the middle cannot shift another language's
  entries out of alignment. They fold into a flat array once, on first use.
- **Data carries keys; the property names stay.** `HeroProfile.Name`, `WorldTheme.DisplayName`,
  `Paragon.Track.DisplayName` are now computed from a `LocKey`. Every existing caller keeps reading
  `.Name` and gets it in the language on screen, with no call site touched — and nothing has to be
  rebuilt when the language changes.
- **Everything is stored in logical order, Hebrew included.** Reordering Hebrew for display is a
  display concern; doing it in the table would mean `Format` composing already-reversed fragments.

### What the tests are standing in for

Translations are the one kind of content where a mistake is invisible to whoever made it. A missing
Hebrew entry, a dropped `{0}`, a mistyped percentage — all of them compile, render, and look fine to
someone who does not read that language.

| test | what it catches |
|---|---|
| every language carries every key exactly once | a blank label on a screen in a language you do not read |
| nothing is blank, nothing has stray whitespace | a label that looks mis-centred |
| every placeholder survives translation | a dropped `{0}` reads as a game bug; an invented `{1}` throws |
| **the numbers in a translation are the numbers in the source** | a talent that states a rule the game does not follow |
| every counted string has a form for every count | Russian agreement failing at 11 and 21 specifically |
| Hebrew carries no combining marks | nikud reordering ahead of their base letter |

### Counting is not `n == 1 ? "" : "s"`

Four strings in this game were counted with that ternary. It is a rule about English spelled as if it
were a rule about counting. **Russian has three forms and picks between them on the last two digits**,
so 21 takes the same form as 1, 22 the same as 2, and 11–14 take the many-form despite ending in 1–4.
`Core/Text/Plural.cs` does this properly; the variants live in one entry separated by `|`.

### The talent tree is half the table

79 nodes, a name and a description each, plus branch names and refusals — 170 of the 238 keys.
Every description was extracted from the source rather than transcribed, and **the digits in all
158 translations were checked against the English before a single one was committed**. That check
then became a permanent test, because these descriptions quote balance values that also exist as a
`StatModifier` a few characters away: a translation reading *"Отряды стоят на 5% меньше"* beside a
`-0.04` states a rule the game does not follow, and it is wrong only to the people who can read it.

The duplication itself is not fixed here. The descriptions still carry their own numbers, so a
future balance change needs all three languages edited together — the test catches a typo, not a
desync. Feeding these from the `StatModifier` beside them is the real fix and is a clean follow-up.

### Word order is not a constant either

`BossAffix.Decorate` was `prefix + " " + bossName`. **Hebrew puts the adjective after the noun**, so
the pairing is a template (`{0} {1}` in English and Russian, `{1} {0}` in Hebrew) rather than a
concatenation.

Russian brought its own problem: adjectives agree in gender with the noun. All six boss names are
rendered as masculine nouns on purpose — the Hollow Leech is *Кровосос* rather than the feminine
*Пиявка* — so one adjective form serves all six. The alternative was a gender field on every boss.

### The register

Informal, and phrased so it never assumes the player's gender. Russian uses `ты` and imperatives,
which are genderless. **Hebrew imperatives are not** — `החלק` to a man, `החליקי` to a woman — so
instructions are phrased as infinitives: `להחליק את האגודל` rather than either.

## What is deliberately not translated

**The magnitude suffixes stay Latin** — `K`, `M`, `B`, `T`. They are near-universal in games, and they
keep the three world-space `TextMesh` labels script-free. That last part matters more than it looks:
those labels are billboarded by facing *away* from the camera and render mirrored through `Cull Off`,
so putting reorderable text on them would interact badly with the bidi pass. `CultureInfo.InvariantCulture`
stays for the same reason.

**The road does not mirror.** In Hebrew the menus mirror — tabs run right to left, buttons swap sides,
text right-aligns — but the 3-lane road, the gate numbers, the boss health bar and the tutorial
patience bar stay exactly as they are. Those are a physical space and two quantities, not reading
order, and a bar that drains the other way reads as filling.
