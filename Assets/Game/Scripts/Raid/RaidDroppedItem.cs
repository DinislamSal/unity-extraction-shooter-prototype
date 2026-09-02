using System.Collections.Generic;
using OfflineExtraction.Core;
using UnityEngine;

namespace OfflineExtraction.Raid
{
    public sealed class RaidDroppedItem : MonoBehaviour
    {
        public ItemInstance item;
        private readonly List<LineRenderer> outline = new();

        private void Awake()
        {
            Vector3[] points =
            {
                new(-.51f,-.51f,-.51f), new(.51f,-.51f,-.51f), new(.51f,-.51f,.51f), new(-.51f,-.51f,.51f),
                new(-.51f,.51f,-.51f), new(.51f,.51f,-.51f), new(.51f,.51f,.51f), new(-.51f,.51f,.51f)
            };
            int[,] edges = { {0,1},{1,2},{2,3},{3,0},{4,5},{5,6},{6,7},{7,4},{0,4},{1,5},{2,6},{3,7} };
            Material material = new(Shader.Find("Sprites/Default")) { color = Color.white };
            for (int i = 0; i < 12; i++)
            {
                GameObject edge = new($"Item outline {i}"); edge.transform.SetParent(transform, false);
                LineRenderer line = edge.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 2;
                line.SetPosition(0, points[edges[i, 0]]); line.SetPosition(1, points[edges[i, 1]]);
                line.startWidth = line.endWidth = .025f; line.material = material; line.enabled = false; outline.Add(line);
            }
        }

        public void SetHighlighted(bool value)
        {
            foreach (LineRenderer line in outline) line.enabled = value;
        }
    }
}
