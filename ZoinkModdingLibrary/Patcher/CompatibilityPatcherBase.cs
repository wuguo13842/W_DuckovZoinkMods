using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using ZoinkModdingLibrary.Logging;

namespace ZoinkModdingLibrary.Patcher
{
    public abstract class CompatibilityPatcherBase : PatcherBase
    {
        protected virtual List<PatcherBase>? SubPatchers { get; }

        public override PatcherBase Setup(Harmony? harmony)
        {
            PatcherBase result = base.Setup(harmony);
            if (SubPatchers != null)
            {
                Log.Info("初始化子补丁程序...");
                foreach (var patcher in SubPatchers)
                {
                    patcher.Setup(harmony);
                }
            }
            return result;
        }

        public override bool Patch()
        {
            bool result = base.Patch();
            Log.Info($"主补丁应用完成，{result}, {SubPatchers?.Count.ToString() ?? "null"}");
            if (result && SubPatchers != null)
            {
                Log.Info("应用子补丁程序...");
                foreach (var patcher in SubPatchers)
                {
                    patcher.Patch();
                }
            }
            return result;
        }

        public override void Unpatch()
        {
            base.Unpatch();
            if (SubPatchers != null)
            {
                Log.Info("移除子补丁程序...");
                foreach (var patcher in SubPatchers)
                {
                    patcher.Unpatch();
                }
            }
        }
    }
}
