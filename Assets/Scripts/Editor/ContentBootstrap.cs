using BattleRunner.Data.Definitions;
using UnityEditor;
using UnityEngine;

namespace BattleRunner.Editor
{
    /// <summary>
    /// Materializes the ContentFactory content set as editable .asset files and wires
    /// Resources/GameConfig.asset. The runtime falls back to in-memory content when
    /// this hasn't run yet, so generation is a designer convenience, not a requirement.
    /// </summary>
    [InitializeOnLoad]
    public static class ContentBootstrap
    {
        private const string ConfigPath = "Assets/Resources/GameConfig.asset";
        private const string ContentRoot = "Assets/Content";

        static ContentBootstrap()
        {
            EditorApplication.delayCall += EnsureContent;
        }

        public static void EnsureContent()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null) return;
            Generate();
        }

        [MenuItem("BattleRunner/Regenerate Content (overwrite)")]
        public static void RegenerateMenu()
        {
            if (!EditorUtility.DisplayDialog("Regenerate content",
                    "Delete Assets/Content and Resources/GameConfig.asset, then regenerate from ContentFactory?",
                    "Regenerate", "Cancel")) return;
            AssetDatabase.DeleteAsset(ContentRoot);
            AssetDatabase.DeleteAsset(ConfigPath);
            Generate();
        }

        private static void Generate()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets", "Content");
            EnsureFolder(ContentRoot, "Gear");
            EnsureFolder(ContentRoot, "Levels");
            EnsureFolder(ContentRoot, "Chunks");
            EnsureFolder(ContentRoot, "Stats");

            GameConfig config = ContentFactory.BuildConfig();

            AssetDatabase.StartAssetEditing();
            try
            {
                Save(config.Balance, $"{ContentRoot}/Balance.asset");
                Save(config.Input, $"{ContentRoot}/InputSettings.asset");
                Save(config.Spells, $"{ContentRoot}/Spells.asset");

                foreach (StatDefinition stat in config.Stats)
                    Save(stat, $"{ContentRoot}/Stats/{stat.name}.asset");

                foreach (GearItemDefinition gear in config.AllGear)
                    Save(gear, $"{ContentRoot}/Gear/{gear.name}.asset");

                // Shared across levels: the loot table and boss definitions come from
                // the first level's references.
                if (config.Levels.Length > 0)
                {
                    Save(config.Levels[0].LootTable, $"{ContentRoot}/LootTable.asset");
                    foreach (LevelDefinition level in config.Levels)
                        if (AssetDatabase.GetAssetPath(level.Boss) == string.Empty)
                            Save(level.Boss, $"{ContentRoot}/{level.Boss.name}.asset");
                }

                foreach (LevelDefinition level in config.Levels)
                {
                    foreach (ChunkDefinition chunk in level.Chunks)
                        Save(chunk, $"{ContentRoot}/Chunks/{chunk.name}.asset");
                    Save(level, $"{ContentRoot}/Levels/{level.name}.asset");
                }

                Save(config, ConfigPath);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[BattleRunner] Content generated: Assets/Content + Resources/GameConfig.asset.");
        }

        private static void Save(Object asset, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) == null)
                AssetDatabase.CreateAsset(asset, path);
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
