using System;
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace Core.Capability
{
    public class EntityMatcher : IEquatable<EntityMatcher>
    {
        public int[] AllOfComponentIds { get; private set; }
        public int[] AnyOfComponentIds { get; private set; }
        public int[] NoneOfComponentIds { get; private set; }
        public int[] Indices { get; private set; }

        private int m_HashCode;

        public static EntityMatcher SetAll(params int[] allOfComponentIds)
        {
            EntityMatcher matcher = new EntityMatcher
            {
                AllOfComponentIds = allOfComponentIds
            };
            
            if (matcher.AllOfComponentIds != null)
            {
                Array.Sort(matcher.AllOfComponentIds);
            }
            matcher.RebuildIndexAndHash();
            return matcher;
        }

        public EntityMatcher SetAny(params int[] anyOfComponentIds)
        {
            AnyOfComponentIds = anyOfComponentIds;
            if (AnyOfComponentIds != null)
            {
                Array.Sort(AnyOfComponentIds);
            }
            RebuildIndexAndHash();
            return this;
        }

        public EntityMatcher SetNone(params int[] noneOfComponentIds)
        {
            NoneOfComponentIds = noneOfComponentIds;
            if (NoneOfComponentIds != null)
            {
                Array.Sort(NoneOfComponentIds);
            }
            RebuildIndexAndHash();
            return this;
        }

        public bool Match(CEntity entity)
        {
            if (AllOfComponentIds != null && !entity.HasComponents(AllOfComponentIds))
            {
                return false;
            }

            if (AnyOfComponentIds != null && !entity.HasAnyComponent(AnyOfComponentIds))
            {
                return false;
            }

            if (NoneOfComponentIds != null && entity.HasAnyComponent(NoneOfComponentIds))
            {
                return false;
            }

            return true;
        }

        private void RebuildIndexAndHash()
        {
            int allLength = AllOfComponentIds?.Length ?? 0;
            int anyLength = AnyOfComponentIds?.Length ?? 0;
            int noneLength = NoneOfComponentIds?.Length ?? 0;

            Indices = new int[allLength + anyLength + noneLength];
            int offset = 0;
            if (allLength > 0)
            {
                Array.Copy(AllOfComponentIds, 0, Indices, offset, allLength);
                offset += allLength;
            }

            if (anyLength > 0)
            {
                Array.Copy(AnyOfComponentIds, 0, Indices, offset, anyLength);
                offset += anyLength;
            }

            if (noneLength > 0)
            {
                Array.Copy(NoneOfComponentIds, 0, Indices, offset, noneLength);
            }

            Array.Sort(Indices);

            unchecked
            {
                int hash = 17;
                hash = HashArray(hash, 31, AllOfComponentIds);
                hash = HashArray(hash, 37, AnyOfComponentIds);
                hash = HashArray(hash, 41, NoneOfComponentIds);
                m_HashCode = hash;
            }
        }

        public bool Equals(EntityMatcher other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || m_HashCode != other.m_HashCode)
            {
                return false;
            }

            return ArrayEquals(AllOfComponentIds, other.AllOfComponentIds)
                && ArrayEquals(AnyOfComponentIds, other.AnyOfComponentIds)
                && ArrayEquals(NoneOfComponentIds, other.NoneOfComponentIds);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EntityMatcher);
        }

        public override int GetHashCode()
        {
            return m_HashCode;
        }

        private static int HashArray(int seed, int factor, int[] array)
        {
            if (array == null || array.Length == 0)
            {
                return seed;
            }

            unchecked
            {
                for (int i = 0; i < array.Length; i++)
                {
                    seed = seed * factor + array[i];
                }
            }

            return seed;
        }

        private static bool ArrayEquals(int[] a, int[] b)
        {
            int la = a?.Length ?? 0;
            int lb = b?.Length ?? 0;
            if (la != lb)
            {
                return false;
            }

            for (int i = 0; i < la; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
