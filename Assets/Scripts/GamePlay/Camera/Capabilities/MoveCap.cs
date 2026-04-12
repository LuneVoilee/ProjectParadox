#region

using Core;
using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class MoveCap : CapabilityBase
    {
        private static readonly int m_RefId = Component<Ref>.TId;
        private static readonly int m_MoveId = Component<Move>.TId;

        protected override void OnInit()
        {
            Filter(m_RefId, m_MoveId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(m_RefId) &&
                   Owner.HasComponent(m_MoveId);
        }

        public override bool ShouldDeactivate() => !ShouldActivate();

        public override void TickActive(float deltaTime, float realElapsedSeconds)
        {
            var inputManager = InputManager.Instance;
            var input = inputManager != null ? inputManager.MoveInput : Vector2.zero;
            if (input == Vector2.zero)
            {
                return;
            }

            if (!Owner.TryGetComponent<Move>(m_MoveId, out var moveComp) ||
                !Owner.TryGetComponent<Ref>(m_RefId, out var refComp) ||
                refComp.Target == null)
            {
                return;
            }

            var delta = new Vector3(input.x, input.y, 0f) * (moveComp.MoveSpeed * deltaTime);
            refComp.Target.position += delta;
        }
    }
}
