using System;
using System.Collections.Generic;
using OfflineExtraction.Core;
using UnityEngine;

namespace OfflineExtraction.Raid
{
    [Serializable]
    public sealed class RaidLoadout
    {
        public string operatorName;
        public List<ItemInstance> items = new();
        public List<string> startingItemIds = new();
        public PlayerVitals vitals = new();

        public static RaidLoadout Capture(PlayerData player)
        {
            var result = new RaidLoadout { operatorName = player.playerName };
            result.vitals = JsonUtility.FromJson<PlayerVitals>(JsonUtility.ToJson(player.vitals));
            var includedIds = new HashSet<string>();
            foreach (ItemInstance item in player.stash)
                if (!string.IsNullOrEmpty(item.equippedSlot)) AddWithContents(player.stash, item, result.items, includedIds);
            result.startingItemIds.AddRange(includedIds);
            return result;
        }

        private static void AddWithContents(List<ItemInstance> source, ItemInstance item, List<ItemInstance> target, HashSet<string> includedIds)
        {
            if (item == null || !includedIds.Add(item.instanceId)) return;
            target.Add(JsonUtility.FromJson<ItemInstance>(JsonUtility.ToJson(item)));
            foreach (ItemInstance child in source)
                if (child.parentContainerId == item.instanceId) AddWithContents(source, child, target, includedIds);
        }
    }

    public static class RaidContext
    {
        public static RaidLoadout Loadout { get; private set; }
        public static void Prepare(PlayerData player) => Loadout = RaidLoadout.Capture(player);
        public static void Clear() => Loadout = null;
    }
}
