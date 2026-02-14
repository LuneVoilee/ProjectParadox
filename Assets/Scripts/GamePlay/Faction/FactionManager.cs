using System.Collections.Generic;
using UnityEngine;
using Tool;
using Map.Components;
using Map.Common;
using Map.Manager;

namespace Faction
{
    public class FactionManager : SingletonMono<FactionManager>
    {
        [Header("Factions")]
        [SerializeField, Range(1, 255)] private int m_FactionCount = 6;
        [SerializeField] private Vector2Int m_TerritorySizeRange = new (30, 120);
        [SerializeField] private int m_Seed = 12345;
        [SerializeField] private bool m_GenerateOnMapReady = true;
        [SerializeField, Range(1, 2000)] private int m_StartSearchAttempts = 200;

        private static readonly HexCoordinates[] s_Directions =
        {
            new (1, -1, 0),
            new (1, 0, -1),
            new (0, 1, -1),
            new (-1, 1, 0),
            new (-1, 0, 1),
            new (0, -1, 1)
        };

        private readonly List<CFaction> m_Factions = new ();
        private MapManager m_MapManager;
        private bool m_HasGenerated;

        private void OnEnable()
        {
            m_MapManager = MapManager.Instance;
            if (m_MapManager != null)
            {
                m_MapManager.MapGenerated += HandleMapGenerated;
            }
        }

        private void OnDisable()
        {
            if (m_MapManager != null)
            {
                m_MapManager.MapGenerated -= HandleMapGenerated;
                m_MapManager = null;
            }
        }

        private void Start()
        {
            if (!m_GenerateOnMapReady || m_MapManager == null || m_HasGenerated)
            {
                return;
            }

            if (m_MapManager.CurrentData != null)
            {
                Generate(m_MapManager.CurrentData);
            }
        }

        private void HandleMapGenerated(GridData data)
        {
            if (!m_GenerateOnMapReady || m_HasGenerated)
            {
                return;
            }

            Generate(data);
        }

        public void Regenerate()
        {
            if (m_MapManager == null)
            {
                return;
            }

            m_HasGenerated = false;
            if (m_MapManager.CurrentData != null)
            {
                Generate(m_MapManager.CurrentData);
            }
        }

        private void Generate(GridData data)
        {
            if (data == null)
            {
                return;
            }

            m_HasGenerated = true;
            m_Factions.Clear();

            var borderRenderer = m_MapManager != null ? m_MapManager.TerritoryBorderRenderer : null;
            var territoryOwnership = new CTerritoryOwnership(data, borderRenderer);
            var random = new System.Random(m_Seed);

            int minSize = Mathf.Max(1, m_TerritorySizeRange.x);
            int maxSize = Mathf.Max(minSize, m_TerritorySizeRange.y);
            int factionCount = Mathf.Clamp(m_FactionCount, 1, 255);

            for (int i = 0; i < factionCount; i++)
            {
                byte id = (byte)(i + 1);
                var faction = new CFaction(id, $"Faction {id}", territoryOwnership);
                m_Factions.Add(faction);

                int targetSize = random.Next(minSize, maxSize + 1);
                GrowTerritory(faction, data, targetSize, random);
            }

            territoryOwnership.ApplyChanges();
        }

        private void GrowTerritory(CFaction faction, GridData data, int targetSize, System.Random random)
        {
            if (!TryFindStartCell(data, random, out var start))
            {
                return;
            }

            faction.TryAddTerritory(start);
            var frontier = new Queue<HexCoordinates>();
            frontier.Enqueue(start);

            var order = new int[s_Directions.Length];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            while (frontier.Count > 0 && faction.TerritoryCount < targetSize)
            {
                var current = frontier.Dequeue();
                Shuffle(order, random);

                for (int i = 0; i < order.Length; i++)
                {
                    var dir = s_Directions[order[i]];
                    var next = new HexCoordinates(current.X + dir.X, current.Y + dir.Y, current.Z + dir.Z);

                    if (faction.TryAddTerritory(next))
                    {
                        frontier.Enqueue(next);
                        if (faction.TerritoryCount >= targetSize)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private bool TryFindStartCell(GridData data, System.Random random, out HexCoordinates start)
        {
            int attempts = Mathf.Max(1, m_StartSearchAttempts);
            for (int i = 0; i < attempts; i++)
            {
                int col = random.Next(0, data.Width);
                int row = random.Next(0, data.Height);
                var cell = data.GetCell(col, row);
                if (cell != null && cell.OwnerId == 0)
                {
                    start = cell.Coordinates;
                    return true;
                }
            }

            for (int row = 0; row < data.Height; row++)
            {
                for (int col = 0; col < data.Width; col++)
                {
                    var cell = data.GetCell(col, row);
                    if (cell != null && cell.OwnerId == 0)
                    {
                        start = cell.Coordinates;
                        return true;
                    }
                }
            }

            start = default;
            return false;
        }

        private static void Shuffle(int[] order, System.Random random)
        {
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }
    }
}
