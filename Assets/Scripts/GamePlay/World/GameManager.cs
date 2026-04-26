#region

using System.Collections.Generic;
using Common.Event;
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

        private void Start()
        {
            EventBus.GP_OnCreatePathIndicator?.Invoke(new List<Vector3>
            {
                new Vector3(20f, 50f, 70f),
                new Vector3(30f, 50f, 70f),
                new Vector3(40f, 60f, 70f),
                new Vector3(50f, 70f, 70f),
            });

            EventBus.GP_OnCreateSelectionIndicator?.Invoke();
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