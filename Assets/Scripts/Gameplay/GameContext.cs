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
        public StatUpgradeScreen StatScreen;
        public ResurrectPrompt Resurrect;

        // Flow
        public GameStateMachine Machine;
        public BootState BootState;
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

        public void SaveProfile() => SaveService.Save(Profile);
    }
}
