using BattleRunner.Core.Flow;
using BattleRunner.Data.Channels;
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
                BattlePass = new DisabledBattlePassService()
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
            ctx.ForceChangedChannel = ScriptableObject.CreateInstance<LongEventChannel>();
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

        private void CreateArena(GameContext ctx)
        {
            ctx.ArenaRoot = new GameObject("Arena");

            ctx.CrowdMaterial = LoadCrowdMaterial();

            var enemyMaterial = ShaderSafety.CreateMaterial(ctx.CrowdMaterial);
            enemyMaterial.SetColorSafe("_BaseColor", new Color(0.35f, 0.08f, 0.08f));
            enemyMaterial.SetColorSafe("_EmissionColor", new Color(0.6f, 0.08f, 0.05f));
            enemyMaterial.SetFloatSafe("_BobAmount", 0f);

            var heroMaterial = ShaderSafety.CreateMaterial(ctx.CrowdMaterial);
            heroMaterial.SetColorSafe("_BaseColor", new Color(0.6f, 0.45f, 0.15f));
            heroMaterial.SetColorSafe("_EmissionColor", new Color(1.1f, 0.75f, 0.2f));

            // The crowd gets a cool rim so blue-lit bodies pop against the dark ground.
            var crowdMaterial = ShaderSafety.CreateMaterial(ctx.CrowdMaterial);
            crowdMaterial.SetColorSafe("_EmissionColor", new Color(0.45f, 0.75f, 1.4f));

            var crowdGo = new GameObject("Crowd");
            crowdGo.transform.SetParent(ctx.ArenaRoot.transform, false);
            ctx.Crowd = crowdGo.AddComponent<CrowdController>();
            ctx.Crowd.Initialize(ctx.ForceChangedChannel, ctx.TierCap, ctx.Config.Balance.LaneWidthMeters);
            var crowdRenderer = crowdGo.AddComponent<CrowdRenderer>();
            crowdRenderer.Initialize(ctx.Crowd, ProceduralMeshes.Unit, crowdMaterial);

            var heroGo = new GameObject("Hero");
            heroGo.transform.SetParent(ctx.ArenaRoot.transform, false);
            ctx.Hero = heroGo.AddComponent<HeroVisual>();
            ctx.Hero.Initialize(ctx.Crowd, ProceduralMeshes.Unit, heroMaterial, ctx.TierCap);

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(ctx.ArenaRoot.transform, false);
            ctx.TrackController = trackGo.AddComponent<TrackController>();
            ctx.TrackController.Initialize(ctx.CrowdMaterial, enemyMaterial, ProceduralMeshes.Unit,
                UiFactory.Font, ctx.Config.Balance.LaneWidthMeters);

            var bossGo = new GameObject("Boss");
            bossGo.transform.SetParent(ctx.ArenaRoot.transform, false);
            ctx.BossView = bossGo.AddComponent<BossView>();
            ctx.BossView.Initialize(ProceduralMeshes.Unit, ctx.CrowdMaterial);

            var cameraGo = new GameObject("GameCamera");
            ctx.CameraRig = cameraGo.AddComponent<CameraRig>();
            ctx.CameraRig.Initialize(ctx.Crowd);
            EnvironmentLook.AttachPostProcessing(ctx.CameraRig.Camera);

            var overlayGo = new GameObject("DebugOverlay");
            overlayGo.AddComponent<DebugOverlay>().Initialize(ctx.Crowd);

            ctx.Spell = new SpellSystem(ctx.Config.Spells);
            ctx.Shield = new ShieldSystem(ctx.Config.Spells);

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
            ctx.LootState = new LootPhaseState(ctx);
            ctx.UpgradeState = new StatUpgradeState(ctx);

            var flowGo = new GameObject("GameFlow");
            flowGo.AddComponent<GameFlowController>().Initialize(ctx);
            ctx.Machine.TransitionTo(ctx.BootState);
        }
    }
}
