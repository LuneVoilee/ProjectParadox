#region

using System;
using System.Collections.Generic;
using System.Reflection;

#endregion

namespace Tool
{
    /// <summary>
    ///     所有和反射相关的机制放在这里
    /// </summary>
    public static partial class Utility
    {
        #region Constructor

        private static readonly Dictionary<Type, ConstructorInfo[]> ms_Constructor = new();

        public static partial class Reflection
        {
            /// <summary>
            ///     创建指定对象
            /// </summary>
            public static T CreateInstance<T>() where T : class
            {
                return CreateInstance(typeof(T)) as T;
            }

            /// <summary>
            ///     创建指定对象
            /// </summary>
            public static object CreateInstance(Type type, object[] args = null)
            {
                // 获取构造函数
                if (!ms_Constructor.TryGetValue(type, out var ctorList))
                {
                    ctorList = type.GetConstructors(
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    ms_Constructor.Add(type, ctorList);
                }


                // 获取匹配的构造函数
                if (args == null || args.Length == 0)
                {
                    // 默认构造函数
                    foreach (ConstructorInfo ctor in ctorList)
                    {
                        var infos = ctor.GetParameters();
                        if (infos.Length == 0)
                            return ctor.Invoke(null);
                    }

                    throw new Exception($"[{type.Name}] 找不到默认构造函数");
                }

                // 其它构造函数
                foreach (ConstructorInfo ctor in ctorList)
                {
                    var infos = ctor.GetParameters();

                    if (infos.Length != args.Length)
                        continue;

                    return ctor.Invoke(args);
                }

                throw new Exception($"[{type.Name}] 找不到构造函数, 参数个数={args.Length}");
            }
        }

        #endregion

        #region Type

        /// <summary>
        ///     检查特定成员是否是公有的
        /// </summary>
        public static bool IsPublic(this MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                    return fieldInfo.IsPublic;
                case PropertyInfo propertyInfo:
                    return propertyInfo.CanRead && propertyInfo.GetMethod.IsPublic;
            }

            return false;
        }

        /// <summary>
        ///     检查一个类是否重载了其基类的某个函数
        /// </summary>
        public static bool IsMethodOverride(this Type type, string methodName)
        {
            MethodInfo methodInfo = type.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (methodInfo != null)
            {
                return methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;
            }

            return false;
        }

        public static T[] GetEnums<T>()
        {
            return (T[])Enum.GetValues(typeof(T));
        }

        public static partial class Reflection
        {
            private static readonly Dictionary<string, Type> m_Types;

            static Reflection()
            {
                m_Types = new();

                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (type.IsClass &&
                            !type.IsAbstract &&
                            !string.IsNullOrEmpty(type.FullName))
                        {
                            m_Types[type.FullName] = type;
                        }
                    }
                }
            }

            /// <summary>
            ///     获取所有满足指定继承条件的类型, 只会返回非抽象类
            /// </summary>
            /// <param name="baseTypes">需要继承的类或者接口</param>
            public static IEnumerable<Type> AllTypes(params Type[] baseTypes)
            {
                foreach (Type type in m_Types.Values)
                {
                    bool isMatch = true;
                    foreach (Type baseType in baseTypes)
                    {
                        if (!baseType.IsAssignableFrom(type))
                        {
                            isMatch = false;
                            break;
                        }
                    }

                    if (isMatch)
                        yield return type;
                }
            }

            public static Type GetType(string fullName)
            {
                return m_Types.GetValueOrDefault(fullName);
            }

            /// <summary>
            ///     获取指定类型所及继承的泛型类型
            /// </summary>
            public static Type GetGenericType(Type type)
            {
                while (type != null && type != typeof(object))
                {
                    if (type.IsGenericType)
                        return type;
                    type = type.BaseType;
                }

                return null;
            }

            /// <summary>
            ///     获取type所继承的泛型类型参数
            /// </summary>
            public static Type GetGenericArguments(Type type, int i)
            {
                Type genericType = GetGenericType(type);
                if (genericType?.GenericTypeArguments != null &&
                    0 <= i && i < genericType.GenericTypeArguments.Length)
                {
                    return genericType.GenericTypeArguments[i];
                }

                return null;
            }

