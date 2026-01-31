using FMOD.Studio;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.ProBuilder.Shapes;
using UnityEngine.UIElements;
using ZoinkModdingLibrary.Logging;

namespace ZoinkModdingLibrary.Utils
{
    public static class AssemblyOperations
    {
        internal static Assembly? GetCallerAssembly(out MethodBase? callerMethod)
        {
            StackTrace stackTrace = new StackTrace(2);
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            foreach (StackFrame frame in stackTrace.GetFrames())
            {
                MethodBase method = frame.GetMethod();
                var assembly = method.DeclaringType.Assembly;

                if (assembly != null && assembly != currentAssembly &&
                    !assembly.FullName.StartsWith("System.") &&
                    !assembly.FullName.StartsWith("Microsoft.") &&
                    !assembly.FullName.StartsWith("mscorlib."))
                {
                    callerMethod = method;
                    return assembly;
                }
            }
            callerMethod = null;
            return null;
        }

        public static Type? FindTypeInAssemblies(string assembliyName, string typeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.FullName.Contains(assembliyName))
                {
                    Type type = assembly.GetType(typeName);
                    if (type != null) return type;
                }
                continue;
            }
            Log.Error($"找不到包含类型 {typeName} 的程序集 {assembliyName}");
            return null;
        }

        private static BindingFlags GetBindingFlags(object? obj, out bool isStatic, out Type? type)
        {
            isStatic = obj is Type;
            if (obj == null)
            {
                type = null;
                return BindingFlags.Default;
            }
            type = isStatic ? obj as Type : obj.GetType();
            BindingFlags flags = (isStatic ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.NonPublic | BindingFlags.Public;
            return flags;
        }

        private static FieldInfo? GetFieldInfo(this object? obj, string fieldName, out bool isStatic)
        {
            BindingFlags flags = GetBindingFlags(obj, out isStatic, out Type? type);
            FieldInfo? field = type?.GetField(fieldName, flags);
            return field;
        }

        public static T? GetField<T>(this object? obj, string fieldName)
        {
            object? result = obj?.GetFieldInfo(fieldName, out bool isStatic)?.GetValue(isStatic ? null : obj);
            if (result is T typedResult)
            {
                return typedResult;
            }
            Log.Error($"获取失败：未在类型 {obj?.GetType().Name ?? "null"} 中找到类型为 {typeof(T).Name} 的字段 {fieldName}");
            return default;
        }

        public static void SetField<T>(this object? obj, string fieldName, T value)
        {
            bool isStatic = false;
            FieldInfo? field = obj?.GetFieldInfo(fieldName, out isStatic);
            if (field == null)
            {
                Log.Error($"设置失败：未在类型 {obj?.GetType().Name ?? "null"} 中找到类型为 {typeof(T).Name} 的字段 {fieldName}");
                return;
            }
            field.SetValue(isStatic ? null : obj, value);
        }

        private static PropertyInfo? GetPropertyInfo(this object? obj, string propertyName, out bool isStatic)
        {
            BindingFlags flags = GetBindingFlags(obj, out isStatic, out Type? type);
            PropertyInfo? property = type?.GetProperty(propertyName, flags);
            return property;
        }

        public static T? GetProperty<T>(this object? obj, string propertyName)
        {
            object? result = obj?.GetPropertyInfo(propertyName, out bool isStatic)?.GetValue(isStatic ? null : obj);
            if (result is T typedResult)
            {
                return typedResult;
            }
            Log.Error($"获取失败：未在类型 {obj?.GetType().Name ?? "null"} 中找到类型为 {typeof(T).Name} 的属性 {propertyName}");
            return default;
        }

        public static void SetProperty<T>(this object? obj, string propertyName, T value)
        {
            bool isStatic = false;
            PropertyInfo? property = obj?.GetPropertyInfo(propertyName, out isStatic);
            if (property == null)
            {
                Log.Error($"设置失败：未在类型 {obj?.GetType().Name ?? "null"} 中找到类型为 {typeof(T).Name} 的属性 {propertyName}");
                return;
            }
            property.SetValue(isStatic ? null : obj, value);
        }

        public static EventInfo? GetEventInfo(this object? eventOwner, string eventName, out bool isStatic)
        {
            BindingFlags flags = GetBindingFlags(eventOwner, out isStatic, out Type? type);
            EventInfo? info = type?.GetEvent(eventName, flags);
            if (info == null)
            {
                Log.Error($"获取失败：未在类型 {eventOwner?.GetType().Name ?? "null"} 中找到名为 {eventName} 的事件, Static: {isStatic}");
            }
            return info;
        }

        public static MethodInfo? GetMethodInfo(this object? obj, string methodName, out bool isStatic, Type[]? parameterTypes = null)
        {
            BindingFlags flags = GetBindingFlags(obj, out isStatic, out Type? type);
            MethodInfo? method = parameterTypes == null ?
                type?.GetMethod(methodName, flags) :
                type?.GetMethod(methodName, flags, null, parameterTypes, null);
            if (method == null)
            {
                Log.Error($"获取失败：未在类型 {obj?.GetType().Name ?? "null"} 中找到名为 {methodName} 的方法, Static: {isStatic}");
            }
            return method;
        }

        /// <summary>
        /// 使用反射调用一个对象包含的方法
        /// </summary>
        /// <remarks>此方法使用反射来定位并调用公共或非公共实例，
        /// 或按名称调用静态方法。如果找不到指定的方法，则该方法返回 <see langword="null"/>。
        /// 调用非公共成员时请谨慎，因为这可能违反封装或安全约束。
        /// </remarks>
        /// <param name="obj">要调用方法的对象实例，或要调用静态方法的 <see cref="Type"/> 对象。</param>
        /// <param name="methodName">要调用的方法的名称。此值区分大小写。</param>
        /// <param name="parameters">要传递给方法的参数数组，如果方法不需要参数，则为 <see langword="null"/>。</param>
        /// <returns>被调用方法的返回值，如果方法没有返回值或
        /// 找不到该方法，则为 <see langword="null"/>。</returns>
        public static object? InvokeMethod(this object? obj, string methodName, Type[]? parameterTypes = null, object?[]? parameters = null)
        {
            bool isStatic = false;
            MethodInfo? method = obj?.GetMethodInfo(methodName, out isStatic, parameterTypes);
            return method?.Invoke(isStatic ? null : obj, parameters);
        }

        public static T? InvokeMethod<T>(this object? obj, string methodName, Type[]? parameterTypes = null, object?[]? parameters = null)
        {
            object? result = obj.InvokeMethod(methodName, parameterTypes, parameters);
            if (result is T typedResult)
            {
                return typedResult;
            }
            return default;
        }

        public static object? InvokeMethod(this object? obj, string methodName, Func<MethodInfo, bool> search, object?[]? parameters = null)
        {
            if (obj == null) return null;
            BindingFlags flags = GetBindingFlags(obj, out bool isStatic, out Type? type);
            if (type == null) return null;
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (search(method))
                {
                    return method.Invoke(isStatic ? null : obj, parameters);
                }
            }
            return null;
        }

        public static T? InvokeMethod<T>(this object? obj, string methodName, Func<MethodInfo, bool> search, object?[]? parameters = null)
        {
            object? result = obj?.InvokeMethod(methodName, search, parameters);
            if (result is T typedResult)
            {
                return typedResult;
            }
            return default;
        }

        public static object? InvokeGenericMethod<T>(this object? obj, string methodName, Type[]? parameterTypes = null, object?[]? parameters = null)
        {
            bool isStatic = false;
            MethodInfo? method = obj?.GetMethodInfo(methodName, out isStatic, parameterTypes);
            MethodInfo? concreteMethod = method?.MakeGenericMethod(typeof(T));
            return concreteMethod?.Invoke(isStatic ? null : obj, parameters);
        }

        public static T2? InvokeGenericMethod<T1, T2>(this object? obj, string methodName, Type[]? parameterTypes = null, object?[]? parameters = null)
        {
            object? result = obj?.InvokeGenericMethod<T1>(methodName, parameterTypes, parameters);
            if (result is T2 typedResult)
            {
                return typedResult;
            }
            return default;
        }

        public static object? InvokeGenericMethod(this object? obj, string methodName, Type[] genericTypes, Type[]? parameterTypes = null, object?[]? parameters = null)
        {
            bool isStatic = false;
            MethodInfo? method = obj?.GetMethodInfo(methodName, out isStatic, parameterTypes);
            MethodInfo? concreteMethod = method?.MakeGenericMethod(genericTypes);
            return concreteMethod?.Invoke(isStatic ? null : obj, parameters);
        }

        public static T? InvokeGenericMethod<T>(this object? obj, string methodName, Type[] genericTypes, Type[]? parameterTypes = null, object?[]? parameters = null)
        {
            object? result = obj?.InvokeGenericMethod(methodName, genericTypes, parameterTypes, parameters);
            if (result is T typedResult)
            {
                return typedResult;
            }
            return default;
        }

        private static readonly ConcurrentDictionary<(object? eventOwner, EventInfo eventInfo, Delegate handler), Delegate> _boundHandlers = new ConcurrentDictionary<(object?, EventInfo, Delegate), Delegate>();

        #region 多参数事件绑定重载
        public static bool BindEvent(this object? eventOwner, string eventName, object? handlerOwner, string handlerMethodName)
        {
            EventInfo? eventInfo = GetEventInfo(eventOwner, eventName, out bool isStaticEvent);
            if (eventInfo == null) return false;

            MethodInfo? methodInfo = GetMethodInfo(handlerOwner, handlerMethodName, out bool isStaticMethod);
            if (methodInfo == null) return false;

            return BindEventInternal(isStaticEvent ? null : eventOwner, eventInfo, isStaticMethod ? null : handlerOwner, methodInfo);
        }
        //public static bool BindEvent(this object? eventOwner, string eventName, Type handlerType, string handlerMethodName)
        //{
        //    EventInfo? eventInfo = GetEvent(eventOwner, eventName);
        //    if (eventInfo == null) return false;

        //    MethodInfo methodInfo = GetStaticMethodInfo(handlerType, handlerMethodName);
        //    if (methodInfo == null) return false;

        //    return BindEventInternal(eventOwner, eventInfo, null, methodInfo);
        //}

        public static bool BindEvent<T>(this object? eventOwner, string eventName, T handler) where T : Delegate
        {
            EventInfo? eventInfo = GetEventInfo(eventOwner, eventName, out bool isStaticEvent);
            if (eventInfo == null || !typeof(T).IsAssignableFrom(eventInfo.EventHandlerType))
                return false;

            return BindEventInternal(isStaticEvent ? null : eventOwner, eventInfo, handler);
        }

        //public static bool BindStaticEvent(Type eventOwnerType, string eventName, object handlerInstance, string handlerMethodName)
        //{
        //    EventInfo eventInfo = GetStaticEventInfo(eventOwnerType, eventName);
        //    if (eventInfo == null) return false;

        //    MethodInfo? methodInfo = GetMethod(handlerInstance, handlerMethodName);
        //    if (methodInfo == null) return false;

        //    return BindEventInternal(null, eventInfo, handlerInstance, methodInfo);
        //}

        //public static bool BindStaticEvent(Type eventOwnerType, string eventName, Type handlerType, string handlerMethodName)
        //{
        //    EventInfo eventInfo = GetStaticEventInfo(eventOwnerType, eventName);
        //    if (eventInfo == null) return false;

        //    MethodInfo methodInfo = GetStaticMethodInfo(handlerType, handlerMethodName);
        //    if (methodInfo == null) return false;

        //    return BindEventInternal(null, eventInfo, null, methodInfo);
        //}

        //public static bool BindStaticEvent<T>(Type eventOwnerType, string eventName, T handler) where T : Delegate
        //{
        //    EventInfo? eventInfo = eventOwnerType?.GetEventInfo(eventName);
        //    if (eventInfo == null || !typeof(T).IsAssignableFrom(eventInfo.EventHandlerType))
        //        return false;

        //    return BindEventInternal(null, eventInfo, handler);
        //}
        #endregion

        #region 多参数事件解绑重载
        public static bool UnbindEvent(this object? eventOwner, string eventName, object? handlerOwner, string handlerMethodName)
        {
            EventInfo? eventInfo = GetEventInfo(eventOwner, eventName, out bool isStaticEvent);
            if (eventInfo == null) return false;

            MethodInfo? methodInfo = GetMethodInfo(handlerOwner, handlerMethodName, out bool isStaticMethod);
            if (methodInfo == null) return false;

            return UnbindEventInternal(isStaticEvent ? null : eventOwner, eventInfo, isStaticMethod ? null : handlerOwner, methodInfo);
        }
        //public static bool UnbindEvent(this object? eventOwner, string eventName, Type handlerType, string handlerMethodName)
        //{
        //    EventInfo? eventInfo = GetEventInfo(eventOwner, eventName);
        //    if (eventInfo == null) return false;

        //    MethodInfo methodInfo = GetStaticMethodInfo(handlerType, handlerMethodName);
        //    if (methodInfo == null) return false;

        //    return UnbindEventInternal(eventOwner, eventInfo, null, methodInfo);
        //}

        public static bool UnbindEvent<T>(this object eventOwner, string eventName, T handler) where T : Delegate
        {
            EventInfo? eventInfo = GetEventInfo(eventOwner, eventName, out bool isStaticEvent);
            if (eventInfo == null) return false;

            return UnbindEventInternal(eventOwner, eventInfo, handler);
        }

        //public static bool UnbindStaticEvent(Type eventOwnerType, string eventName, object handlerInstance, string handlerMethodName)
        //{
        //    EventInfo? eventInfo = GetStaticEventInfo(eventOwnerType, eventName);
        //    if (eventInfo == null) return false;

        //    MethodInfo methodInfo = GetMethodInfo(handlerInstance, handlerMethodName);
        //    if (methodInfo == null) return false;

        //    return UnbindEventInternal(null, eventInfo, handlerInstance, methodInfo);
        //}

        //public static bool UnbindStaticEvent(Type eventOwnerType, string eventName, Type handlerType, string handlerMethodName)
        //{
        //    EventInfo? eventInfo = GetEventInfo(eventOwnerType, eventName);
        //    if (eventInfo == null) return false;

        //    MethodInfo? methodInfo = GetStaticMethodInfo(handlerType, handlerMethodName);
        //    if (methodInfo == null) return false;

        //    return UnbindEventInternal(null, eventInfo, null, methodInfo);
        //}

        //public static bool UnbindStaticEvent<T>(Type eventOwnerType, string eventName, T handler) where T : Delegate
        //{
        //    EventInfo eventInfo = GetStaticEventInfo(eventOwnerType, eventName);
        //    if (eventInfo == null) return false;

        //    return UnbindEventInternal(null, eventInfo, handler.Target, handler.Method);
        //}
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
            if (!IsMethodMatchDelegate(eventInfo.EventHandlerType, methodInfo))
                throw new ArgumentException($"方法签名与事件委托不匹配：{methodInfo} vs {eventInfo.EventHandlerType}");
            Delegate handler = handlerInstance == null ? Delegate.CreateDelegate(eventInfo.EventHandlerType, methodInfo) : Delegate.CreateDelegate(eventInfo.EventHandlerType, handlerInstance, methodInfo);
            return UnbindEventInternal(eventOwner, eventInfo, handler);
        }
        #endregion
    }
}
