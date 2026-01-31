using System;
using System.Collections.Generic;
using System.Text;

namespace ZoinkModdingLibrary.Logging
{
    public class LogConfig
    {
        public int MaxLogFiles { get; set; } = 2;
        public string AssemblyLogDir { get; set; } = string.Empty;
        public int LogBatchSize { get; set; } = 100; // 批量写入阈值
        public int LogFlushInterval { get; set; } = 5000; // 自动刷新间隔（毫秒）
        public bool EnableLogDesensitization { get; set; } = true; // 生产环境默认开启日志脱敏
    }
}
