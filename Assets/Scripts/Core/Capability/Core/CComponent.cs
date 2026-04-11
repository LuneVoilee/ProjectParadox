using System;

namespace Core.Capability
{
    [Serializable]
    public class CComponent : IDisposable
    {
        public CEntity Owner { get; internal set; }

        public virtual void Dispose()
        {
        }
    }
}
