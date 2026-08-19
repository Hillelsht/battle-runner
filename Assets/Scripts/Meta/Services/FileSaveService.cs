using System;
using System.IO;
using BattleRunner.Core.Save;
using UnityEngine;

namespace BattleRunner.Meta.Services
{
    /// <summary>
    /// JSON save under persistentDataPath. Write path: serialize -> temp file -> swap,
    /// so an interrupted write leaves the previous save intact; a checksum line detects
    /// torn or hand-edited files (doc 01, R8).
    /// </summary>
    public sealed class FileSaveService : ISaveService
    {
        private readonly string _path;
        private readonly string _tempPath;

        public FileSaveService(string fileName = "profile.sav")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
            _tempPath = _path + ".tmp";
        }

        public PlayerProfile Load()
        {
            try
            {
                if (!File.Exists(_path)) return NewProfile();

                string raw = File.ReadAllText(_path);
                int newline = raw.IndexOf('\n');
                if (newline <= 0)
                {
                    Debug.LogWarning("[Save] Malformed save file; starting fresh.");
                    return NewProfile();
                }

                string checksum = raw.Substring(0, newline).Trim();
                string json = raw.Substring(newline + 1);
                if (!Checksum.Verify(json, checksum))
                {
                    Debug.LogWarning("[Save] Save checksum mismatch (corrupt or edited); starting fresh.");
                    return NewProfile();
                }

                var profile = JsonUtility.FromJson<PlayerProfile>(json);
                if (profile == null) return NewProfile();
                return SaveMigrator.Migrate(profile);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Load failed ({e.Message}); starting fresh.");
                return NewProfile();
            }
        }

        public void Save(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            try
            {
                string json = JsonUtility.ToJson(profile);
                string payload = Checksum.Compute(json) + "\n" + json;
                File.WriteAllText(_tempPath, payload);
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(_tempPath, _path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Save failed: {e.Message}");
            }
        }

        private static PlayerProfile NewProfile() =>
            SaveMigrator.Migrate(new PlayerProfile { SchemaVersion = 1 });
    }
}
