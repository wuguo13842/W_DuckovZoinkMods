using Duckov.Modding;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZoinkModdingLibrary.Extentions
{
    public static class ModInfoExtension
    {
        public static string GetModId(this ModInfo modInfo)
        {
            return $"{modInfo.publishedFileId}-{modInfo.name}";
        }

        public static bool IsEmpty(this ModInfo modInfo)
        {
            return string.IsNullOrEmpty(modInfo.name);
        }

        public static bool ModIdEquals(this ModInfo self, ModInfo compare)
        {
            return self.GetModId() == compare.GetModId();
        }
    }
}
