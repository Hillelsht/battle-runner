# 09 — Monetization setup: what only you can do

## The short answer

**Yes — real ads and real in-app purchases need accounts in your name, and nobody else
can create them.** They are tied to identity, tax details and a bank account, because
they are how money reaches you. Everything else — SDK integration, the service classes,
the consent flow, the build pipeline — is code, and code is my side of the line.

There is a second, larger truth buried in the question: **the Google Play Console account
is not really a monetization step, it is the shipping step.** Without it the game cannot
be on Google Play at all, paid or free. And it carries a 14-day clock that cannot be
compressed, bought, or parallelised. That clock is the single most important thing on
this page.

> **Do this first, today, before anything else:** create the Play Console account and
> start the closed test. Everything else can happen while it runs.

---

## Why we need each piece

| Piece | Why the game needs it |
|---|---|
| **Rewarded ads** | Two buttons in the game are already wired to them and currently do nothing real: **double your loot** after a run, and **resurrect** when the crowd is wiped. In hybrid-casual these are the primary revenue line, and the resurrect prompt is also a retention mechanic — a player who dies at level 3 and stops is a player lost. |
| **In-app purchases** | The keys the loot chests consume, and the planned $3.99 battle pass. Smaller revenue than ads at this stage, much higher per paying player. |
| **Play Console** | The only way onto Google Play. Also where products, pricing, store listing, ratings and the privacy declarations live. |
| **Privacy policy + consent** | Legally required the moment an ad SDK touches the device advertising ID. Google rejects builds without it. |

---

## The pipeline, in the order it has to happen

Steps are marked **[you]**, **[me]**, or **[wait]**. Hands-on time is *your* time at a
keyboard; waiting time is calendar time where you do nothing.

### Phase 0 — Start the clock (day 1)

| # | Step | Who | Your time | Waiting | Why |
|---|---|---|---|---|---|
| 0.1 | Create a Google Play Console developer account, pay the one-time **$25** fee | [you] | 30–60 min | — | The gate to everything. Have a government ID ready. |
| 0.2 | Identity verification | [wait] | — | 1–3 days | Google verifies the ID before the account is usable. |
| 0.3 | Create a Google payments (merchant) profile | [you] | 20–30 min | up to a few days | Required to *sell* anything. Needs address and tax details. |
| 0.4 | Publish a privacy policy at a public URL | [you] 5 min | 5 min | — | Mandatory. **[me]** I will write the page and host it on GitHub Pages from this repo — you only paste the URL into the Console. |

> **The 14-day gate.** Personal Play Console accounts created after 13 Nov 2023 must run a
> **closed test with at least 12 opted-in testers who actually use the app for 14
> continuous days**, and only then may apply for production access. Google now checks the
> testers genuinely engaged, not just that they opted in. Organisation accounts (registered
> with a legal business entity) are exempt. **This is why step 0.1 is day one:** the 14 days
> run in the background while we finish the game. Line up 12 people — friends, family, a
> Discord — each needs a Google account email.

### Phase 1 — Ads (can run in parallel with the clock)

I recommend **Unity LevelPlay**, not AdMob directly. LevelPlay is Unity's own mediation
layer: one SDK that auctions each ad slot across AdMob, AppLovin, Meta and others, so you
get the highest bid rather than one network's price. You already have a Unity account, so
there is no new identity to verify. AdMob then becomes *one of the networks LevelPlay
calls*, added later without touching the game.

| # | Step | Who | Your time | Waiting | Why |
|---|---|---|---|---|---|
| 1.1 | Unity Dashboard → enable LevelPlay monetization, accept the terms | [you] | 10 min | — | Turns the product on for your Unity org. |
| 1.2 | Add the Android app, create **two rewarded ad units** (`LootDouble`, `Resurrect`) | [you] | 15 min | — | The two placements already named in `IAdService`. |
| 1.3 | Paste the **App Key** and the two **Ad Unit IDs** into `Assets/Resources/MonetizationConfig.asset` | [you] | 5 min | — | **[me]** I build that asset with labelled empty fields; you fill three boxes. |
| 1.4 | Add `com.unity.services.levelplay`, write `LevelPlayAdService : IAdService` | [me] | — | — | Binds behind the existing seam. No gameplay code changes. |
| 1.5 | Consent flow (GDPR/UK, US state laws) | [me] | — | — | Non-optional if you sell outside a handful of countries. |
| 1.6 | Verify on your phone **in test mode** | [you] | 20 min | — | Confirms the reward actually fires. |
| 1.7 | Payout details in the LevelPlay dashboard | [you] | 20 min | — | Where the money lands. Thresholds are typically ~$100. |
| 1.8 | *Later, optional:* add AdMob / AppLovin / Meta as mediated networks | [you] | 30–45 min each | 1–2 days review each | Each is its own account. Raises fill rate and eCPM. Do it after the game is live and there is traffic worth bidding on. |

> **Never tap your own live ads.** Use test mode until release. Self-clicking is the
> fastest way to get an ad account permanently banned.

