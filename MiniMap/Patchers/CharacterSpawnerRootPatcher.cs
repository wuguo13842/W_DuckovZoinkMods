using MiniMap.Managers;
using MiniMap.Poi;
using MiniMap.Utils;
using System;
using ZoinkModdingLibrary.Attributes;
using ZoinkModdingLibrary.Logging;
using ZoinkModdingLibrary.Patcher;

namespace MiniMap.Patchers
{
    [TypePatcher(typeof(CharacterSpawnerRoot))]
    public class CharacterSpawnerRootPatcher : PatcherBase
    {
        public static new PatcherBase Instance { get; } = new CharacterSpawnerRootPatcher();

        private CharacterSpawnerRootPatcher() { }

        [MethodPatcher("AddCreatedCharacter", PatchType.Prefix)]
        public static bool AddCreatedCharacterPrefix(CharacterMainControl c)
        {
            try
            {
                PoiCommon.CreatePoiIfNeeded(c, out _, out _);
            }
            catch (Exception e)
            {
                Log.Error($"characterPoi add failed: {e.Message}");
            }
            return true;
        }
    }
}
