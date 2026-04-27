#region

using Core.Capability;
using GamePlay.Strategy;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;
using Random = System.Random;

#endregion

namespace GamePlay.Map
{
    // 负责无缝地图数据生成。
    public class GenerateMapDataCap : CapabilityBase
    {
        private static readonly int m_MapId = Component<Map>.TId;
        private static readonly int m_NoiseId = Component<Noise>.TId;
        private static readonly int m_BiomeId = Component<Biome>.TId;

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.ScenarioMapGenerate;

        protected override void OnInit()
        {
            Filter(m_MapId, m_NoiseId, m_BiomeId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_MapId) &&
                   Owner.HasComponent(m_NoiseId) &&
                   Owner.HasComponent(m_BiomeId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        protected override void OnActivated()
        {
            if (!Owner.TryGetMap(out var map) ||
                !Owner.TryGetNoise(out var noise) ||
                !Owner.TryGetBiome(out var biome))
            {
                Debug.LogError("MapGenerateCap: Missing required components.");
                return;
            }

            var width = Mathf.Max(1, map.Width);
            var height = Mathf.Max(1, map.Height);

            if (map.EnableSeamlessX && (width & 1) == 1)
            {
                width += 1;
            }


            var grid = Owner.AddComponent<Grid>();

            grid.Cells = new Cell[width * height];
            grid.Width = width;
            grid.Height = height;
            grid.EnableSeamlessX = map.EnableSeamlessX;
            grid.EnableSeamlessY = map.EnableSeamlessY;

            var random = new Random(noise.Seed);

            var heightNoise = CreateNoiseSettings(noise.HeightScale, random, noise, map);

            //避免在内层循环做高频的乘法和加法
            int index = 0;

            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    //获取该数组元素内存地址的直接引用
                    ref var cell = ref grid.Cells[index];

                    cell.Coordinates = HexCoordinates.FromOffset(col, row);

                    var hexHeight = Sample(col, row, width, height, heightNoise);
                    cell.Height = hexHeight;

                    cell.TerrainType = Resolve(hexHeight, biome.SeaLevel, biome.MountainLevel);

                    index++;
                }
            }

            if (Owner.TryGetDrawMap(out var drawMap))
            {
                drawMap.IsDirty = true;
            }

            Owner.RemoveComponent(m_MapId);
            Owner.RemoveComponent(m_NoiseId);
            Owner.RemoveComponent(m_BiomeId);
        }

        private static NoiseParam CreateNoiseSettings
        (
            float targetScale, Random random, Noise noise,
            Map map
        )
        {
            return new NoiseParam
            {
                Scale = Mathf.Max(noise.MinNoiseScale, targetScale),
                OffsetX = random.Next(noise.MinOffset, noise.MaxOffset),
                OffsetY = random.Next(noise.MinOffset, noise.MaxOffset),
                SeamlessX = map.EnableSeamlessX,
                SeamlessY = map.EnableSeamlessY
            };
        }

        private float Sample
        (
            int col, int row, int width,
            int height, NoiseParam param
        )
        {
            var nx = (col + param.OffsetX) * param.Scale;
            var ny = (row + param.OffsetY) * param.Scale;
            var seamlessX = param.SeamlessX && width > 1;
            var seamlessY = param.SeamlessY && height > 1;

            if (!seamlessX && !seamlessY)
            {
                return Mathf.PerlinNoise(nx, ny);
            }

            var periodX = seamlessX ? width - 1 : width;
            var periodY = seamlessY ? height - 1 : height;

            if (seamlessX && seamlessY)
            {
                var tx = periodX > 0 ? col / (float)periodX : 0f;
                var ty1 = periodY > 0 ? row / (float)periodY : 0f;
                var px = periodX * param.Scale;
                var py1 = periodY * param.Scale;

                var a = Mathf.PerlinNoise(nx, ny);
                var b = Mathf.PerlinNoise(nx - px, ny);
                var c = Mathf.PerlinNoise(nx, ny - py1);
                var d = Mathf.PerlinNoise(nx - px, ny - py1);
                var ab = Mathf.Lerp(a, b, tx);
                var cd = Mathf.Lerp(c, d, tx);
                return Mathf.Lerp(ab, cd, ty1);
            }

            if (seamlessX)
            {
                var tx = periodX > 0 ? col / (float)periodX : 0f;
                var px = periodX * param.Scale;
                var a = Mathf.PerlinNoise(nx, ny);
                var b = Mathf.PerlinNoise(nx - px, ny);
                return Mathf.Lerp(a, b, tx);
            }

            var ty = periodY > 0 ? row / (float)periodY : 0f;
            var py = periodY * param.Scale;
            var ay = Mathf.PerlinNoise(nx, ny);
            var by = Mathf.PerlinNoise(nx, ny - py);
            return Mathf.Lerp(ay, by, ty);
        }

        private static TerrainType Resolve(float height, float seaLevel, float mountainLevel)
        {
            if (height < seaLevel)
            {
                return TerrainType.Ocean;
            }

            if (height > mountainLevel)
            {
                return TerrainType.Mountain;
            }

            return TerrainType.Plain;
        }
    }

    // 领土变更应用 Cap：把玩法侧提交的占领请求写入 Grid.Cells，并维护地图填色脏状态。
    public class ApplyTerritoryChangesCap : CapabilityBase
    {
        // 同时依赖格子、请求缓冲、绘制脏状态和国家索引，缺任一项都不应进入结算。
        private static readonly int m_GridId = Component<Grid>.TId;

        private static readonly int m_TerritoryOwnershipBufferId =
            Component<TerritoryOwnershipBuffer>.TId;

