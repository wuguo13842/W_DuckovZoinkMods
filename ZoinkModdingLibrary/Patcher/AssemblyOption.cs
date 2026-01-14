using System;
using System.Reflection;
using UnityEngine;

namespace ZoinkModdingLibrary.Patcher
{
    public static class AssemblyOption
    {
        public static Type? FindTypeInAssemblies(string assembliyName, string typeName, ModLogger? logger = null)
        {
            logger ??= ModLogger.DefultLogger;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.FullName.Contains(assembliyName))
                {
                    logger.Log($"找到{assembliyName}相关程序集: {assembly.FullName}");
                }

                Type type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            logger.LogError($"找不到程序集{assembliyName}");
            return null;
        }

        public static T? GetField<T>(this object? obj, string fieldName)
        {
            FieldInfo? field = obj?.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            object? result = field?.GetValue(obj);
            if (result is T typedResult)
            {
                return typedResult;
            }
            return default;
        }

        public static void SetField<T>(this object? obj, string fieldName, T value)
        {
            FieldInfo? field = obj?.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            field?.SetValue(obj, value);
        }

        public static T? GetProperty<T>(this object? obj, string propertyName)
        {
            PropertyInfo? property = obj?.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            object? result = property?.GetValue(obj);
            if (result is T typedResult)
            {
                return typedResult;
            }
            return default;
        }

        public static void SetProperty<T>(this object? obj, string propertyName, T value)
        {
            PropertyInfo? property = obj?.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            property?.SetValue(obj, value);
        }

        public static object? InvokeMethod(this object? obj, string methodName, object[]? parameters = null)
        {
            MethodInfo? method = obj?.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return method?.Invoke(obj, parameters);
        }

        public static T? InvokeMethod<T>(this object? obj, string methodName, object[]? parameters = null)
        {
            object? result = obj.InvokeMethod(methodName, parameters);
            if (result is T typedResult)
            {
                return typedResult;
            }
            return default;
        }
    }
}
