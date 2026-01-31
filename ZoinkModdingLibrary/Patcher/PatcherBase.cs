using Duckov.Modding;
using HarmonyLib;
using ParadoxNotion.Services;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Logging;

namespace ZoinkModdingLibrary.Patcher
{
    public abstract class PatcherBase : IPatcher
    {
        public static PatcherBase? Instance { get; }

        private Type? targetType = null;
        private bool isPatched = false;
        private Dictionary<string, PatchEntry>? queue;
        protected Harmony? harmony;

        public virtual bool IsPatched => isPatched;

        public virtual PatcherBase Setup(Harmony? harmony)
        {
            this.harmony = harmony;
            ModManager.OnModActivated += OnModActivated;
            ModManager.OnModWillBeDeactivated += OnModWillBeDeactivated;
            return this;
        }

        public virtual bool Patch()
        {
            try
            {
                if (isPatched)
                {
                    Log.Warning("Already Patched");
                    return true;
                }
                TypePatcherAttribute typePatcher = GetType().GetCustomAttribute<TypePatcherAttribute>();
                if (typePatcher == null)
                {
                    Log.Error($"{GetType().Name} 需要 \"{typeof(TypePatcherAttribute).Name}\" 特性（Attribute）");
                    isPatched = false;
                    return false;
                }
                targetType = typePatcher.TargetType;
                if (targetType == null)
                {
                    if (typePatcher.IsCertain)
                    {
                        Log.Error($"{GetType().Name}: 找不到目标类！");
                    }
                    else
                    {
                        Log.Warning($"找不到程序集 \"{typePatcher.TargetAssemblyName}\" 或者类 \"{typePatcher.TargetTypeName}\"！即将检测后续Mod，如果找到匹配的将再次尝试Patch");
                    }
                    isPatched = false;
                    return false;
                }
                Log.Info($"Patching {targetType.Name}");
                IEnumerable<MethodInfo> patchMethods = GetType().GetMethods().Where(s => s.HasAttribute<MethodPatcherAttribute>());
                if (patchMethods.Count() == 0)
                {
                    Log.Warning($"没有找到任何需要被Patch的方法！是否没有定义 \"{typeof(MethodPatcherAttribute).Name}\" 特性（Attribute）?");
                }
                if (queue == null)
                {
                    queue = new Dictionary<string, PatchEntry>();
                    foreach (MethodInfo method in patchMethods)
                    {
                        MethodPatcherAttribute? methodPatcher = method.GetCustomAttribute<MethodPatcherAttribute>();
                        if (methodPatcher == null)
                        {
                            continue;
                        }
                        string targetMethod = methodPatcher.MethodName;
                        PatchType patchType = methodPatcher.PatchType;
                        MethodInfo? originalMethod = targetType.GetMethod(targetMethod, methodPatcher.BindingFlags);
                        if (originalMethod == null)
                        {
                            Log.Warning($"Target Method \"{targetType.Name}.{targetMethod}\" Not Found!");
                            continue;
                        }
                        Log.Info($"Patching {targetType.Name}.{originalMethod.Name}");
                        PatchEntry entry;
                        if (queue.ContainsKey(originalMethod.ToString()))
                        {
                            entry = queue[originalMethod.ToString()];
                        }
                        else
                        {
                            entry = new PatchEntry(originalMethod);
                            queue.Add(originalMethod.ToString(), entry);
                        }
                        switch (patchType)
                        {
                            case PatchType.Prefix:
                                entry.prefix = new HarmonyMethod(method);
                                break;
                            case PatchType.Postfix:
                                entry.postfix = new HarmonyMethod(method);
                                break;
                            case PatchType.Transpiler:
                                entry.transpiler = new HarmonyMethod(method);
                                break;
                            case PatchType.Finalizer:
                                entry.finalizer = new HarmonyMethod(method);
                                break;
                            default:
                                Log.Warning($"Unknown Patch Type \"{patchType}\".");
                                break;
                        }
                    }
                }

                foreach (KeyValuePair<string, PatchEntry> item in queue)
                {
                    item.Value.Patch(harmony);
                }
                isPatched = true;
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Error When Patching: {e.Message}");
                return false;
            }
        }

        public virtual void Unpatch()
        {
            if (isPatched && queue != null)
            {
                foreach (KeyValuePair<string, PatchEntry> item in queue)
                {
                    item.Value.Unpatch(harmony);
                    Log.Info($"{item.Key} Unpatched");
                }
                isPatched = false;
            }
            ModManager.OnModWillBeDeactivated -= OnModWillBeDeactivated;
            ModManager.OnModActivated -= OnModActivated;
        }

        protected virtual void OnModWillBeDeactivated(ModInfo mod, ModBehaviour modBehaviour)
        {
            TypePatcherAttribute typePatcher = GetType().GetCustomAttribute<TypePatcherAttribute>();
            if (modBehaviour.GetType().Assembly.FullName.Contains(typePatcher.TargetAssemblyName) && isPatched)
            {
                Log.Info($"检测到匹配的Mod \"{mod.name}\" 即将被卸载，尝试进行Unpatch");
                Unpatch();
            }
        }

        protected virtual void OnModActivated(ModInfo mod, ModBehaviour modBehaviour)
        {
            TypePatcherAttribute typePatcher = GetType().GetCustomAttribute<TypePatcherAttribute>();
            if (modBehaviour.GetType().Assembly.FullName.Contains(typePatcher.TargetAssemblyName) && !isPatched)
            {
                Log.Info($"检测到匹配的Mod \"{mod.name}\" 被激活，尝试进行Patch");
                Patch();
            }
        }
    }
}
