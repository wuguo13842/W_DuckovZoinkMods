using HarmonyLib;

namespace ZoinkModdingLibrary.Patcher
{
    public interface IPatcher
    {
        public bool IsPatched { get; }

        public abstract bool Patch();

        public abstract void Unpatch();
    }
}
