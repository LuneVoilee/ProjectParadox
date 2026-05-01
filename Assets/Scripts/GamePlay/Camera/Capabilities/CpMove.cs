#region

using Core;
using Core.Capability;
using GamePlay.Util;
using GamePlay.World;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class CpMove : CapabilityBase
    {
        private readonly System.Collections.Generic.List<CEntity> m_Entities =
            new System.Collections.Generic.List<CEntity>(4);

        public override int TickGroupOrder { get; protected set; } =
            CapabilityOrder.PresentationCameraMove;

        public override string Pipeline => CapabilityPipeline.Camera;

        public override void Tick
            (CapabilityContext context, float deltaTime, float realElapsedSeconds)
        {
            var inputManager = InputManager.Instance;
            var input = inputManager != null ? inputManager.MoveInput : Vector2.zero;
            if (input == Vector2.zero)
            {
                return;
            }

            context.QuerySnapshot<Ref, Move>(m_Entities);
            for (int i = 0; i < m_Entities.Count; i++)
            {
                CEntity entity = m_Entities[i];
                if (!entity.TryGetMove(out var moveComp)) continue;
                if (!entity.TryGetRef(out var refComp)) continue;
                if (refComp.Target == null) continue;

                var delta = new Vector3(input.x, input.y, 0f) *
                            (moveComp.MoveSpeed * deltaTime);
                refComp.Target.position += delta;
                context.MarkWorked();
            }
        }
    }
}
