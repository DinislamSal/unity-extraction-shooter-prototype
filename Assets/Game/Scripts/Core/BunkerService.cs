using System;
using System.Collections.Generic;

namespace OfflineExtraction.Core
{
    public static class BunkerService
    {
        public const double FuelCanHours = 12d;
        public static bool IsPowered(PlayerData player) => player.bunkerFuelUntilUtcTicks > DateTime.UtcNow.Ticks;
        public static float Efficiency(PlayerData player) => IsPowered(player) ? 1f : .5f;
        public static float ApplyEfficiency(PlayerData player, float moduleBonus) => moduleBonus * Efficiency(player);
        public static bool CanRepairWeapons(PlayerData player) => IsPowered(player) && GetLevel(player, "workbench") > 0;
        public static TimeSpan RemainingPower(PlayerData player)
            => IsPowered(player) ? new TimeSpan(player.bunkerFuelUntilUtcTicks - DateTime.UtcNow.Ticks) : TimeSpan.Zero;

        public static bool TryAddFuel(PlayerData player, out string message)
        {
            if (GetLevel(player, "generator") <= 0) { message = "Сначала постройте генератор"; return false; }
            if (Count(player.stash, "fuel_can") <= 0) { message = "В хранилище нет топлива"; return false; }
            Consume(player.stash, "fuel_can", 1);
            long start = Math.Max(DateTime.UtcNow.Ticks, player.bunkerFuelUntilUtcTicks);
            player.bunkerFuelUntilUtcTicks = start + TimeSpan.FromHours(FuelCanHours).Ticks;
            message = $"Генератор заправлен на {FuelCanHours:0} часов";
            return true;
        }

        public static int GetLevel(PlayerData player, string moduleId)
            => player.bunkerModules.Find(state => state.moduleId == moduleId)?.level ?? 0;

        public static bool TryUpgrade(PlayerData player, string moduleId, out string message)
        {
            BunkerModuleSO module = BunkerCatalog.Get(moduleId);
            if (module == null) { message = "Модуль не найден"; return false; }
            int level = GetLevel(player, moduleId);
            if (level >= module.MaxLevel) { message = "Достигнут максимальный уровень"; return false; }
            BunkerLevelData next = module.levels[level];
            foreach (BunkerRequirement requirement in next.requirements)
                if (Count(player.stash, requirement.itemId) < requirement.quantity)
                { message = "Недостаточно ресурсов"; return false; }

            foreach (BunkerRequirement requirement in next.requirements) Consume(player.stash, requirement.itemId, requirement.quantity);
            BunkerModuleState state = player.bunkerModules.Find(value => value.moduleId == moduleId);
            if (state == null) { state = new BunkerModuleState { moduleId = moduleId }; player.bunkerModules.Add(state); }
            state.level++;
            message = $"{module.displayName}: уровень {state.level} построен";
            return true;
        }

        public static int Count(List<ItemInstance> stash, string itemId)
        {
            int total = 0;
            foreach (ItemInstance item in stash) if (item.definitionId == itemId) total += item.quantity;
            return total;
        }

        private static void Consume(List<ItemInstance> stash, string itemId, int quantity)
        {
            for (int i = stash.Count - 1; i >= 0 && quantity > 0; i--)
            {
                ItemInstance item = stash[i];
                if (item.definitionId != itemId) continue;
                int take = System.Math.Min(quantity, item.quantity);
                item.quantity -= take; quantity -= take;
                if (item.quantity <= 0) stash.RemoveAt(i);
            }
        }
    }
}
