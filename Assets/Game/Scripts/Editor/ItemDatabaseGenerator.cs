#if UNITY_EDITOR
using System.IO;
using OfflineExtraction.Core;
using UnityEditor;
using UnityEngine;

namespace OfflineExtraction.EditorTools
{
    public static class ItemDatabaseGenerator
    {
        private const string Folder = "Assets/Game/Resources/Items";

        [InitializeOnLoadMethod]
        private static void CreateMissingAssets()
        {
            // Keep external code and art changes synchronized with the open Editor.
            EditorPrefs.SetInt("kAutoRefresh", 1);
            EditorPrefs.SetBool("kAutoRefresh", true);
            EditorPrefs.SetBool("DirectoryMonitoring", true);
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources"))
                AssetDatabase.CreateFolder("Assets/Game", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/Resources", "Items");

            Create("rifle_mk1", "ШТУРМОВАЯ ВИНТОВКА", "Основная штурмовая винтовка калибра 5.56 × 45 мм.", ItemCategory.Weapon, 5, 2, 78000, new Color(.25f,.34f,.31f), weight: 4.2f, caliber: "5.56x45", ergonomics: 36, recoil: 48);
            Create("smg_c9", "ПИСТОЛЕТ-ПУЛЕМЁТ", "Компактный пистолет-пулемёт для ближнего боя.", ItemCategory.Weapon, 4, 2, 46000, new Color(.27f,.31f,.33f), weight: 2.8f, caliber: "9x19", ergonomics: 58, recoil: 57);
            Create("melee_knife", "ТАКТИЧЕСКИЙ НОЖ", "Постоянное оружие оператора. Нельзя выбросить или потерять.", ItemCategory.Weapon, 1, 3, 0, new Color(.32f,.35f,.34f), weight: .35f, canBuy: false);
            Create("armor_t3", "БРОНЕЖИЛЕТ КЛ. 3", "Бронежилет третьего класса защиты.", ItemCategory.Armor, 3, 3, 52000, new Color(.31f,.35f,.25f), weight: 7.5f);
            Create("backpack_20", "РЮКЗАК 20 Л", "Компактный рейдовый рюкзак.", ItemCategory.Backpack, 3, 4, 34000, new Color(.32f,.27f,.20f), 2, 1, 4, 5, 1.2f);
            Create("backpack_35", "РЮКЗАК 35", "Средний рейдовый рюкзак.", ItemCategory.Backpack, 4, 5, 68000, new Color(.27f,.25f,.18f), 2, 1, 5, 7, 1.8f);
            Create("backpack_54", "РЮКЗАК 54", "Большой рейдовый рюкзак.", ItemCategory.Backpack, 4, 6, 112000, new Color(.22f,.25f,.20f), 2, 2, 6, 9, 2.6f);
            Create("rig_16", "РАЗГРУЗКА 16", "Тактическая разгрузочная система.", ItemCategory.Armor, 4, 3, 29000, new Color(.29f,.32f,.22f), 2, 1, 4, 4, 1.6f);
            Create("helmet_t3", "ШЛЕМ КЛ. 3", "Шлем третьего класса защиты.", ItemCategory.Armor, 2, 2, 38000, new Color(.29f,.33f,.30f), weight: 1.4f);
            Create("headset_m32", "ТАКТИЧЕСКИЕ НАУШНИКИ M32", "Усиливают тихие звуки и приглушают опасные импульсные шумы.", ItemCategory.Armor, 2, 2, 42000, new Color(.20f,.24f,.22f), weight: .45f);
            Create("face_shield_t2", "ЗАЩИТНОЕ СТЕКЛО КЛ. 2", "Защищает лицо от осколков и слабых боеприпасов.", ItemCategory.Armor, 2, 2, 26000, new Color(.22f,.30f,.30f), weight: 1f);
            Create("ammo_556", "5.56 × 45", "Пачка винтовочных боеприпасов.", ItemCategory.Ammo, 1, 1, 8200, new Color(.48f,.36f,.15f), weight: .7f, maxStack: 60, caliber: "5.56x45");
            Create("ammo_9x19", "9 × 19", "Пачка пистолетных боеприпасов.", ItemCategory.Ammo, 1, 1, 5400, new Color(.42f,.32f,.16f), weight: .45f, maxStack: 60, caliber: "9x19");
            Create("medkit_field", "ПОЛЕВАЯ АПТЕЧКА", "Комплект для восстановления здоровья в рейде.", ItemCategory.Medical, 2, 2, 14500, new Color(.38f,.16f,.14f), weight: .8f, healing: 120);
            Create("bandage", "БИНТ", "Останавливает лёгкое кровотечение.", ItemCategory.Medical, 1, 1, 2100, new Color(.56f,.55f,.49f), weight: .1f, maxStack: 4, healing: 15, bleeding: true);
            Create("surgical_kit", "ХИРУРГИЧЕСКИЙ НАБОР", "Позволяет восстановить уничтоженную часть тела.", ItemCategory.Medical, 2, 2, 38500, new Color(.30f,.38f,.34f), weight: 1.1f, maxStack: 2, healing: 20, fracture: true);
            Create("mod_muzzle", "ТАКТИЧЕСКИЙ ГЛУШИТЕЛЬ", "Снижает звук выстрела и вспышку.", ItemCategory.Modification, 2, 1, 12500, new Color(.25f,.29f,.28f), weight: .5f);
            Create("mod_optic", "ОПТИЧЕСКИЙ ПРИЦЕЛ", "Улучшает точность прицельной стрельбы.", ItemCategory.Modification, 2, 1, 28000, new Color(.24f,.28f,.29f), weight: .6f);
            Create("mod_stock", "ТАКТИЧЕСКИЙ ПРИКЛАД", "Повышает контроль отдачи.", ItemCategory.Modification, 2, 1, 18500, new Color(.27f,.29f,.27f), weight: .7f);
            Create("mod_laser", "ЛАЗЕРНЫЙ ЦЕЛЕУКАЗАТЕЛЬ", "Улучшает стрельбу от бедра.", ItemCategory.Modification, 1, 1, 9200, new Color(.23f,.31f,.29f), weight: .2f);
            Create("mod_magazine", "МАГАЗИН УВЕЛИЧЕННЫЙ", "Увеличенный оружейный магазин.", ItemCategory.Modification, 1, 2, 14800, new Color(.24f,.27f,.25f), weight: .5f);
            Create("mod_magazine_smg", "МАГАЗИН ПП 30", "Магазин на 30 патронов калибра 9 × 19.", ItemCategory.Modification, 1, 2, 9800, new Color(.22f,.25f,.24f), weight: .35f);
            Create("mod_grip", "ТАКТИЧЕСКАЯ РУКОЯТКА", "Улучшает эргономику и контроль оружия.", ItemCategory.Modification, 1, 2, 11600, new Color(.25f,.28f,.26f), weight: .3f);
            Create("intel_drive", "ЗАЩИЩЁННЫЙ НАКОПИТЕЛЬ", "Редкий носитель зашифрованных данных.", ItemCategory.Valuable, 2, 1, 125000, new Color(.18f,.38f,.42f), weight: .4f, canBuy: false, rarity: ItemRarity.Mythic);
            Create("scrap_metal", "МЕТАЛЛОЛОМ", "Строительный металл для модулей бункера.", ItemCategory.Valuable, 1, 1, 1800, new Color(.32f,.34f,.34f), weight: 1.2f, canBuy: false);
            Create("electronics", "ЭЛЕКТРОНИКА", "Электронные компоненты для оборудования бункера.", ItemCategory.Valuable, 1, 1, 6200, new Color(.18f,.33f,.35f), weight: .4f, canBuy: false);
            Create("toolkit", "НАБОР ИНСТРУМЕНТОВ", "Инструменты для строительства и ремонта.", ItemCategory.Valuable, 2, 1, 9800, new Color(.40f,.32f,.18f), weight: 2f, canBuy: false);
            Create("fuel_can", "КАНИСТРА ТОПЛИВА", "Редкий рейдовый ресурс. Обеспечивает генератор энергией на 12 часов.", ItemCategory.Valuable, 2, 2, 24000, new Color(.45f,.27f,.08f), weight: 8f, canBuy: false);
            AssetDatabase.SaveAssets();
        }

        private static void Create(string id, string displayName, string description, ItemCategory category, int width, int height, int price, Color color,
            int foldedWidth = 0, int foldedHeight = 0, int internalWidth = 0, int internalHeight = 0, float weight = 0f,
            int maxStack = 1, string caliber = "", int ergonomics = 0, int recoil = 0, int healing = 0, bool bleeding = false,
            bool fracture = false, bool canBuy = true, ItemRarity rarity = ItemRarity.Common)
        {
            string path = Path.Combine(Folder, id + ".asset").Replace('\\', '/');
            ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemSO>();
                item.id = id; item.itemName = displayName; item.description = description; item.category = category;
                item.width = width; item.height = height; item.price = price; item.color = color;
                item.foldedWidth = foldedWidth; item.foldedHeight = foldedHeight; item.internalWidth = internalWidth; item.internalHeight = internalHeight;
                item.weightKg = weight; item.maxStack = maxStack; item.canBeBoughtFromTrader = canBuy; item.rarity = rarity;
                AssetDatabase.CreateAsset(item, path);
            }
            if (item.schemaVersion < 1)
            {
                item.weapon.caliber = category == ItemCategory.Weapon ? caliber : "";
                item.weapon.ergonomics = ergonomics; item.weapon.recoilControl = recoil;
                item.ammunition.caliber = category == ItemCategory.Ammo ? caliber : "";
                item.medicine.healingAmount = healing; item.medicine.treatsBleeding = bleeding; item.medicine.treatsFracture = fracture;
                ApplySpecializedDefaults(item);
            }
            if (item.schemaVersion < 2) item.atlasIconIndex = AtlasIndex(item.id);
            if (item.schemaVersion < 3 && item.id == "mod_magazine")
            {
                item.modification.magazineCapacity = 45;
                item.modification.magazineCaliber = "5.56x45";
            }
            if (item.schemaVersion < 4)
            {
                if (item.id == "headset_m32")
                {
                    item.headset.hearingDistanceMultiplier = 1.22f;
                    item.headset.ambientNoiseReduction = .28f;
                    item.headset.gunshotProtection = .42f;
                }
                if (item.id == "face_shield_t2")
                {
                    item.armor.armorClass = 2;
                    item.armor.maxDurability = 25;
                    item.armor.protectedArea = "Лицо";
                }
            }
            if (item.schemaVersion < 5)
            {
                if (item.id == "mod_magazine")
                {
                    item.modification.slot = ModificationSlot.Magazine;
                    item.modification.magazineCapacity = 45;
                    item.modification.magazineCaliber = "5.56x45";
                    item.modification.compatibleWeaponIds = new() { "rifle_mk1" };
                }
                if (item.id == "mod_magazine_smg")
                {
                    item.modification.slot = ModificationSlot.Magazine;
                    item.modification.magazineCapacity = 30;
                    item.modification.magazineCaliber = "9x19";
                    item.modification.compatibleWeaponIds = new() { "smg_c9" };
                }
                if (item.id == "ammo_9x19") { item.ammunition.caliber = "9x19"; item.ammunition.damage = 36; item.ammunition.penetration = 18; }
            }
            item.schemaVersion = 5;
            EditorUtility.SetDirty(item);
        }

