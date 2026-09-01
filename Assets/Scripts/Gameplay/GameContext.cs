using BattleRunner.Core.Flow;
using BattleRunner.Core.Run;
using BattleRunner.Core.Save;
using BattleRunner.Core.Stats;
using BattleRunner.Data.Channels;
using BattleRunner.Data.Definitions;
using BattleRunner.Gameplay.Combat;
using BattleRunner.Gameplay.Crowd;
using BattleRunner.Gameplay.States;
using BattleRunner.Gameplay.Track;
using BattleRunner.Meta.Services;
using BattleRunner.Meta.UI;
using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>Everything the states need, assembled once by GameBootstrap.</summary>
    public sealed class GameContext
    {
        // Content & config
        public GameConfig Config;
        public int TierCap;
        public Material CrowdMaterial;

        // Persistence & services
        public PlayerProfile Profile;
        public ISaveService SaveService;
        public IAdService Ads;
        public IIapService Iap;
        public IBattlePassService BattlePass;

        // Input intent channels
        public FloatEventChannel LaneTargetChannel;
        public VoidEventChannel FlickUpChannel;
        public VoidEventChannel FlickDownChannel;
        public LongEventChannel ForceChangedChannel;

        // Scene systems
        public GameObject ArenaRoot;
        public CrowdController Crowd;
        public HeroVisual Hero;
        public TrackController TrackController;
        public BossView BossView;
        public CameraRig CameraRig;
        public SpellSystem Spell;
        public ShieldSystem Shield;

        // UI
        public MainMenuScreen MenuScreen;
        public HudScreen Hud;
        public LootScreen LootScreen;
        public SkillTreeScreen SkillScreen;
        public ResurrectPrompt Resurrect;
        public SlotSelectScreen SlotScreen;
        public TutorialOverlay TutorialOverlay;
        public TutorialCoach Tutorial;

        // Flow
        public GameStateMachine Machine;
        public BootState BootState;
        public SlotSelectState SlotState;
        public MainMenuState MenuState;
        public RunLoadingState RunLoadingState;
        public RunnerLoopState RunnerState;
        public BossEncounterState BossState;
        public LootPhaseState LootState;
        public StatUpgradeState UpgradeState;

        // Per-run data
        public RunState Run;
        public RunResult LastResult;
        public StatSheet CurrentStats;

        public LevelDefinition CurrentLevel => Config.LevelFor(Profile.CurrentLevelIndex);

        /// <summary>
        /// Every save banks tutorial progress first. The boss-defeat path used to write the
        /// profile before the state's Exit ran the coach's Persist, so the shield beat — the
        /// only one taught in the boss phase — never reached disk and was re-taught forever.
        /// </summary>
        public void SaveProfile()
        {
            // Before a slot is chosen there is nowhere legitimate to write. Saving anyway
            // would stamp the placeholder profile over whichever file the service happens
            // to be pointing at.
            if (ActiveSlot < 0) return;
            Tutorial?.Persist();
            SaveService.Save(Profile);
        }

        /// <summary>Which save the session is playing. -1 until a slot is chosen.</summary>
        public int ActiveSlot = -1;

        /// <summary>
        /// Point the session at one save. Everything reads Profile through this context, so
        /// swapping the field is enough — but the tutorial coach latched its progress at
        /// construction, so it has to be rebuilt against whatever this slot has been taught.
        /// </summary>
        public void ActivateSlot(int slot)
        {
            ActiveSlot = slot;
            SaveService = FileSaveService.ForSlot(slot);
            Profile = SaveService.Load();
            Tutorial?.AdoptProfile();
            CurrentStats = ProfileStatsResolver.Resolve(Profile, Config);
        }
    }
}
