#region

using Core;
using Core.Capability;
using UnityEngine;

#endregion

namespace GamePlay.Camera
{
    public class MoveCap : CapabilityBase
    {
        protected override void OnInit()
        {
            Filter(Component<Ref>.TId, Component<Move>.TId);
        }

        public override bool ShouldActivate()
        {
            return Owner.HasComponent(Component<Ref>.TId) &&
                   Owner.HasComponent(Component<Move>.TId);
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

            var moveComp = Owner.GetComponent(Component<Move>.TId) as Move;
            var refComp = Owner.GetComponent(Component<Ref>.TId) as Ref;

            if (moveComp == null || refComp == null)
            {
                return;
            }

            if (refComp.Target == null)
            {
                return;
            }

            var delta = new Vector3(input.x, input.y, 0f) * (moveComp.MoveSpeed * deltaTime);
            refComp.Target.position += delta;
        }
    }
}