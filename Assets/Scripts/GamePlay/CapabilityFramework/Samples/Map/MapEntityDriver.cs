using Map.Settings;
using Map.View;
using UnityEngine;

namespace GamePlay.CapabilityFramework.Samples.Map
{
    /// <summary>
    /// Map Entity 的 Unity 桥接驱动器（示例 MonoBehaviour）。
    ///
    /// 作用：
    /// - 在 Unity 生命周期里创建/持有一个 Entity；
    /// - 把 Inspector 配置写入 MapRuntimeState；
    /// - 每帧或每回合驱动 Scheduler；
    /// - 在 RenderSync 阶段把 State 同步到 Renderer。
    ///
    /// 说明：
    /// - 这是教学版最小样例，先求可读性；
    /// - 之后你可以把阶段驱动迁移到全局 TurnManager。
    /// </summary>
    public class MapEntityDriver : MonoBehaviour
    {
        [Header("Map Size")]
        [SerializeField] private int m_Width = 100;
        [SerializeField] private int m_Height = 100;

        [Header("Noise")]
        [SerializeField] private int m_Seed;
        [SerializeField] private float m_HeightScale = 0.08f;
        [SerializeField] private float m_MoistureScale = 0.12f;

        [Header("Wrap")]
        [SerializeField] private bool m_SeamlessX = true;
        [SerializeField] private bool m_SeamlessY;

        [Header("Settings")]
        [SerializeField] private BiomeSettings m_BiomeSettings;

        [Header("Links")]
        [SerializeField] private HexMapRenderer m_HexMapRenderer;
        [SerializeField] private TerritoryBorderRenderer m_TerritoryBorderRenderer;

        [Header("Debug")]
        [SerializeField] private bool m_GenerateOnStart = true;
        [SerializeField] private bool m_DrivePhasesEveryFrame = true;

        private Entity m_Entity;
        private bool m_ViewDirty;

        private void Awake()
        {
            BuildEntity();
        }

        private void Start()
        {
            if (m_GenerateOnStart)
            {
                RequestGenerateMap(m_Seed);
                RunOneTurnPipeline(Time.deltaTime);
            }
        }

        private void Update()
        {
            if (!m_DrivePhasesEveryFrame || m_Entity == null)
            {
                return;
            }

            // Always：可放持续逻辑。
            m_Entity.Tick(CapabilityPhase.Always, Time.deltaTime);

            // 教学示例：每帧完整驱动一次“回合流水线”。
            // 实际项目建议改为：由 TurnManager 在回合边界调用。
            RunOneTurnPipeline(Time.deltaTime);
        }

        private void OnDestroy()
        {
            m_Entity?.Clear();
            m_Entity = null;
        }

        /// <summary>
        /// 外部调用：请求地图生成。
        /// </summary>
        public void RequestGenerateMap(int seed)
        {
            if (m_Entity == null)
            {
                return;
            }

            m_Entity.Requests.Push(new GenerateMapRequest { Seed = seed });
        }

        private void BuildEntity()
        {
            m_Entity = new Entity();

            var state = new MapRuntimeState
            {
                Width = m_Width,
                Height = m_Height,
                Seed = m_Seed,
                HeightScale = m_HeightScale,
                MoistureScale = m_MoistureScale,
                SeamlessX = m_SeamlessX,
                SeamlessY = m_SeamlessY,
                BiomeSettings = m_BiomeSettings,
                HexMapRenderer = m_HexMapRenderer,
                TerritoryBorderRenderer = m_TerritoryBorderRenderer
            };

            m_Entity.SetState(state);

            // 注册能力。
            m_Entity.AddCapability(new GenerateMapCapability());
            m_Entity.AddCapability(new RebuildFactionsCapability());

            // 示例：监听消息（也可以给别的 Capability 订阅）。
            m_Entity.Messages.Subscribe<MapGeneratedMessage>(OnMapGeneratedMessage);
        }

        private void RunOneTurnPipeline(float deltaTime)
        {
            if (m_Entity == null)
            {
                return;
            }

            m_Entity.Tick(CapabilityPhase.PreTurn, deltaTime);
            m_Entity.Tick(CapabilityPhase.TurnLogic, deltaTime);
            m_Entity.Tick(CapabilityPhase.Resolve, deltaTime);
            m_Entity.Tick(CapabilityPhase.PostTurn, deltaTime);
            m_Entity.Tick(CapabilityPhase.RenderSync, deltaTime);

            // 视图同步在 Driver 做（Unity 桥接层职责）。
            SyncView();
        }

        private void SyncView()
        {
            if (!m_ViewDirty)
            {
                return;
            }

            var state = m_Entity.GetState<MapRuntimeState>();
            if (state.Grid == null)
            {
                return;
            }

            if (state.HexMapRenderer != null)
            {
                state.HexMapRenderer.Render(state.Grid);
            }

            if (state.TerritoryBorderRenderer != null)
            {
                state.TerritoryBorderRenderer.Render(state.Grid);
            }

            m_ViewDirty = false;
        }

        private void OnMapGeneratedMessage(MapGeneratedMessage message)
        {
            // 地图变化后才触发一次视图同步，避免每帧重绘。
            m_ViewDirty = true;

            // 你也可以在这里接日志、统计、任务系统。
            _ = message;
        }
    }
}