        private static int AtlasIndex(string id) => id switch
        {
            "rifle_mk1" => 0, "smg_c9" => 1, "armor_t3" => 2, "helmet_t3" => 3,
            "backpack_20" => 4, "backpack_35" => 5, "backpack_54" => 6, "rig_16" => 7,
            "ammo_556" => 8, "medkit_field" => 9, "bandage" => 10, "mod_muzzle" => 11,
            "mod_optic" => 12, "mod_stock" => 13, "mod_laser" => 14, "mod_grip" => 15,
            _ => -1
        };

        private static void ApplySpecializedDefaults(ItemSO item)
        {
            switch (item.id)
            {
                case "rifle_mk1": item.weapon.damage = 48; item.weapon.rateOfFire = 700; item.weapon.magazineCapacity = 30; item.weapon.fireModes = new() { FireMode.Single, FireMode.Automatic }; break;
                case "smg_c9": item.weapon.damage = 34; item.weapon.rateOfFire = 850; item.weapon.magazineCapacity = 30; item.weapon.fireModes = new() { FireMode.Single, FireMode.Automatic }; break;
                case "melee_knife": item.weapon.damage = 55; item.weapon.rateOfFire = 75; item.weapon.magazineCapacity = 1; item.weapon.fireModes = new() { FireMode.Single }; break;
                case "armor_t3": item.armor.armorClass = 3; item.armor.maxDurability = 45; item.armor.protectedArea = "Грудь, живот"; break;
                case "helmet_t3": item.armor.armorClass = 3; item.armor.maxDurability = 35; item.armor.protectedArea = "Голова"; break;
                case "ammo_556": item.ammunition.damage = 52; item.ammunition.penetration = 31; break;
                case "ammo_9x19": item.ammunition.damage = 36; item.ammunition.penetration = 18; break;
                case "medkit_field": item.medicine.useTime = 4.5f; break;
                case "bandage": item.medicine.useTime = 2.5f; break;
                case "mod_muzzle": item.modification.slot = ModificationSlot.Muzzle; item.modification.recoilModifier = 8; break;
                case "mod_optic": item.modification.slot = ModificationSlot.Optic; item.modification.ergonomicsModifier = -3; break;
                case "mod_stock": item.modification.slot = ModificationSlot.Stock; item.modification.recoilModifier = 10; break;
                case "mod_laser": item.modification.slot = ModificationSlot.Tactical; item.modification.ergonomicsModifier = 4; break;
                case "mod_magazine": item.modification.slot = ModificationSlot.Magazine; item.modification.ergonomicsModifier = -5; break;
                case "mod_magazine_smg": item.modification.slot = ModificationSlot.Magazine; item.modification.magazineCapacity = 30; item.modification.magazineCaliber = "9x19"; break;
                case "mod_grip": item.modification.slot = ModificationSlot.Grip; item.modification.ergonomicsModifier = 6; item.modification.recoilModifier = 5; break;
            }
            if (item.category == ItemCategory.Modification)
                item.modification.compatibleWeaponIds = item.id switch
                {
                    "mod_magazine" => new() { "rifle_mk1" },
                    "mod_magazine_smg" => new() { "smg_c9" },
                    _ => new() { "rifle_mk1", "smg_c9" }
                };
        }
    }
}
#endif
