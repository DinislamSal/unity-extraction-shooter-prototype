using System;
using System.IO;
using UnityEngine;

namespace OfflineExtraction.Core
{
    public sealed class SaveService
    {
        private const string FileName = "profile.json";
        private readonly string savePath;
        private readonly string backupPath;

        public SaveService()
        {
            savePath = Path.Combine(Application.persistentDataPath, FileName);
            backupPath = savePath + ".backup";
        }

        public PlayerData Load()
        {
            PlayerData data = TryLoad(savePath) ?? TryLoad(backupPath) ?? new PlayerData();
            Normalize(data);
            return data;
        }

        public void Save(PlayerData data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            string temporaryPath = savePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
            if (File.Exists(savePath)) File.Copy(savePath, backupPath, true);
            File.Copy(temporaryPath, savePath, true);
            File.Delete(temporaryPath);
        }

        private static PlayerData TryLoad(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<PlayerData>(File.ReadAllText(path)); }
            catch (Exception exception)
            {
                Debug.LogWarning($"Не удалось прочитать сохранение {path}: {exception.Message}");
                return null;
            }
        }

        private static void Normalize(PlayerData data)
        {
            if (data.saveVersion < 2)
            {
                data.vitals ??= new PlayerVitals();
                data.vitals.rightArm = Mathf.RoundToInt(60f * .60f);
                data.vitals.rightLeg = Mathf.RoundToInt(65f * .20f);
                data.saveVersion = 2;
            }
            if (data.saveVersion < 3)
            {
                data.stash ??= new System.Collections.Generic.List<ItemInstance>();
                foreach (ItemInstance item in data.stash) item.condition = 100;
                data.saveVersion = 3;
            }
            if (data.saveVersion < 4)
            {
                data.stash ??= new System.Collections.Generic.List<ItemInstance>();
                foreach (ItemInstance item in data.stash)
                {
                    item.loadedAmmoDefinitionId ??= "";
                    item.loadedAmmoCount = Mathf.Max(0, item.loadedAmmoCount);
                }
                data.saveVersion = 4;
            }
            if (data.saveVersion < 5)
            {
                data.vitals ??= new PlayerVitals();
                data.vitals.head = 35;
                data.vitals.chest = 85;
                data.vitals.abdomen = 70;
                data.vitals.leftArm = 60;
                data.vitals.rightArm = 60;
                data.vitals.leftLeg = 65;
                data.vitals.rightLeg = 65;
                data.saveVersion = 5;
            }
            if (data.saveVersion < 6)
            {
                data.bunkerModules ??= new System.Collections.Generic.List<BunkerModuleState>();
                data.saveVersion = 6;
            }
            if (data.saveVersion < 7)
            {
                data.bunkerFuelUntilUtcTicks = 0;
                data.saveVersion = 7;
            }
            data.playerName ??= "Оператор";
            data.unlockedSkinIds ??= new System.Collections.Generic.List<string> { "recruit" };
            data.abilities ??= new System.Collections.Generic.List<AbilityRank>();
            data.stash ??= new System.Collections.Generic.List<ItemInstance>();
            data.statistics ??= new PlayerStatistics();
            data.vitals ??= new PlayerVitals();
            data.bunkerModules ??= new System.Collections.Generic.List<BunkerModuleState>();
            data.level = Mathf.Clamp(data.level, 1, Progression.MaxLevel);
            data.experience = Mathf.Max(0, data.experience);
            data.abilityPoints = Mathf.Max(0, data.abilityPoints);
            if (data.stash.Count == 0) ItemCatalog.AddStarterItems(data.stash);
            foreach (ItemInstance item in data.stash)
            {
                item.attachmentIds ??= new System.Collections.Generic.List<string>();
                item.installedMagazineInstanceId ??= "";
            }
            ItemCatalog.AddMissingDemoGear(data.stash);
            ItemCatalog.EnsurePermanentKnife(data.stash);
            ItemCatalog.RepairRootLayout(data.stash);
        }
    }
}
