using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfflineExtraction.Core
{
    public static class ItemCatalog
    {
        private static Dictionary<string, ItemSO> items;

        public static IReadOnlyCollection<ItemSO> All { get { EnsureLoaded(); return items.Values; } }

        public static ItemSO Get(string id)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(id) && items.TryGetValue(id, out ItemSO definition)) return definition;
            if (items.TryGetValue("bandage", out ItemSO fallback)) return fallback;
            throw new InvalidOperationException("Item database is empty. Create ItemSO assets in Resources/Items.");
        }

        public static void Reload() { items = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (items != null) return;
            items = new Dictionary<string, ItemSO>(StringComparer.Ordinal);
            foreach (ItemSO item in Resources.LoadAll<ItemSO>("Items"))
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id)) continue;
                if (!items.TryAdd(item.id, item)) Debug.LogWarning($"Duplicate ItemSO id: {item.id}", item);
            }
        }

        public static void GetSize(ItemInstance item, out int width, out int height)
        {
            ItemSO definition = Get(item.definitionId);
            width = item.folded && definition.CanFold ? definition.foldedWidth : definition.width;
            height = item.folded && definition.CanFold ? definition.foldedHeight : definition.height;
            if (item.rotated) (width, height) = (height, width);
        }

        public static void AddStarterItems(List<ItemInstance> stash)
        {
            Add(stash, "rifle_mk1", 0, 0); Add(stash, "smg_c9", 5, 0); Add(stash, "armor_t3", 0, 3);
            Add(stash, "backpack_20", 3, 3); Add(stash, "medkit_field", 6, 3); Add(stash, "intel_drive", 6, 5);
            Add(stash, "ammo_556", 9, 0, 60); Add(stash, "ammo_556", 9, 1, 60); Add(stash, "bandage", 9, 2, 2);
        }

        public static void AddMissingDemoGear(List<ItemInstance> stash)
        {
            Ensure(stash, "helmet_t3", 8, 6); Ensure(stash, "rig_16", 0, 7);
            Ensure(stash, "backpack_35", 4, 6, true); Ensure(stash, "backpack_54", 6, 7, true);
            Ensure(stash, "mod_optic", 7, 9); Ensure(stash, "mod_grip", 9, 9);
            Ensure(stash, "headset_m32", 0, 0); Ensure(stash, "face_shield_t2", 0, 0);
            Ensure(stash, "scrap_metal", 0, 0); Ensure(stash, "electronics", 0, 0); Ensure(stash, "toolkit", 0, 0);
            Ensure(stash, "fuel_can", 0, 0);
            Ensure(stash, "ammo_9x19", 0, 0);
            EnsureLoadedMagazine(stash, "mod_magazine", "ammo_556", 45);
            EnsureLoadedMagazine(stash, "mod_magazine_smg", "ammo_9x19", 30);
        }

        public static void EnsurePermanentKnife(List<ItemInstance> stash)
        {
            ItemInstance knife = stash.Find(item => item.permanent && item.definitionId == "melee_knife")
                ?? stash.Find(item => item.definitionId == "melee_knife");
            if (knife == null)
            {
                knife = ItemInstance.Create("melee_knife");
                stash.Add(knife);
            }
            foreach (ItemInstance item in stash)
                if (item != knife && item.equippedSlot == "melee") item.equippedSlot = null;
            knife.permanent = true;
            knife.equippedSlot = "melee";
            knife.parentContainerId = null;
            knife.folded = false;
            knife.rotated = false;
            knife.condition = 100;
        }

        private static void EnsureLoadedMagazine(List<ItemInstance> stash, string magazineId, string ammoId, int rounds)
        {
            if (stash.Exists(item => item.definitionId == magazineId)) return;
            Ensure(stash, magazineId, 0, 0);
            ItemInstance magazine = stash.Find(item => item.definitionId == magazineId);
            if (magazine == null) return;
            magazine.loadedAmmoDefinitionId = ammoId;
            magazine.loadedAmmoCount = Mathf.Min(rounds, AmmunitionService.MagazineCapacity(magazine));
        }

        private static void Ensure(List<ItemInstance> stash, string id, int x, int y, bool folded = false)
        {
            if (stash.Exists(item => item.definitionId == id)) return;
            ItemInstance item = ItemInstance.Create(id, x, y, 1, folded);
            GetSize(item, out int width, out int height);
            for (int row = 0; row <= 11 - height; row++)
            for (int column = 0; column <= 10 - width; column++)
            {
                RectInt candidate = new(column, row, width, height);
                bool blocked = stash.Exists(other =>
                {
                    if (!string.IsNullOrEmpty(other.equippedSlot) || !string.IsNullOrEmpty(other.parentContainerId)) return false;
                    GetSize(other, out int otherWidth, out int otherHeight);
                    return candidate.Overlaps(new RectInt(other.x, other.y, otherWidth, otherHeight));
                });
                if (blocked) continue;
                item.x = column; item.y = row; stash.Add(item); return;
            }
        }

        public static void RepairRootLayout(List<ItemInstance> stash)
        {
            var occupied = new List<RectInt>();
            foreach (ItemInstance item in stash)
            {
                if (!string.IsNullOrEmpty(item.equippedSlot) || !string.IsNullOrEmpty(item.parentContainerId)) continue;
                GetSize(item, out int width, out int height);
                RectInt current = new(item.x, item.y, width, height);
                bool valid = item.x >= 0 && item.y >= 0 && item.x + width <= 10 && item.y + height <= 11 && !occupied.Exists(rect => rect.Overlaps(current));
                if (!valid)
                {
                    bool found = false;
                    for (int row = 0; row <= 11 - height && !found; row++)
                    for (int column = 0; column <= 10 - width; column++)
                    {
                        RectInt candidate = new(column, row, width, height);
                        if (occupied.Exists(rect => rect.Overlaps(candidate))) continue;
                        item.x = column; item.y = row; current = candidate; found = true; break;
                    }
                    if (!found && Get(item.definitionId).CanFold)
                    {
                        item.folded = true; GetSize(item, out width, out height);
                        for (int row = 0; row <= 11 - height && !found; row++)
                        for (int column = 0; column <= 10 - width; column++)
                        {
                            RectInt candidate = new(column, row, width, height);
                            if (occupied.Exists(rect => rect.Overlaps(candidate))) continue;
                            item.x = column; item.y = row; current = candidate; found = true; break;
                        }
                    }
                }
                occupied.Add(current);
            }
        }

        private static void Add(List<ItemInstance> stash, string id, int x, int y, int quantity = 1)
            => stash.Add(ItemInstance.Create(id, x, y, quantity));
    }
}
