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

namespace ZoinkModdingLibrary.Patcher
{
    public abstract class PatcherBase : IPatcher
    {
        public static PatcherBase? Instance { get; }

        private Type? targetType = null;
        private bool isPatched = false;
        private Dictionary<string, PatchEntry>? queue;

        public virtual bool IsPatched => isPatched;

        public virtual bool Patch(Harmony? harmony, ModLogger? logger = null)
        {
            logger ??= ModLogger.DefultLogger;
            try
            {
                if (isPatched)
                {
                    logger.LogWarning("Already Patched");
                    return true;
                }
                TypePatcherAttribute typePatcher = GetType().GetCustomAttribute<TypePatcherAttribute>();
                if (typePatcher == null)
                {
                    logger.LogError($"{GetType().Name} needs \"{typeof(TypePatcherAttribute).Name}\" Attribute");
                    return false;
                }
                targetType = typePatcher.TargetType;
                if (targetType == null)
                {
                    logger.LogWarning($"Target Assembly \"{typePatcher.TargetAssemblyName}\" Or Type \"{typePatcher.TargetTypeName}\" Not Found!");
                    return false;
                }
                logger.Log($"Patching {targetType.Name}");
                IEnumerable<MethodInfo> patchMethods = GetType().GetMethods().Where(s => s.HasAttribute<MethodPatcherAttribute>());
                logger.Log($"Find {patchMethods.Count()} Methods to patch");
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
                            Debug.LogWarning($"Target Method \"{targetType.Name}.{targetMethod}\" Not Found!");
                            continue;
                        }
                        logger.Log($"Patching {targetType.Name}.{originalMethod.Name}");
                        PatchEntry entry;
                        if (queue.ContainsKey(originalMethod.ToString()))
                        {
                            entry = queue[originalMethod.ToString()];
                        }
                        else
                        {
                            entry = new PatchEntry(originalMethod, logger);
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
                                Debug.LogWarning($"Unknown Patch Type \"{patchType}\".");
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
                logger.LogError($"Error When Patching: {e.Message}");
                return false;
            }
        }

        public virtual void Unpatch(Harmony? harmony, ModLogger? logger = null)
        {
            logger ??= ModLogger.DefultLogger;
            if (isPatched && queue != null)
            {
                foreach (KeyValuePair<string, PatchEntry> item in queue)
                {
                    item.Value.Unpatch(harmony);
                    logger.Log($"{item.Key} Unpatched");
                }
                isPatched = false;
            }
        }
    }
}
