using System.Collections.Generic;
using UnityEngine;

namespace OfflineExtraction.Core
{
    public enum ItemCategory { Weapon, Armor, Ammo, Medical, Backpack, Modification, Valuable }
    public enum ItemRarity { Common, Rare, Epic, Legendary, Mythic }
    public enum FireMode { Single, Burst, Automatic }
    public enum ModificationSlot { None, Muzzle, Optic, Stock, Tactical, Magazine, Grip }

    [System.Serializable]
    public sealed class WeaponData
    {
        public string caliber;
        [Min(0)] public int damage;
        [Min(0)] public int rateOfFire;
        [Min(1)] public int magazineCapacity = 1;
        [Range(0, 100)] public int ergonomics;
        [Range(0, 100)] public int recoilControl;
        public List<FireMode> fireModes = new();
    }

    [System.Serializable]
    public sealed class ArmorData
    {
        [Range(0, 6)] public int armorClass;
        [Min(0)] public int maxDurability;
        public string protectedArea;
    }

    [System.Serializable]
    public sealed class HeadsetData
    {
        [Min(1f)] public float hearingDistanceMultiplier = 1f;
        [Range(0f, 1f)] public float ambientNoiseReduction;
        [Range(0f, 1f)] public float gunshotProtection;
    }

    [System.Serializable]
    public sealed class AmmoData
    {
        public string caliber;
        [Min(0)] public int damage;
        [Min(0)] public int penetration;
    }

    [System.Serializable]
    public sealed class MedicalData
    {
        [Min(0)] public int healingAmount;
        [Min(0f)] public float useTime;
        public bool treatsBleeding;
        public bool treatsFracture;
    }

    [System.Serializable]
    public sealed class ModificationData
    {
        public ModificationSlot slot;
        public int ergonomicsModifier;
        public int recoilModifier;
        [Min(0)] public int magazineCapacity;
        public string magazineCaliber;
        public List<string> compatibleWeaponIds = new();
    }

    [CreateAssetMenu(fileName = "Item", menuName = "Offline Extraction/Item Definition")]
    public sealed class ItemSO : ScriptableObject
    {
        [HideInInspector] public int schemaVersion;

        [Header("Identity")]
        public string id;
        public string itemName;
        [TextArea(2, 5)] public string description;
        public Sprite icon;
        [Tooltip("Cell in the 4x4 fallback atlas; -1 uses the color placeholder.")]
        public int atlasIconIndex = -1;

        [Header("Classification")]
        public ItemCategory category;
        public ItemRarity rarity;
        public bool canBeBoughtFromTrader = true;
        public int price;
        public Color color = Color.gray;

        [Header("Inventory geometry")]
        [Min(1)] public int width = 1;
        [Min(1)] public int height = 1;
        public int foldedWidth;
        public int foldedHeight;
        public int internalWidth;
        public int internalHeight;

        [Header("Physical state")]
        [Min(0f)] public float weightKg;
        [Min(1)] public int maxStack = 1;

        [Header("Category-specific data")]
        public WeaponData weapon = new();
        public ArmorData armor = new();
        public HeadsetData headset = new();
        public AmmoData ammunition = new();
        public MedicalData medicine = new();
        public ModificationData modification = new();

        public new string name => string.IsNullOrWhiteSpace(itemName) ? id : itemName;
        public bool IsContainer => internalWidth > 0 && internalHeight > 0;
        public bool CanFold => foldedWidth > 0 && foldedHeight > 0;
    }
}
