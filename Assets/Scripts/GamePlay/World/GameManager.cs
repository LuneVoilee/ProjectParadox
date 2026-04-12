#region

using Sirenix.OdinInspector;
using Tool;

#endregion

namespace NewGamePlay
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
    }
}