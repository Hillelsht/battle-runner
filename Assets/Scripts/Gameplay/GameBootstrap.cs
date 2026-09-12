using BattleRunner.Core.Flow;
using BattleRunner.Data.Channels;
using BattleRunner.Core.Audio;
using BattleRunner.Core.Save;
using BattleRunner.Data.Definitions;
using BattleRunner.Gameplay.Combat;
using BattleRunner.Gameplay.Crowd;
using BattleRunner.Gameplay.Input;
using BattleRunner.Gameplay.States;
using BattleRunner.Gameplay.Track;
using BattleRunner.Meta.Services;
using BattleRunner.Meta.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleRunner.Gameplay
{
    /// <summary>
    /// The only component in Main.unity. Everything else — camera, light, UI, track,
    /// crowd, services, flow — is constructed here at runtime (plan decision 1), so
    /// the project has exactly one hand-written serialized scene object to break.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.targetFrameRate = 60;
            Time.fixedDeltaTime = 1f / 30f;

            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
                Debug.LogWarning("[Bootstrap] No render pipeline asset assigned. In the editor, the URP " +
                                 "auto-setup runs on first load — if this persists, run BattleRunner > Setup Project.");

            SetupEnvironmentLook();

            var ctx = new GameContext
            {
                Config = LoadConfig(),
                SaveService = new FileSaveService(),
                Ads = new MockAdService(),
                Iap = new MockIapService(),
                BattlePass = new DisabledBattlePassService(),
                Audio = new AudioDirector()
            };
            // No slot is chosen yet, so start on an empty profile. Loading here would pick a
            // save the player has not asked for; SlotSelectState decides which one becomes
            // real and ActivateSlot swaps this out.
            ctx.Profile = new PlayerProfile { SchemaVersion = SaveMigrator.CurrentVersion };
            ctx.TierCap = DetectTierCap(ctx.Config.Balance);

            CreateChannels(ctx);
            CreateArena(ctx);
            CreateUi(ctx);
            CreateInput(ctx);
            // Needs the loaded profile (for what has already been taught) and the systems it
            // listens to, so it is built after the arena, UI and input exist.
            ctx.Tutorial = new TutorialCoach(ctx, ctx.TutorialOverlay, ctx.Profile.TutorialMask);
            CreateFlow(ctx);

            Debug.Log($"[Bootstrap] Ready. Tier cap {ctx.TierCap}; awaiting slot choice.");
        }

        private static GameConfig LoadConfig()
        {
            var config = Resources.Load<GameConfig>("GameConfig");
            if (config != null && config.Balance != null && config.Levels != null && config.Levels.Length > 0)
                return config;

            Debug.Log("[Bootstrap] Resources/GameConfig missing — using built-in default content.");
            return ContentFactory.BuildConfig();
        }

        private static void SetupEnvironmentLook() => EnvironmentLook.Apply();

        private static int DetectTierCap(BalanceSettings balance)
        {
            int memoryMb = SystemInfo.systemMemorySize;
            if (memoryMb > 0 && memoryMb < 3000) return balance.TierCapLow;
            if (memoryMb < 5000) return balance.TierCapMid;
            return balance.TierCapHigh;
        }

        private static void CreateChannels(GameContext ctx)
        {
            ctx.LaneTargetChannel = ScriptableObject.CreateInstance<FloatEventChannel>();
            ctx.FlickUpChannel = ScriptableObject.CreateInstance<VoidEventChannel>();
            ctx.FlickDownChannel = ScriptableObject.CreateInstance<VoidEventChannel>();
            ctx.ForceChangedChannel = ScriptableObject.CreateInstance<DoubleEventChannel>();
        }

        private static Material LoadCrowdMaterial()
        {
            // Trusting Resources.Load unconditionally is how a magenta material reached
            // the screen in v0.1.0 — validate against the ACTIVE pipeline before using it.
            Shader resolved = ShaderSafety.Resolve();
            var material = Resources.Load<Material>("Crowd");
            if (material != null && !ShaderSafety.UsingFallback && material.shader != null && material.shader.isSupported)
            {
                material.enableInstancing = true;
                return material;
            }

            Debug.LogWarning("[Bootstrap] Resources/Crowd.mat unusable here; rebuilding from the resolved shader.");
            var fallback = new Material(resolved);
            fallback.SetColorSafe("_BaseColor", new Color(0.25f, 0.28f, 0.38f));
            fallback.SetColorSafe("_EmissionColor", new Color(0.35f, 0.5f, 0.9f));
            fallback.SetFloatSafe("_BobAmount", 0.12f);
            fallback.enableInstancing = true;
            return fallback;
        }

        /// <summary>
        /// The additive VFX material, or null if it cannot render here — in which case
        /// VfxSystem turns itself off entirely.
        ///
        /// Deliberately NOT symmetric with LoadCrowdMaterial, which rebuilds a fallback
        /// from the resolved shader when Resources/Crowd.mat is unusable. There is no
        /// sensible stand-in for an additive shader: substituting an opaque one would
        /// flash untinted rectangles across the road, which is worse than the silence.
        /// The crowd has to be drawn one way or another; a shockwave does not.
        /// </summary>
        private static Material LoadVfxMaterial()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) return null;
            var material = Resources.Load<Material>("Vfx");
            if (material != null && material.shader != null && material.shader.isSupported) return material;
            Debug.LogWarning("[Bootstrap] Resources/Vfx.mat unusable here; effects will be skipped.");
            return null;
        }

        private void CreateArena(GameContext ctx)
        {
            ctx.ArenaRoot = new GameObject("Arena");

            ctx.CrowdMaterial = LoadCrowdMaterial();

            var enemyMaterial = ShaderSafety.CreateMaterial(ctx.CrowdMaterial);
            enemyMaterial.SetColorSafe("_BaseColor", new Color(0.35f, 0.08f, 0.08f));
            enemyMaterial.SetColorSafe("_EmissionColor", new Color(0.6f, 0.08f, 0.05f));
            // No legs, because a squad STANDING in the road waiting for the army should not
            // be walking on the spot. SquadRenderer derives a second material from this one
            // with the bob back on, and routes a squad into it for the duration of a clash —
            // before that split, the player's soldiers animated while the red side was a
            // rigid statue sliding along the road through the entire fight.
            enemyMaterial.SetFloatSafe("_BobAmount", 0f);

            var heroMaterial = ShaderSafety.CreateMaterial(ctx.CrowdMaterial);
            heroMaterial.SetColorSafe("_BaseColor", new Color(0.6f, 0.45f, 0.15f));
            heroMaterial.SetColorSafe("_EmissionColor", new Color(1.1f, 0.75f, 0.2f));

            // The crowd gets a cool rim so blue-lit bodies pop against the dark ground.
            var crowdMaterial = ShaderSafety.CreateMaterial(ctx.CrowdMaterial);
            crowdMaterial.SetColorSafe("_EmissionColor", new Color(0.45f, 0.75f, 1.4f));

            // The crowd is the one thing made of hard-normal boxes seen in bulk, so it is
            // the one thing the wide default rim ruins: at power 2.5 two of the three
            // visible faces glow over half strength and the flat term floods the third,
            // leaving units as self-lit blocks. A tighter lobe plus an up-face mask puts
            // the light back on the silhouette. The road decals now set the same mask for
            // the opposite reason: their top face is the only face they have, and the rim is
            // strongest at grazing angles, which is all a ground decal is ever seen at.
            crowdMaterial.SetFloatSafe("_RimPower", 4.5f);
            crowdMaterial.SetFloatSafe("_RimStrength", 0.6f);
            crowdMaterial.SetFloatSafe("_RimUpMask", 1f);
            crowdMaterial.SetFloatSafe("_EmissionFlat", 0.03f);
            // The CROWD only. Everything else on this shader — gates, rails, road markings,
            // the finish line, the boss, the hero — sits far outside the 0.44-0.50 instance
            // scale the phase is decoded from, so their phase pins to 1.0 and they would all
            // silently brighten by 30% if this were on the shared material.
            crowdMaterial.SetFloatSafe("_ToneSpread", 1f);

            var crowdGo = new GameObject("Crowd");
            crowdGo.transform.SetParent(ctx.ArenaRoot.transform, false);
            ctx.Crowd = crowdGo.AddComponent<CrowdController>();
            ctx.Crowd.Initialize(ctx.ForceChangedChannel, ctx.TierCap, ctx.Config.Balance.LaneWidthMeters);
            var crowdRenderer = crowdGo.AddComponent<CrowdRenderer>();
            crowdRenderer.Initialize(ctx.Crowd, ProceduralMeshes.Unit, crowdMaterial);
            // The army's headcount, in world space over the crowd — so the player compares it
            // against a squad's number in one glance instead of against the HUD readout.
            crowdGo.AddComponent<Crowd.ArmyCountLabel>().Initialize(ctx.Crowd, UiFactory.Font);

            var heroGo = new GameObject("Hero");
            heroGo.transform.SetParent(ctx.ArenaRoot.transform, false);
            ctx.Hero = heroGo.AddComponent<HeroVisual>();
            ctx.Hero.Initialize(ctx.Crowd, ProceduralMeshes.Unit, heroMaterial, ctx.TierCap);

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(ctx.ArenaRoot.transform, false);
            ctx.TrackController = trackGo.AddComponent<TrackController>();
            ctx.TrackController.Initialize(ctx.CrowdMaterial, enemyMaterial, ProceduralMeshes.Unit,
                UiFactory.Font, ctx.Config.Balance.LaneWidthMeters);
            // Enemy squads and the detachment that fights them are drawn instanced, which
            // needs the crowd (for where the army's front rank is) and the ally material.
            ctx.TrackController.AttachSquadRenderer(ctx.Crowd, crowdMaterial);

            // On the same object as the track, and after the crowd: the prop field needs the
            // crowd to know which slice of the verge is worth drawing.
            ctx.Props = trackGo.AddComponent<Track.SceneryField>();
            ctx.Props.Initialize(ctx.Crowd, ctx.CrowdMaterial);

            // Loaded HERE rather than beside the Vfx system further down: the boss's own
            // ward shell needs the same additive material, and BossView is built first.
            Material vfxMaterial = LoadVfxMaterial();

            var bossGo = new GameObject("Boss");
            bossGo.transform.SetParent(ctx.ArenaRoot.transform, false);
            ctx.BossView = bossGo.AddComponent<BossView>();
            ctx.BossView.Initialize(ctx.CrowdMaterial, vfxMaterial);

            var cameraGo = new GameObject("GameCamera");
            ctx.CameraRig = cameraGo.AddComponent<CameraRig>();
            ctx.CameraRig.Initialize(ctx.Crowd);
            EnvironmentLook.AttachPostProcessing(ctx.CameraRig.Camera);

            // The listener goes on the CAMERA, which is where the player's ears are, and
            // the voice pool goes under the arena root so it is one object in the hierarchy
            // rather than sixteen loose ones. Neither existed before: this project had no
            // AudioListener anywhere, so nothing it played would have been audible.
            // NOT under ArenaRoot. CreateArena ends with ArenaRoot.SetActive(false) and an
            // AudioSource on an inactive GameObject does not play — the menus would have been
            // silent and the first round's music would only have started on the second round.
            var audioRoot = new GameObject("Audio").transform;
            Object.DontDestroyOnLoad(audioRoot.gameObject);
            (ctx.Audio as AudioDirector)?.Initialize(cameraGo.transform, audioRoot);

            var overlayGo = new GameObject("DebugOverlay");
            overlayGo.AddComponent<DebugOverlay>().Initialize(ctx.Crowd);

            ctx.Spell = new SpellSystem(ctx.Config.Spells);
            ctx.Shield = new ShieldSystem(ctx.Config.Spells);
            // The EVENT, not the input handler. TryRaise silently refuses while on cooldown,
            // and a sound on a shield that did not actually go up teaches the player that the
            // flick worked when it did not.
            ctx.Shield.Raised += () => ctx.Audio.Play(AudioCue.ShieldRaise);
            Meta.UI.UiFactory.Tap += () => ctx.Audio.Play(AudioCue.UiTap);

            // AFTER ctx.Shield exists — the ward holds a reference to it, and the systems
            // are built at the bottom of this method while the camera is built above.
            //
            // The block window had no visual at all: the player flicked down and nothing
            // on screen changed, so there was no way to learn the flick had registered.
            // The ward recolours the army's own emission, so it costs no new mesh,
            // material, shader or draw call — and adds no shader-stripping risk.
            ctx.Ward = cameraGo.AddComponent<Vfx.ShieldWard>();
            ctx.Ward.Initialize(ctx.Shield, crowdMaterial, heroMaterial);

            var vfxGo = new GameObject("Vfx");
            vfxGo.transform.SetParent(ctx.ArenaRoot.transform, false);
            ctx.Effects = vfxGo.AddComponent<Vfx.VfxSystem>();
            ctx.Effects.Initialize(vfxMaterial);

            // The dome lives on the same object and shares the one additive material.
            ctx.Dome = vfxGo.AddComponent<Vfx.ShieldDome>();
            ctx.Dome.Initialize(ctx.Shield, ctx.Crowd, vfxMaterial);

            ctx.ArenaRoot.SetActive(false);
        }

        private static void CreateUi(GameContext ctx)
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            Canvas canvas = UiFactory.CreateCanvas("UiCanvas");
            Transform root = canvas.transform;

            ctx.MenuScreen = new MainMenuScreen(root,
                () => ctx.MenuState.OnPlayPressed(),
                () => ctx.MenuState.OnNewRunPressed());
            ctx.MenuScreen.BindSound(
                () => ctx.Audio.Enabled,
                () => ctx.Audio.Enabled = !ctx.Audio.Enabled);
            ctx.SlotScreen = new SlotSelectScreen(root);
            ctx.Hud = new HudScreen(root);
            ctx.LootScreen = new LootScreen(root);
            ctx.SkillScreen = new SkillTreeScreen(root);
            // Above the HUD, below the resurrect modal: the canvas never sets sortingOrder,
            // so draw order is sibling order.
            ctx.TutorialOverlay = new TutorialOverlay(root);
            ctx.Resurrect = new ResurrectPrompt(root);
        }

        private void CreateInput(GameContext ctx)
        {
            var inputGo = new GameObject("InputRouter");
            var router = inputGo.AddComponent<InputRouter>();
            router.Initialize(ctx.Config.Input != null ? ctx.Config.Input.Gestures
                    : BattleRunner.Core.Gestures.GestureSettings.Default,
                ctx.LaneTargetChannel, ctx.FlickUpChannel, ctx.FlickDownChannel);
        }

        private void CreateFlow(GameContext ctx)
        {
            ctx.Machine = new GameStateMachine();
            ctx.BootState = new BootState(ctx);
            ctx.SlotState = new SlotSelectState(ctx);
            ctx.MenuState = new MainMenuState(ctx);
            ctx.RunLoadingState = new RunLoadingState(ctx);
            ctx.RunnerState = new RunnerLoopState(ctx);
            ctx.BossState = new BossEncounterState(ctx);
            ctx.ThreatState = new BossThreatState(ctx);
            ctx.LootState = new LootPhaseState(ctx);
            ctx.UpgradeState = new StatUpgradeState(ctx);

            var flowGo = new GameObject("GameFlow");
            flowGo.AddComponent<GameFlowController>().Initialize(ctx);
            ctx.Machine.TransitionTo(ctx.BootState);
        }
    }
}
