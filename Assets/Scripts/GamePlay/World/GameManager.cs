#region

using Sirenix.OdinInspector;
using Tool;
using UnityEngine;

#endregion

namespace GamePlay.World
{
    public class GameManager : SingletonMono<GameManager>
    {
        public GameWorld World = new GameWorld();

        [LabelText("最大组件数量")] public int MaxComponentCount = 512;

        protected override void Awake()
        {
            base.Awake();
            World.OnInitialize(MaxComponentCount);
        }

        private void Update()
        {
            World?.OnUpdate(Time.deltaTime, Time.realtimeSinceStartup);
        }

        private void FixedUpdate()
        {
            World?.OnFixedUpdate(Time.fixedDeltaTime, Time.realtimeSinceStartup);
        }
    }
}