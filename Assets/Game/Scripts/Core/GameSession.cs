using System.Collections.Generic;
using OfflineExtraction.Raid;
using UnityEngine.SceneManagement;

namespace OfflineExtraction.Core
{
    public sealed class GameSession
    {
        private readonly SaveService saves = new();
        public PlayerData Player { get; private set; }
        public string LastNotification { get; private set; } = "Профиль загружен";

        public void Initialize() => Player = saves.Load();

        public void Save()
        {
            saves.Save(Player);
            LastNotification = "Прогресс сохранён";
        }

        public bool Upgrade(string abilityId)
        {
            if (!Player.TryUpgradeAbility(abilityId)) return false;
            Save();
            LastNotification = "Способность улучшена";
            return true;
        }

        public bool UpgradeBunker(string moduleId)
        {
            bool upgraded = BunkerService.TryUpgrade(Player, moduleId, out string message);
            if (upgraded) Save();
            LastNotification = message;
            return upgraded;
        }

        public bool AddBunkerFuel()
        {
            bool added = BunkerService.TryAddFuel(Player, out string message);
            if (added) Save();
            LastNotification = message;
            return added;
        }

        public void SimulateRaid(bool survived)
        {
            PlayerStatistics stats = Player.statistics;
            stats.raids++;
            stats.raidTimeMinutes += survived ? 24f : 11f;
            int raidKills = survived ? 4 : 1;
            stats.kills += raidKills;
            int value = survived ? 185000 : 0;
            int xp = survived ? 1550 : 420;
            if (survived)
            {
                stats.survivedRaids++;
                stats.extractedValue += value;
                stats.bestRaidValue = System.Math.Max(stats.bestRaidValue, value);
                Player.money += value;
            }
            else
            {
                stats.deaths++;
                stats.lostLoadoutValue += 65000;
            }

            List<string> rewards = Player.AddExperience(xp);
            Save();
            LastNotification = survived ? $"Эвакуация: +{xp} опыта, +{value:N0} ₽" : $"Оператор погиб: +{xp} опыта";
            if (rewards.Count > 0) LastNotification += " · " + string.Join(" · ", rewards);
        }

        public void BeginRaid()
        {
            Save();
            RaidContext.Prepare(Player);
            SceneManager.LoadScene("Raid_Telecenter");
        }

        public string CompleteRaid(RaidLoadout loadout, bool survived, float raidMinutes)
        {
            if (loadout == null) return "Данные рейда потеряны";
            Player.statistics.raids++;
            Player.statistics.raidTimeMinutes += System.Math.Max(0f, raidMinutes);
            var initial = new HashSet<string>(loadout.startingItemIds ?? new List<string>());
            Player.stash.RemoveAll(item => initial.Contains(item.instanceId) && (survived || !item.permanent));
            int extractedValue = 0;
            if (survived)
            {
                foreach (ItemInstance item in loadout.items)
                {
                    if (item.permanent) Player.stash.RemoveAll(existing => existing.permanent && existing.definitionId == item.definitionId);
                    Player.stash.Add(UnityEngine.JsonUtility.FromJson<ItemInstance>(UnityEngine.JsonUtility.ToJson(item)));
                    if (!initial.Contains(item.instanceId)) extractedValue += ItemCatalog.Get(item.definitionId).price * System.Math.Max(1, item.quantity);
                }
                Player.vitals = UnityEngine.JsonUtility.FromJson<PlayerVitals>(UnityEngine.JsonUtility.ToJson(loadout.vitals));
                Player.statistics.survivedRaids++;
                Player.statistics.extractedValue += extractedValue;
                Player.statistics.bestRaidValue = System.Math.Max(Player.statistics.bestRaidValue, extractedValue);
            }
            else Player.statistics.deaths++;
            ItemCatalog.EnsurePermanentKnife(Player.stash);
            int xp = survived ? 900 + loadout.items.Count * 35 : 250;
            List<string> rewards = Player.AddExperience(xp);
            Save();
            string result = survived ? $"ЭВАКУАЦИЯ · ДОБЫЧА {extractedValue:N0} ₽ · +{xp} XP" : $"РЕЙД ПОКИНУТ · СНАРЯЖЕНИЕ ПОТЕРЯНО · +{xp} XP";
            if (rewards.Count > 0) result += " · " + string.Join(" · ", rewards);
            return result;
        }
    }
}