        private static readonly int m_TerritoryPaintStateId = Component<TerritoryPaintState>.TId;
        private static readonly int m_NationIndexId = Component<NationIndex>.TId;

        // 放在规则结算阶段，晚于 OccupyCap 写请求，早于表现阶段的 DrawMapCap 读脏格。
        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.StageResolve + 20;

        protected override void OnInit()
        {
            Filter(m_GridId, m_TerritoryOwnershipBufferId, m_TerritoryPaintStateId,
                m_NationIndexId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_GridId) &&
                   Owner.HasComponent(m_TerritoryOwnershipBufferId) &&
                   Owner.HasComponent(m_TerritoryPaintStateId) &&
                   Owner.HasComponent(m_NationIndexId);
        }

        public override bool ShouldDeactivate()
        {
            return !ShouldActivate();
        }

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            // Tick 时重新取组件，避免运行中组件被移除后继续持有旧引用。
            if (!Owner.TryGetGrid(out var grid) ||
                !Owner.TryGetTerritoryOwnershipBuffer(out var ownershipBuffer) ||
                !Owner.TryGetTerritoryPaintState(out var paintState) ||
                !Owner.TryGetNationIndex(out var nationIndex))
            {
                return;
            }

            if (grid.Cells == null || grid.Cells.Length == 0)
            {
                // 地图尚未生成或被清理时，丢弃积压请求，避免之后用旧 cellIndex 写入新地图。
                Clear(ownershipBuffer);
                return;
            }

            // 把“同格最后一次写入”的字典快照转换成可遍历列表，本帧只消费这批请求。
            BuildChanges(ownershipBuffer);
            if (ownershipBuffer.Changes.Count == 0)
            {
                return;
            }

            int changedCount = 0;
            for (int i = 0; i < ownershipBuffer.Changes.Count; i++)
            {
                // 过滤越界 cellIndex，防止单位控制区数据和当前地图尺寸不一致时写坏数组。
                TerritoryOwnershipBuffer.OwnershipChange change = ownershipBuffer.Changes[i];
                int cellIndex = change.CellIndex;
                if ((uint)cellIndex >= (uint)grid.Cells.Length)
                {
                    continue;
                }

                byte ownerId = change.NewOwnerId;
                // 未注册国家 id 不允许写进权威格子，统一回退到 Neutral。
                if (!NationRegistryCap.IsValidNationId(nationIndex, ownerId))
                {
                    ownerId = NationIndex.NeutralId;
                }

                ref Cell cell = ref grid.Cells[cellIndex];
                if (cell.OwnerId == ownerId)
                {
                    continue;
                }

                // 只有归属真正变化时才标脏，避免 DrawMapCap 做无意义颜色刷新。
                cell.OwnerId = ownerId;
                MarkDirty(paintState, cellIndex);
                changedCount++;
            }

            // Changes 是本帧消费缓存；LatestOwnerByCell 已在 BuildChanges 中清空。
            ownershipBuffer.Changes.Clear();
            if (changedCount <= 0)
            {
                return;
            }

            // dirty 太多时让表现层全图重刷，比维护大量散点更稳定。
            if (ShouldUseFullRepaint(paintState, grid.Cells.Length))
            {
                paintState.ColorDirtyAll = true;
                ClearDirty(paintState);
            }
        }

        private static void BuildChanges(TerritoryOwnershipBuffer ownershipBuffer)
        {
            // 用字典聚合后再转列表，保证同一格在同一帧多次占领只留下最终 owner。
            ownershipBuffer.Changes.Clear();
            foreach (var pair in ownershipBuffer.LatestOwnerByCell)
            {
                ownershipBuffer.Changes.Add(new TerritoryOwnershipBuffer.OwnershipChange
                {
                    CellIndex = pair.Key,
                    NewOwnerId = pair.Value
                });
            }

            ownershipBuffer.LatestOwnerByCell.Clear();
        }

        private static void Clear(TerritoryOwnershipBuffer ownershipBuffer)
        {
            // 同时清理生产缓冲和消费列表，恢复到无待处理请求状态。
            ownershipBuffer.LatestOwnerByCell.Clear();
            ownershipBuffer.Changes.Clear();
        }

        private static void MarkDirty(TerritoryPaintState paintState, int cellIndex)
        {
            // HashSet 负责去重，List 负责给 DrawMapCap 提供紧凑遍历。
            if (paintState.DirtyCellSet.Add(cellIndex))
            {
                paintState.DirtyCellIndices.Add(cellIndex);
            }
        }

        private static void ClearDirty(TerritoryPaintState paintState)
        {
            // 全图脏时不需要保存单格脏列表，避免 DrawMapCap 误走增量路径。
            paintState.DirtyCellSet.Clear();
            paintState.DirtyCellIndices.Clear();
        }

        private static bool ShouldUseFullRepaint(TerritoryPaintState paintState, int totalCellCount)
        {
            // 外部已经指定全图脏时直接全量刷新。
            if (paintState.ColorDirtyAll)
            {
                return true;
            }

            // 无有效格子时没有必要触发全量刷新。
            if (totalCellCount <= 0)
            {
                return false;
            }

            // 绝对数量阈值用于大地图保护，比例阈值用于小地图保护。
            if (paintState.DirtyCellIndices.Count >= paintState.DirtyToFullThresholdAbs)
            {
                return true;
            }

            float ratio = paintState.DirtyCellIndices.Count / (float)totalCellCount;
            return ratio >= paintState.DirtyToFullThresholdRatio;
        }
    }
}