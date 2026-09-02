using System;
using System.Collections.Generic;
using OfflineExtraction.Core;
using UnityEngine;

namespace OfflineExtraction.Raid
{
    public sealed class RaidLootContainer : MonoBehaviour
    {
        public string displayName = "ТЕХНИЧЕСКИЙ ЯЩИК";
        public int columns = 6;
        public int rows = 6;
        public int revealedCount;
        public float revealTimer;
        public bool isCorpse;
        public List<ItemInstance> items = new();
        private readonly List<LineRenderer> outline = new();

        private void Awake() => CreateOutline();

        public void SetHighlighted(bool highlighted)
        {
            foreach (LineRenderer line in outline) line.enabled = highlighted;
        }

        private void CreateOutline()
        {
            Vector3[] p =
            {
                new(-.51f,-.51f,-.51f), new(.51f,-.51f,-.51f), new(.51f,-.51f,.51f), new(-.51f,-.51f,.51f),
                new(-.51f,.51f,-.51f), new(.51f,.51f,-.51f), new(.51f,.51f,.51f), new(-.51f,.51f,.51f)
            };
            int[,] edges = { {0,1},{1,2},{2,3},{3,0},{4,5},{5,6},{6,7},{7,4},{0,4},{1,5},{2,6},{3,7} };
            Material material = new(Shader.Find("Sprites/Default")); material.color = Color.white;
            for (int i = 0; i < 12; i++)
            {
                GameObject edge = new($"Outline {i}"); edge.transform.SetParent(transform, false);
                LineRenderer line = edge.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 2;
                line.SetPosition(0, p[edges[i,0]]); line.SetPosition(1, p[edges[i,1]]); line.startWidth = line.endWidth = .018f;
                line.material = material; line.enabled = false; outline.Add(line);
            }
        }

        public void FillTestLoot()
        {
            if (items.Count > 0) return;
            items.Add(ItemInstance.Create("fuel_can", 0, 0));
            items.Add(ItemInstance.Create("electronics", 3, 0));
            items.Add(ItemInstance.Create("scrap_metal", 4, 0));
            items.Add(ItemInstance.Create("bandage", 5, 0));
            items.Add(ItemInstance.Create("mod_optic", 0, 3));
            foreach (ItemInstance item in items) item.instanceId = Guid.NewGuid().ToString("N");
            CompactItems();
        }

        public void FillCorpseLoot()
        {
            if (items.Count > 0) return;
            isCorpse = true;
            displayName = "ТЕЛО БОЙЦА";
            columns = 7; rows = 7;
            ItemInstance rifle = ItemInstance.Create("rifle_mk1"); rifle.condition = UnityEngine.Random.Range(45, 91); items.Add(rifle);
            ItemInstance magazine = ItemInstance.Create("mod_magazine"); magazine.loadedAmmoDefinitionId = "ammo_556"; magazine.loadedAmmoCount = UnityEngine.Random.Range(8, 31); items.Add(magazine);
            items.Add(ItemInstance.Create("ammo_556", quantity: UnityEngine.Random.Range(12, 41)));
            items.Add(ItemInstance.Create("bandage", quantity: UnityEngine.Random.Range(1, 3)));
            ItemInstance armor = ItemInstance.Create("armor_t3"); armor.condition = UnityEngine.Random.Range(25, 76); items.Add(armor);
            items.Add(ItemInstance.Create("rig_16"));
            foreach (ItemInstance item in items) item.instanceId = Guid.NewGuid().ToString("N");
            CompactItems();
            SetOutlineBounds(new Vector3(0f, .9f, 0f), new Vector3(.9f, 1.9f, .9f));
        }

        public void FillCorpseLoot(IEnumerable<ItemInstance> actualItems)
        {
            items.Clear();
            isCorpse = true;
            displayName = "ТЕЛО БОЙЦА";
            columns = 7; rows = 7;
            if (actualItems != null)
                foreach (ItemInstance item in actualItems)
                    if (item != null) { item.parentContainerId = null; item.equippedSlot = null; items.Add(item); }
            CompactItems();
            SetOutlineBounds(new Vector3(0f, .9f, 0f), new Vector3(.9f, 1.9f, .9f));
        }

        private void SetOutlineBounds(Vector3 center, Vector3 size)
        {
            Vector3 half = size * .5f;
            Vector3[] p =
            {
                center + new Vector3(-half.x,-half.y,-half.z), center + new Vector3(half.x,-half.y,-half.z), center + new Vector3(half.x,-half.y,half.z), center + new Vector3(-half.x,-half.y,half.z),
                center + new Vector3(-half.x,half.y,-half.z), center + new Vector3(half.x,half.y,-half.z), center + new Vector3(half.x,half.y,half.z), center + new Vector3(-half.x,half.y,half.z)
            };
            int[,] edges = { {0,1},{1,2},{2,3},{3,0},{4,5},{5,6},{6,7},{7,4},{0,4},{1,5},{2,6},{3,7} };
            for (int i = 0; i < outline.Count && i < 12; i++) { outline[i].SetPosition(0, p[edges[i,0]]); outline[i].SetPosition(1, p[edges[i,1]]); }
        }

        private void CompactItems()
        {
            var placed = new List<RectInt>();
            foreach (ItemInstance item in items)
            {
                ItemCatalog.GetSize(item, out int width, out int height);
                for (int y = 0; y <= rows - height; y++)
                for (int x = 0; x <= columns - width; x++)
                {
                    RectInt candidate = new(x, y, width, height);
                    if (placed.Exists(rect => rect.Overlaps(candidate))) continue;
                    item.x = x; item.y = y; placed.Add(candidate); y = rows; break;
                }
            }
        }
    }
}
