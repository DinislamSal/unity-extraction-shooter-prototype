using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfflineExtraction.Core
{
    public static class BunkerCatalog
    {
        private static Dictionary<string, BunkerModuleSO> modules;
        public static IReadOnlyCollection<BunkerModuleSO> All { get { Load(); return modules.Values; } }

        public static BunkerModuleSO Get(string id)
        {
            Load();
            return modules.TryGetValue(id, out BunkerModuleSO module) ? module : null;
        }

        private static void Load()
        {
            if (modules != null) return;
            modules = new Dictionary<string, BunkerModuleSO>(StringComparer.Ordinal);
            foreach (BunkerModuleSO module in Resources.LoadAll<BunkerModuleSO>("BunkerModules"))
                if (module != null && !string.IsNullOrWhiteSpace(module.id)) modules[module.id] = module;
        }
    }
}
