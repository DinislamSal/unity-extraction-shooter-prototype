using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfflineExtraction.Core
{
    [Serializable]
    public sealed class BunkerRequirement
    {
        public string itemId;
        [Min(1)] public int quantity = 1;
    }

    [Serializable]
    public sealed class BunkerLevelData
    {
        public string bonusDescription;
        public List<BunkerRequirement> requirements = new();
    }

    [CreateAssetMenu(fileName = "BunkerModule", menuName = "Offline Extraction/Bunker Module")]
    public sealed class BunkerModuleSO : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public List<BunkerLevelData> levels = new();
        public int MaxLevel => levels?.Count ?? 0;
    }

    [Serializable]
    public sealed class BunkerModuleState
    {
        public string moduleId;
        public int level;
    }
}
