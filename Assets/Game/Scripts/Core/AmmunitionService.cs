using UnityEngine;

namespace OfflineExtraction.Core
{
    public static class AmmunitionService
    {
        public static bool IsMagazine(ItemSO definition)
            => definition != null && definition.category == ItemCategory.Modification && definition.modification.slot == ModificationSlot.Magazine;

        public static bool IsCompatibleMagazine(ItemInstance weapon, ItemInstance magazine)
        {
            if (weapon == null || magazine == null) return false;
            ItemSO weaponDefinition = ItemCatalog.Get(weapon.definitionId);
            ItemSO magazineDefinition = ItemCatalog.Get(magazine.definitionId);
            if (weaponDefinition.category != ItemCategory.Weapon || !IsMagazine(magazineDefinition)) return false;
            if (magazineDefinition.modification.magazineCaliber != weaponDefinition.weapon.caliber) return false;
            return magazineDefinition.modification.compatibleWeaponIds == null
                || magazineDefinition.modification.compatibleWeaponIds.Count == 0
                || magazineDefinition.modification.compatibleWeaponIds.Contains(weapon.definitionId);
        }

        public static int MagazineCapacity(ItemInstance magazine)
        {
            if (magazine == null) return 0;
            ItemSO definition = ItemCatalog.Get(magazine.definitionId);
            return IsMagazine(definition) ? Mathf.Max(1, definition.modification.magazineCapacity) : 0;
        }

        // Shared rules used by the lobby and the future raid weapon controller.
        public static int WeaponMagazineCapacity(ItemInstance weapon)
        {
            ItemSO definition = ItemCatalog.Get(weapon.definitionId);
            int capacity = definition.weapon.magazineCapacity;
            if (weapon.attachmentIds != null && weapon.attachmentIds.Contains("magazine"))
            {
                ItemSO magazine = ItemCatalog.Get(weapon.definitionId == "smg_c9" ? "mod_magazine_smg" : "mod_magazine");
                if (magazine.modification.magazineCaliber == definition.weapon.caliber)
                    capacity = Mathf.Max(capacity, magazine.modification.magazineCapacity);
            }
            return Mathf.Max(0, capacity);
        }

        public static bool TryLoadMagazine(ItemInstance magazine, ItemInstance ammoStack, int requested, out int loaded)
        {
            loaded = 0;
            if (magazine == null || ammoStack == null || requested <= 0) return false;
            ItemSO magazineDefinition = ItemCatalog.Get(magazine.definitionId);
            ItemSO ammoDefinition = ItemCatalog.Get(ammoStack.definitionId);
            if (!IsMagazine(magazineDefinition) || ammoDefinition.category != ItemCategory.Ammo) return false;
            if (magazineDefinition.modification.magazineCaliber != ammoDefinition.ammunition.caliber) return false;
            if (!string.IsNullOrEmpty(magazine.loadedAmmoDefinitionId) && magazine.loadedAmmoDefinitionId != ammoStack.definitionId) return false;
            int free = MagazineCapacity(magazine) - magazine.loadedAmmoCount;
            loaded = Mathf.Min(requested, Mathf.Min(free, ammoStack.quantity));
            if (loaded <= 0) return false;
            magazine.loadedAmmoDefinitionId = ammoStack.definitionId;
            magazine.loadedAmmoCount += loaded;
            ammoStack.quantity -= loaded;
            return true;
        }

        public static bool TryLoad(ItemInstance weapon, ItemInstance ammoStack, int requested, out int loaded)
        {
            loaded = 0;
            if (weapon == null || ammoStack == null || requested <= 0) return false;
            ItemSO weaponDefinition = ItemCatalog.Get(weapon.definitionId);
            ItemSO ammoDefinition = ItemCatalog.Get(ammoStack.definitionId);
            if (weaponDefinition.category != ItemCategory.Weapon || ammoDefinition.category != ItemCategory.Ammo) return false;
            if (weaponDefinition.weapon.caliber != ammoDefinition.ammunition.caliber) return false;
            if (!string.IsNullOrEmpty(weapon.loadedAmmoDefinitionId) && weapon.loadedAmmoDefinitionId != ammoStack.definitionId) return false;

            int free = WeaponMagazineCapacity(weapon) - weapon.loadedAmmoCount;
            loaded = Mathf.Min(requested, Mathf.Min(free, ammoStack.quantity));
            if (loaded <= 0) return false;
            weapon.loadedAmmoDefinitionId = ammoStack.definitionId;
            weapon.loadedAmmoCount += loaded;
            ammoStack.quantity -= loaded;
            return true;
        }

        public static bool TryUnload(ItemInstance weapon, ItemInstance targetAmmoStack, int requested, out int unloaded)
        {
            unloaded = 0;
            if (weapon == null || targetAmmoStack == null || requested <= 0 || weapon.loadedAmmoCount <= 0) return false;
            if (targetAmmoStack.definitionId != weapon.loadedAmmoDefinitionId) return false;
            ItemSO ammoDefinition = ItemCatalog.Get(targetAmmoStack.definitionId);
            int free = Mathf.Max(0, ammoDefinition.maxStack - targetAmmoStack.quantity);
            unloaded = Mathf.Min(requested, Mathf.Min(free, weapon.loadedAmmoCount));
            if (unloaded <= 0) return false;
            targetAmmoStack.quantity += unloaded;
            weapon.loadedAmmoCount -= unloaded;
            if (weapon.loadedAmmoCount == 0) weapon.loadedAmmoDefinitionId = "";
            return true;
        }
    }
}
