#region

using System;
using Tool.ToolMath;

#endregion

namespace GamePlay.Strategy
{
    namespace Core.Capability.Tags
    {
        [Serializable]
        public readonly struct NationTag : IEquatable<NationTag>
        {
            public static readonly NationTag None = new(string.Empty);

            public int Hash { get; }

            public string Name { get; }

            public NationTag(string tagName)
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    Name = string.Empty;
                    Hash = 0;
                }
                else
                {
                    // 强制大写，如 GER, FRA
                    Name = tagName.ToUpperInvariant();
                    Hash = ToolMath.GetHash(Name);
                }
            }

            public bool Equals(NationTag other) => Hash == other.Hash;
            public override bool Equals(object obj) => obj is NationTag other && Equals(other);
            public override int GetHashCode() => Hash;
            public override string ToString() => Name;

            public static bool operator ==
                (NationTag left, NationTag right) => left.Equals(right);

            public static bool operator !=
                (NationTag left, NationTag right) => !left.Equals(right);

            // 隐式转换，方便写代码时直接传字符串
            public static implicit operator NationTag(string name) => new(name);
        }
    }
}