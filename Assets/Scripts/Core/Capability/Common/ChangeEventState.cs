using System;

namespace Core.Capability
{
    public static class ChangeEventState
    {
        [Flags]
        public enum EventMask
        {
            Add = 1,
            Remove = 2,
            AddRemove = Add | Remove,
            Update = 4,
            AddUpdate = Add | Update,
            RemoveUpdate = Remove | Update,
            AddRemoveUpdate = Add | Remove | Update
        }
    }
}
