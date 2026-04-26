#region

using System;
using Tool.ToolMath;

#endregion

namespace GamePlay.Strategy
{
    // 国家静态配置的字符串 Tag 包装。它不等同于运行时 byte id，适合跨存档/配置引用。
    [Serializable]
    public readonly struct NationTag : IEquatable<NationTag>
    {
        // 空 Tag 表示未指定国家；运行时占领逻辑仍应使用 NationId 的 NeutralId。
        public static readonly NationTag None = new(string.Empty);

        // Hash 用于快速比较；Name 保存标准化后的大写 Tag，便于调试和反查配置。
        public int Hash { get; }

        public string Name { get; }

        public NationTag(string tagName)
        {
            // 空白输入统一归一为 None，避免不同空字符串产生不同语义。
            if (string.IsNullOrWhiteSpace(tagName))
            {
                Name = string.Empty;
                Hash = 0;
                return;
            }

            // Tag 统一大写，保证 "fra" 和 "FRA" 在配置与运行时查询里含义一致。
            Name = tagName.Trim().ToUpperInvariant();
            Hash = ToolMath.GetHash(Name);
        }

        // 相等性只比较标准化 Tag 的 Hash，保留原项目 NationTag 的轻量比较方式。
        public bool Equals(NationTag other) => Hash == other.Hash;
        public override bool Equals(object obj) => obj is NationTag other && Equals(other);
        public override int GetHashCode() => Hash;
        public override string ToString() => Name;

        public static bool operator ==(NationTag left, NationTag right) => left.Equals(right);
        public static bool operator !=(NationTag left, NationTag right) => !left.Equals(right);

        public static implicit operator NationTag(string name) => new(name);
    }
}
