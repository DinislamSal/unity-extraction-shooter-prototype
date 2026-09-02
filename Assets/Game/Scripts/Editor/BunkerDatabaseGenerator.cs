#if UNITY_EDITOR
using System.Collections.Generic;
using OfflineExtraction.Core;
using UnityEditor;
using UnityEngine;

namespace OfflineExtraction.EditorTools
{
    public static class BunkerDatabaseGenerator
    {
        private const string Folder = "Assets/Game/Resources/BunkerModules";

        [InitializeOnLoadMethod]
        private static void CreateMissingAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources/BunkerModules"))
                AssetDatabase.CreateFolder("Assets/Game/Resources", "BunkerModules");
            Create("generator", "ДИЗЕЛЬНЫЙ ГЕНЕРАТОР", "Обеспечивает энергией остальные помещения.", "+5% к восстановлению энергии");
            Create("storage", "СКЛАД", "Расширяет полезное пространство постоянного хранилища.", "+10 ячеек хранилища");
            Create("workbench", "ОРУЖЕЙНЫЙ ВЕРСТАК", "Открывает обслуживание и сборку оружия.", "Скидка 5% на модификации");
            Create("bed", "ЖИЛОЙ БЛОК", "Улучшает восстановление оператора между рейдами.", "+5% к восстановлению здоровья");
            Create("hall_of_fame", "ЗАЛ СЛАВЫ", "Хранит редкие трофеи и активирует их бонусы.", "Открыт первый слот трофея");
            AssetDatabase.SaveAssets();
        }

        private static void Create(string id, string title, string description, string firstBonus)
        {
            string path = $"{Folder}/{id}.asset";
            BunkerModuleSO module = AssetDatabase.LoadAssetAtPath<BunkerModuleSO>(path);
            if (module != null) return;
            module = ScriptableObject.CreateInstance<BunkerModuleSO>();
            module.id = id; module.displayName = title; module.description = description;
            module.levels = new List<BunkerLevelData>
            {
                Level(firstBonus, ("scrap_metal", 1), ("toolkit", 1)),
                Level("Улучшенный эффект модуля", ("scrap_metal", 3), ("electronics", 2)),
                Level("Максимальный эффект модуля", ("scrap_metal", 5), ("electronics", 4), ("toolkit", 2))
            };
            AssetDatabase.CreateAsset(module, path);
        }

        private static BunkerLevelData Level(string bonus, params (string id, int count)[] resources)
        {
            var level = new BunkerLevelData { bonusDescription = bonus };
            foreach ((string id, int count) in resources) level.requirements.Add(new BunkerRequirement { itemId = id, quantity = count });
            return level;
        }
    }
}
#endif
