using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace LumaPlayerLimit
{
    internal static class Reflect
    {
        public static object GetStaticMemberValue(string typeName, string memberName)
        {
            var type = AccessTools.TypeByName(typeName);
            return type == null ? null : GetMemberValue(type, memberName);
        }

        public static object GetMemberValue(object target, string memberName)
        {
            if (target == null)
            {
                return null;
            }

            if (target is Type)
            {
                var staticType = (Type)target;
                var staticProperty = AccessTools.Property(staticType, memberName);
                if (staticProperty != null)
                {
                    return staticProperty.GetValue(null, null);
                }

                var staticField = AccessTools.Field(staticType, memberName);
                return staticField != null ? staticField.GetValue(null) : null;
            }

            var type = target.GetType();
            var property = AccessTools.Property(type, memberName);
            if (property != null)
            {
                return property.GetValue(target, null);
            }

            var field = AccessTools.Field(type, memberName);
            return field != null ? field.GetValue(target) : null;
        }

        public static bool SetMemberValue(object target, string memberName, object value)
        {
            if (target == null)
            {
                return false;
            }

            var type = target.GetType();
            var property = AccessTools.Property(type, memberName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value, null);
                return true;
            }

            var field = AccessTools.Field(type, memberName);
            if (field != null)
            {
                field.SetValue(target, value);
                return true;
            }

            return false;
        }

        public static bool GetBool(object target, string memberName, bool fallback)
        {
            var value = GetMemberValue(target, memberName);
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return fallback;
            }
        }

        public static int GetInt(object target, string memberName, int fallback)
        {
            var value = GetMemberValue(target, memberName);
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        public static int GetCount(object maybeCollection)
        {
            if (maybeCollection == null)
            {
                return 0;
            }

            var collection = maybeCollection as ICollection;
            if (collection != null)
            {
                return collection.Count;
            }

            var countValue = GetMemberValue(maybeCollection, "Count");
            if (countValue != null)
            {
                try
                {
                    return Convert.ToInt32(countValue);
                }
                catch
                {
                }
            }

            return 0;
        }

        public static bool InvokeIfExists(object target, string methodName, params object[] args)
        {
            if (target == null)
            {
                return false;
            }

            var method = FindCompatibleMethod(target.GetType(), methodName, args, false);
            if (method == null)
            {
                return false;
            }

            method.Invoke(target, args);
            return true;
        }

        public static bool InvokeStaticIfExists(string typeName, string methodName, params object[] args)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return false;
            }

            var method = FindCompatibleMethod(type, methodName, args, true);
            if (method == null)
            {
                return false;
            }

            method.Invoke(null, args);
            return true;
        }

        private static MethodInfo FindCompatibleMethod(Type type, string methodName, object[] args, bool isStatic)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            var methods = type.GetMethods(flags).Where(m => m.Name == methodName);

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                var compatible = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    var argument = args[i];
                    var parameterType = parameters[i].ParameterType;

                    if (argument == null)
                    {
                        if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                        {
                            compatible = false;
                            break;
                        }

                        continue;
                    }

                    if (!parameterType.IsInstanceOfType(argument) && !CanChangeType(argument, parameterType))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible)
                {
                    continue;
                }

                try
                {
                    var convertedArgs = new object[args.Length];
                    Array.Copy(args, convertedArgs, args.Length);
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var argument = convertedArgs[i];
                        if (argument == null)
                        {
                            continue;
                        }

                        var parameterType = parameters[i].ParameterType;
                        if (!parameterType.IsInstanceOfType(argument) && CanChangeType(argument, parameterType))
                        {
                            var underlyingType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
                            convertedArgs[i] = Convert.ChangeType(argument, underlyingType);
                        }
                    }

                    Array.Copy(convertedArgs, args, args.Length);
                    return method;
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool CanChangeType(object value, Type targetType)
        {
            try
            {
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                Convert.ChangeType(value, underlyingType);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
