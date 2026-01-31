using HarmonyLib;
using ItemStatsSystem;
using ParadoxNotion.Services;
using Sirenix.Serialization;
using System;
using System.Reflection;
using UnityEngine;
using ZoinkModdingLibrary.Logging;

namespace ZoinkModdingLibrary.Patcher
{
    public class PatchEntry
    {
        private MethodInfo original;
        public HarmonyMethod? prefix;
        public HarmonyMethod? postfix;
        public HarmonyMethod? transpiler;
        public HarmonyMethod? finalizer;

        public PatchEntry(MethodInfo original)
        {
            this.original = original;
        }

        public bool IsEmpty => prefix == null && postfix == null && transpiler == null && finalizer == null;

        public void Patch(Harmony? harmony)
        {
            if (IsEmpty)
            {
                return;
            }
            try
            {
                harmony?.Patch(original, prefix, postfix, transpiler, finalizer);
                Log.Info($"{original.Name} Patched");
            }
            catch (Exception e)
            {
                Log.Error($"{original.Name} Patch Failed: {e.Message}\n{e.StackTrace}");
            }
        }

        public void Unpatch(Harmony? harmony)
        {
            harmony?.Unpatch(original, HarmonyPatchType.All, harmony.Id);
        }
    }
}
