using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfflineExtraction.Core
{
    [Serializable]
    public sealed class PlayerData
    {
        public int saveVersion = 7;
        public string playerName = "Оператор";
        public int level = 1;
        public int experience;
        public int abilityPoints;
        public int money = 250000;
        public string equippedSkinId = "recruit";
        public List<string> unlockedSkinIds = new() { "recruit" };
        public List<AbilityRank> abilities = new();
        public List<ItemInstance> stash = new();
        public PlayerStatistics statistics = new();
        public PlayerVitals vitals = new();
        public List<BunkerModuleState> bunkerModules = new();
        public long bunkerFuelUntilUtcTicks;

        public int ExperienceForNextLevel => Progression.ExperienceRequired(level);

        public int GetAbilityRank(string abilityId)
        {
            AbilityRank rank = abilities.Find(item => item.id == abilityId);
            return rank == null ? 0 : rank.rank;
        }

        public bool TryUpgradeAbility(string abilityId)
        {
            if (abilityPoints <= 0) return false;
            AbilityRank rank = abilities.Find(item => item.id == abilityId);
            if (rank == null)
            {
                rank = new AbilityRank { id = abilityId };
                abilities.Add(rank);
            }

            if (rank.rank >= 5) return false;
            rank.rank++;
            abilityPoints--;
            return true;
        }

        public List<string> AddExperience(int amount)
        {
            var rewards = new List<string>();
            experience += Mathf.Max(0, amount);
            while (level < Progression.MaxLevel && experience >= ExperienceForNextLevel)
            {
                experience -= ExperienceForNextLevel;
                level++;
                abilityPoints++;
                if (level % 5 == 0)
                {
                    string skinId = $"level_{level}";
                    if (!unlockedSkinIds.Contains(skinId)) unlockedSkinIds.Add(skinId);
                    rewards.Add($"Открыт новый скин за {level} уровень");
                }
            }
            return rewards;
        }
    }

    [Serializable]
    public sealed class PlayerVitals
    {
        public int head = 35;
        public int chest = 85;
        public int abdomen = 70;
        public int leftArm = 60;
        public int rightArm = 60;
        public int leftLeg = 65;
        public int rightLeg = 65;
        public int hydration = 100;
        public int nutrition = 100;
        public int energy = 100;
        public List<string> bleedingParts = new();
        public List<string> fracturedParts = new();
        public List<string> destroyedParts = new();

        public int CurrentHealth => head + chest + abdomen + leftArm + rightArm + leftLeg + rightLeg;
        public const int MaxHealth = 440;
    }

    [Serializable]
    public sealed class ItemInstance
    {
        public string instanceId;
        public string definitionId;
        [Range(0, 100)] public int condition = 100;
        public int x;
        public int y;
        public bool rotated;
        public int quantity = 1;
        public string equippedSlot;
        public string parentContainerId;
        public bool folded;
        public bool permanent;
        public List<string> attachmentIds = new();
        public string loadedAmmoDefinitionId;
        [Min(0)] public int loadedAmmoCount;
        public string installedMagazineInstanceId;

        public static ItemInstance Create(string definitionId, int x = 0, int y = 0, int quantity = 1, bool folded = false)
        {
            return new ItemInstance
            {
                instanceId = Guid.NewGuid().ToString("N"),
                definitionId = definitionId,
                condition = 100,
                x = x,
                y = y,
                quantity = Mathf.Max(1, quantity),
                folded = folded
            };
        }
    }

    [Serializable]
    public sealed class AbilityRank
    {
        public string id;
        [Range(0, 5)] public int rank;
    }

    [Serializable]
    public sealed class PlayerStatistics
    {
        public int raids;
        public int survivedRaids;
        public int deaths;
        public int kills;
        public int bossesKilled;
        public long extractedValue;
        public long lostLoadoutValue;
        public int bestRaidValue;
        public float raidTimeMinutes;

        public float SurvivalRate => raids == 0 ? 0f : survivedRaids * 100f / raids;
        public float KillDeathRatio => deaths == 0 ? kills : (float)kills / deaths;
    }

    public static class Progression
    {
        public const int MaxLevel = 50;

        public static int ExperienceRequired(int level)
        {
            return Mathf.RoundToInt(900f + 175f * level + 35f * level * level);
        }
    }
}
