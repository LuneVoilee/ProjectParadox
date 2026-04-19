#region

using UnityEngine;

#endregion

namespace Tool.ToolMath
{
    public class ToolMath
    {
        public static int GetHash(string Name) => Animator.StringToHash(Name);
    }
}