            /// <summary>
            ///     将 from 中的数据拷贝至 to, 简单拷贝，不复制对象
            /// </summary>
            public static void SoftCopy(object from, object to)
            {
                if (from == null || to == null)
                    throw new ArgumentNullException();
                if (from.GetType() != to.GetType())
                    throw new ArgumentException();

                Type type = from.GetType();
                if (!type.IsClass)
                    throw new ArgumentException("只支持 Class 拷贝");

                foreach (FieldInfo fieldInfo in type.GetFields())
                {
                    object val = fieldInfo.GetValue(from);
                    fieldInfo.SetValue(to, val);
                }

                foreach (PropertyInfo propertyInfo in type.GetProperties())
                {
                    if (propertyInfo.CanRead && propertyInfo.CanWrite)
                    {
                        object val = propertyInfo.GetValue(from);
                        propertyInfo.SetValue(to, val);
                    }
                }
            }
        }

        #endregion

        #region Field

        /// <summary>
        ///     获取指定名字下 Field
        /// </summary>
        public static FieldInfo GetFieldInfo(this Type type, string name)
        {
            return type.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static partial class Reflection
        {
            private static readonly Dictionary<Type, FieldInfo[]> m_FieldInfos = new();

            /// <summary>
            ///     获取指定类型的所有Field
            /// </summary>
            public static FieldInfo[] AllFields(Type type)
            {
                if (!m_FieldInfos.TryGetValue(type, out var infos))
                {
                    infos = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance);
                    m_FieldInfos.Add(type, infos);
                }

                return infos;
            }

            /// <summary>
            ///     获取指定类型下，含有指定Attribute的所有Field
            /// </summary>
            public static IEnumerable<FieldInfo> AllFields(Type type, Type attributeType)
            {
                foreach (FieldInfo fieldInfo in AllFields(type))
                {
                    if (fieldInfo.GetCustomAttribute(attributeType) != null)
                        yield return fieldInfo;
                }
            }
        }

        #endregion

        #region Property

        /// <summary>
        ///     获取指定名字下 Property
        /// </summary>
        public static PropertyInfo GetPropertyInfo(this Type type, string name)
        {
            return type.GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static partial class Reflection
        {
            private static readonly Dictionary<Type, PropertyInfo[]> m_PropertyInfos = new();


            /// <summary>
            ///     获取指定类型下的所有Property
            /// </summary>
            public static PropertyInfo[] AllProperties(Type type)
            {
                if (!m_PropertyInfos.TryGetValue(type, out var infos))
                {
                    infos = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Instance);
                    m_PropertyInfos.Add(type, infos);
                }

                return infos;
            }

            /// <summary>
            ///     获取指定类型下，含有指定Attribute的所有Property
            /// </summary>
            public static IEnumerable<PropertyInfo> AllProperties(Type type, Type attributeType)
            {
                foreach (PropertyInfo propertyInfo in AllProperties(type))
                {
                    if (propertyInfo.GetCustomAttribute(attributeType) != null)
                        yield return propertyInfo;
                }
            }
        }

        #endregion

        #region Method

        public static partial class Reflection
        {
            private static readonly Dictionary<Type, MethodInfo[]> m_MethodInfos = new();


            /// <summary>
            ///     获取指定类型下的所有Property
            /// </summary>
            public static MethodInfo[] AllMethods(Type type)
            {
                if (!m_MethodInfos.TryGetValue(type, out var infos))
                {
                    infos = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance);
                    m_MethodInfos.Add(type, infos);
                }

                return infos;
            }

            /// <summary>
            ///     获取指定类型下，含有指定Attribute的所有Property
            /// </summary>
            public static IEnumerable<MethodInfo> AllMethods(Type type, Type attributeType)
            {
                foreach (MethodInfo methodInfo in AllMethods(type))
                {
                    if (methodInfo.GetCustomAttribute(attributeType) != null)
                        yield return methodInfo;
                }
            }

            /// <summary>
            ///     获取继承自指定类型，含有指定Attribute的所有Property
            /// </summary>
            public static IEnumerable<MethodInfo> AllMethodsFromBaseType
                (Type baseType, Type attributeType)
            {
                var types = AllTypes(baseType);
                foreach (var type in types)
                {
                    foreach (MethodInfo methodInfo in AllMethods(type))
                    {
                        if (methodInfo.GetCustomAttribute(attributeType) != null)
                            yield return methodInfo;
                    }
                }
            }
        }

        #endregion
    }
}