### Phase 2 — In-app purchases

| # | Step | Who | Your time | Waiting | Why |
|---|---|---|---|---|---|
| 2.1 | Create the app in Play Console; store listing, screenshots, content rating, **Data safety**, ads declaration, target audience | [you] | 2–4 h | — | The biggest block of your time on this page, and it is all form-filling. Data safety must declare the advertising ID the ad SDK collects. |
| 2.2 | Create an **upload keystore** and store 4 values as GitHub secrets | [you] | 20 min | — | Play only accepts signed builds, and the key must never change for the app's life. One `keytool` command; I give you the exact line. |
| 2.3 | Switch CI from APK to a **signed AAB** | [me] | — | — | Google Play requires Android App Bundles for new apps; our current APK is for sideloading only. |
| 2.4 | Upload the first build to an internal-testing track | [you] | 15 min | ~1 h review | **The Console will not let you create in-app products until a build containing the billing library is uploaded.** This ordering trips everyone. |
| 2.5 | Create the three products (`KeysSmall`, `KeysLarge`, `StarterChest`) with prices | [you] | 20 min | — | IDs must match `IapProduct` exactly. |
| 2.6 | Copy the Play **license key** (RSA) into the config asset | [you] | 5 min | — | Lets the game validate receipts on-device. |
| 2.7 | Add `com.unity.purchasing`, write `UnityIapService : IIapService` | [me] | — | — | Behind the existing seam. |
| 2.8 | Add yourself + testers as **licence testers**, run a real (free) test purchase | [you] | 30 min | — | The only way to know the whole chain works. |

### Phase 3 — The gate to production

| # | Step | Who | Your time | Waiting | Why |
|---|---|---|---|---|---|
| 3.1 | Set up the closed test, invite 12+ testers | [you] | 1 h | — | Started in Phase 0 if you took the advice above. |
| 3.2 | Testers keep the app installed and use it | [wait] | — | **14 days** | Cannot be shortened. Engagement is checked. |
| 3.3 | Apply for production access | [you] | 30 min | up to ~7 days | Google reviews the application. |
| 3.4 | First production release | [you] | 15 min | 1–7 days review | Live. |

---

## What it adds up to

| | |
|---|---|
| **Your hands-on time** | **6–9 hours**, almost all of it forms |
| **Calendar time** | **3–5 weeks**, dominated by the 14-day test and verification waits |
| **Money** | **$25** one-time (Play Console). LevelPlay and Unity IAP cost nothing up front. |

The gap between 8 hours of work and 5 weeks of calendar is the whole point of this page:
**the waiting is the schedule, and it starts when you click, not when the game is
finished.**

---

## What is already built, waiting for the IDs

The seams exist and are exercised by mocks today, so nothing in the game has to change
when the real SDKs land:

- `Assets/Scripts/Meta/Services/IAdService.cs` — `IsRewardedReady(placement)`,
  `ShowRewarded(placement, onDone)` where the callback receives **true only when the
  reward was genuinely earned**. Two placements: `LootDouble`, `Resurrect`.
- `Assets/Scripts/Meta/Services/IIapService.cs` — `Purchase(product, onDone)` over three
  products.
- Call sites: `LootPhaseState` (double), `RunnerLoopState` and `BossEncounterState`
  (resurrect). `GameFlowController` pumps `Ads.Tick(dt)` every frame.
- `GameBootstrap` picks the implementation in one place. Swapping mock for real is one
  line, plus the SDK behind it.

## Risks worth knowing before you start

- **Version treadmill.** Google raises the minimum Play Billing Library and the minimum
  `targetSdkVersion` every year, on a deadline. An app that ships and is then left alone
  gets pulled from the store. CI catches this early; it is a maintenance cost, not a
  surprise.
- **Ad accounts get banned for self-clicks.** Test mode until release, without exception.
- **Data safety declarations must match reality.** Declaring "no data collected" while an
  ad SDK reads the advertising ID is a policy violation, and enforcement is automated.
- **Ads before there is a game to reward is backwards.** Rewarded ads pay per *engaged*
  view. A player who quits in the first minute never sees one, which is exactly why the
  FTUE and the talent tree came first.

## Sources

- [App testing requirements for new personal developer accounts — Play Console Help](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en)
- [Everything about the 12 testers requirement — Google Play Developer Community](https://support.google.com/googleplay/android-developer/community-guide/255621488/everything-about-the-12-testers-requirement?hl=en)
- [LevelPlay rewarded ads integration for Unity](https://docs.unity.com/en-us/grow/levelplay/sdk/unity/rewarded-ad-integration-package)
- [Unity.Services.LevelPlay API reference](https://docs.unity3d.com/Packages/com.unity.services.levelplay@8.7/api/Unity.Services.LevelPlay.html)
- [Google Play Billing Library release notes](https://developer.android.com/google/play/billing/release-notes)
- [Unity IAP — Google Play store](https://docs.unity.com/en-us/iap/google-store)
