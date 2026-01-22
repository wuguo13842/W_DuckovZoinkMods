using System;
using System.Collections.Concurrent;
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

        public static object? InvokeStaticMethod(this Type type,  string methodName, object[]? parameters = null)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            return method?.Invoke(null, parameters);
        }

        public static T? InvokeStaticMethod<T>(this Type type,  string methodName, object[]? parameters = null)
        {
            object? result = type.InvokeStaticMethod(methodName, parameters);
            if (result is T typedResult)
            {
                return typedResult;
            }
            return default;
        }

        private static readonly ConcurrentDictionary<(object? eventOwner, EventInfo eventInfo, Delegate handler), Delegate> _boundHandlers = new ConcurrentDictionary<(object?, EventInfo, Delegate), Delegate>();

        #region 多参数事件绑定重载
        public static bool BindEvent(this object eventOwner, string eventName, object handlerInstance, string handlerMethodName)
        {
            EventInfo eventInfo = GetEventInfo(eventOwner, eventName);
            if (eventInfo == null) return false;

            MethodInfo methodInfo = GetMethodInfo(handlerInstance, handlerMethodName);
            if (methodInfo == null) return false;

            return BindEventInternal(eventOwner, eventInfo, handlerInstance, methodInfo);
        }
        public static bool BindEvent(this object eventOwner, string eventName, Type handlerType, string handlerMethodName)
        {
            EventInfo eventInfo = GetEventInfo(eventOwner, eventName);
            if (eventInfo == null) return false;

            MethodInfo methodInfo = GetStaticMethodInfo(handlerType, handlerMethodName);
            if (methodInfo == null) return false;

            return BindEventInternal(eventOwner, eventInfo, null, methodInfo);
        }

        public static bool BindEvent<T>(this object eventOwner, string eventName, T handler) where T : Delegate
        {
            EventInfo eventInfo = GetEventInfo(eventOwner, eventName);
            if (eventInfo == null || !typeof(T).IsAssignableFrom(eventInfo.EventHandlerType))
                return false;

            return BindEventInternal(eventOwner, eventInfo, handler.Target, handler.Method);
        }

        public static bool BindStaticEvent(Type eventOwnerType, string eventName, object handlerInstance, string handlerMethodName)
        {
            EventInfo eventInfo = GetStaticEventInfo(eventOwnerType, eventName);
            if (eventInfo == null) return false;

            MethodInfo methodInfo = GetMethodInfo(handlerInstance, handlerMethodName);
            if (methodInfo == null) return false;

            return BindEventInternal(null, eventInfo, handlerInstance, methodInfo);
        }

        public static bool BindStaticEvent(Type eventOwnerType, string eventName, Type handlerType, string handlerMethodName)
        {
            EventInfo eventInfo = GetStaticEventInfo(eventOwnerType, eventName);
            if (eventInfo == null) return false;

            MethodInfo methodInfo = GetStaticMethodInfo(handlerType, handlerMethodName);
            if (methodInfo == null) return false;

            return BindEventInternal(null, eventInfo, null, methodInfo);
        }

        public static bool BindStaticEvent<T>(Type eventOwnerType, string eventName, T handler) where T : Delegate
        {
            EventInfo eventInfo = GetStaticEventInfo(eventOwnerType, eventName);
            if (eventInfo == null || !typeof(T).IsAssignableFrom(eventInfo.EventHandlerType))
                return false;

            return BindEventInternal(null, eventInfo, handler);
        }
        #endregion

        #region 多参数事件解绑重载
        public static bool UnbindEvent(this object eventOwner, string eventName, object handlerInstance, string handlerMethodName)
        {
            EventInfo eventInfo = GetEventInfo(eventOwner, eventName);
            if (eventInfo == null) return false;

            MethodInfo methodInfo = GetMethodInfo(handlerInstance, handlerMethodName);
            if (methodInfo == null) return false;

            return UnbindEventInternal(eventOwner, eventInfo, handlerInstance, methodInfo);
        }
        public static bool UnbindEvent(this object eventOwner, string eventName, Type handlerType, string handlerMethodName)
        {
            EventInfo eventInfo = GetEventInfo(eventOwner, eventName);
            if (eventInfo == null) return false;

            MethodInfo methodInfo = GetStaticMethodInfo(handlerType, handlerMethodName);
            if (methodInfo == null) return false;

            return UnbindEventInternal(eventOwner, eventInfo, null, methodInfo);
        }

        public static bool UnbindEvent<T>(this object eventOwner, string eventName, T handler) where T : Delegate
        {
            EventInfo eventInfo = GetEventInfo(eventOwner, eventName);
            if (eventInfo == null) return false;

            return UnbindEventInternal(eventOwner, eventInfo, handler);
        }

        public static bool UnbindStaticEvent(Type eventOwnerType, string eventName, object handlerInstance, string handlerMethodName)
        {
            EventInfo eventInfo = GetStaticEventInfo(eventOwnerType, eventName);
            if (eventInfo == null) return false;

            MethodInfo methodInfo = GetMethodInfo(handlerInstance, handlerMethodName);
            if (methodInfo == null) return false;

            return UnbindEventInternal(null, eventInfo, handlerInstance, methodInfo);
        }

        public static bool UnbindStaticEvent(Type eventOwnerType, string eventName, Type handlerType, string handlerMethodName)
        {
            EventInfo eventInfo = GetStaticEventInfo(eventOwnerType, eventName);
            if (eventInfo == null) return false;

            MethodInfo methodInfo = GetStaticMethodInfo(handlerType, handlerMethodName);
            if (methodInfo == null) return false;

            return UnbindEventInternal(null, eventInfo, null, methodInfo);
        }

        public static bool UnbindStaticEvent<T>(Type eventOwnerType, string eventName, T handler) where T : Delegate
        {
            EventInfo eventInfo = GetStaticEventInfo(eventOwnerType, eventName);
            if (eventInfo == null) return false;

            return UnbindEventInternal(null, eventInfo, handler.Target, handler.Method);
        }
        #endregion

        #region 核心校验与实现
        private static bool IsMethodMatchDelegate(Type delegateType, MethodInfo method)
        {
            if (!typeof(Delegate).IsAssignableFrom(delegateType))
                return false;

            MethodInfo invokeMethod = delegateType.GetMethod("Invoke");
            ParameterInfo[] delegateParams = invokeMethod.GetParameters();
            ParameterInfo[] methodParams = method.GetParameters();

            // 1. 检查参数数量匹配
            if (delegateParams.Length != methodParams.Length)
                return false;

            // 2. 检查每个参数类型匹配（支持隐式转换）
            for (int i = 0; i < delegateParams.Length; i++)
            {
                if (!delegateParams[i].ParameterType.IsAssignableFrom(methodParams[i].ParameterType))
                    return false;
            }

            // 3. 检查返回类型匹配
            return invokeMethod.ReturnType.IsAssignableFrom(method.ReturnType);
        }

        private static bool BindEventInternal(object? eventOwner, EventInfo eventInfo, Delegate handler)
        {
            (object? eventOwner, EventInfo eventInfo, Delegate handler) key = (eventOwner, eventInfo, handler);
            if (_boundHandlers.TryAdd(key, handler))
            {
                eventInfo.AddEventHandler(eventOwner, handler);
                return true;
            }
            return false;
        }

        private static bool BindEventInternal(object? eventOwner, EventInfo eventInfo, object? handlerInstance, MethodInfo methodInfo)
        {
            if (!IsMethodMatchDelegate(eventInfo.EventHandlerType, methodInfo))
                throw new ArgumentException($"方法签名与事件委托不匹配：{methodInfo} vs {eventInfo.EventHandlerType}");
            Delegate handler = handlerInstance == null ? Delegate.CreateDelegate(eventInfo.EventHandlerType, methodInfo) : Delegate.CreateDelegate(eventInfo.EventHandlerType, handlerInstance, methodInfo);
            return BindEventInternal(eventOwner, eventInfo, handler);
        }

        private static bool UnbindEventInternal(object? eventOwner, EventInfo eventInfo, Delegate handler)
        {
            var key = (eventOwner, eventInfo, handler);

            if (_boundHandlers.TryRemove(key, out var registeredHandler))
            {
                eventInfo.RemoveEventHandler(eventOwner, registeredHandler);
                return true;
            }
            return false;
        }

        private static bool UnbindEventInternal(object? eventOwner, EventInfo eventInfo, object? handlerInstance, MethodInfo methodInfo)
        {
            Delegate handler = handlerInstance == null ? Delegate.CreateDelegate(eventInfo.EventHandlerType, methodInfo) : Delegate.CreateDelegate(eventInfo.EventHandlerType, handlerInstance, methodInfo);
            return UnbindEventInternal(eventOwner, eventInfo, handler);
        }

        public static EventInfo GetEventInfo(this object eventOwner, string eventName)
        {
            Type ownerType = eventOwner?.GetType() ?? throw new ArgumentNullException(nameof(eventOwner));
            return ownerType.GetEvent(eventName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static EventInfo GetStaticEventInfo(Type eventOwnerType, string eventName)
        {
            if (eventOwnerType == null) throw new ArgumentNullException(nameof(eventOwnerType));
            return eventOwnerType.GetEvent(eventName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static MethodInfo GetMethodInfo(this object handlerInstance, string methodName)
        {
            Type handlerType = handlerInstance?.GetType() ?? throw new ArgumentNullException(nameof(handlerInstance));
            return handlerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static MethodInfo GetStaticMethodInfo(Type handlerType, string handlerMethodName)
        {
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            return handlerType.GetMethod(handlerMethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }
        #endregion
    }
}
