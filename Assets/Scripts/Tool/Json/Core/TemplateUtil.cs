using System;
using System.Collections.Generic;
using System.Reflection;
using Tool;

namespace Tool.Json.GameFramework
{
    internal interface IJsonTemplateSet
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class TemplatePreloadOrderAttribute : Attribute
    {
        public readonly int Order;

        public TemplatePreloadOrderAttribute(int order)
        {
            Order = order;
        }
    }

    public static class KTemplateUtil
    {
        public static void UnloadAll()
        {
            foreach (Type type in Utility.Reflection.AllTypes(typeof(IJsonTemplateSet)))
            {
                if (type.ContainsGenericParameters)
                {
                    continue;
                }

                MethodInfo method = type.GetMethod("Unload",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (method == null)
                {
                    method = type.GetMethod("UnLoad",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                }

                if (method == null)
                {
                    continue;
                }

                try
                {
                    method.Invoke(null, null);
                }
                catch (Exception e)
                {
                    Log.Exception(e);
                }
            }
        }

        public static void PreloadAll()
        {
            var all = new List<(int, Type)>();
            foreach (Type type in Utility.Reflection.AllTypes(typeof(IJsonTemplateSet)))
            {
                if (type.ContainsGenericParameters)
                {
                    continue;
                }

                TemplatePreloadOrderAttribute attr = type.GetCustomAttribute<TemplatePreloadOrderAttribute>();
                all.Add(attr != null ? (attr.Order, type) : (0, type));
            }

            all.Sort((x, y) => x.Item1.CompareTo(y.Item1));

            foreach ((int _, Type type) in all)
            {
                MethodInfo method = type.GetMethod("Preload",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (method == null)
                {
                    continue;
                }

                try
                {
                    method.Invoke(null, null);
                }
                catch (Exception e)
                {
                    Log.Exception(e);
                }
            }
        }

        public static bool IsPreloading => PreloadingQueue.IsProcessing;

        public static void LoadAll()
        {
            foreach (Type type in Utility.Reflection.AllTypes(typeof(IJsonTemplateSet)))
            {
                if (type.ContainsGenericParameters)
                {
                    continue;
                }

                PropertyInfo property = type.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (property == null || !property.CanRead)
                {
                    continue;
                }

                try
                {
                    property.GetValue(null);
                }
                catch (Exception e)
                {
                    Log.Exception(e);
                }
            }
        }
    }
}
