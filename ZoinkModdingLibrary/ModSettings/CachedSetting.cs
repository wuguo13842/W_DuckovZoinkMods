using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZoinkModdingLibrary.ModSettings
{
    public class CachedSetting
    {
        public bool IsUICreated { get; set; } = false;
        public JObject? Template { get; internal set; }
        public JObject Config { get; internal set; }

        public CachedSetting(JObject? template, JObject config)
        {
            Template = template;
            Config = config;
        }
    }
}
