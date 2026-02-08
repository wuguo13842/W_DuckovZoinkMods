using Sirenix.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using ZoinkModdingLibrary.Utils;

namespace ZoinkModdingLibrary.Logging
{
    public class LogManager : IDisposable
    {
        private static LogManager? _instance;
        public static LogManager Instance => _instance ??= new LogManager();

        private LogConfig _globalConfig = new LogConfig();
        private readonly ConcurrentQueue<LogData> _logQueue = new ConcurrentQueue<LogData>();
        private readonly Dictionary<string, string> _logFiles = new Dictionary<string, string>();
        private readonly Thread _logWriterThread;
        private readonly AutoResetEvent _logEvent = new AutoResetEvent(false);
        private volatile bool _isRunning = true;
        private readonly object _fileLock = new object();

        private LogManager()
        {
            // 初始化全局配置
            _globalConfig.MaxLogFiles = 3;
            _globalConfig.LogBatchSize = 100;
            _globalConfig.LogFlushInterval = 5000;

            // 启动异步日志写入线程
            _logWriterThread = new Thread(LogWriterLoop)
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.BelowNormal // 降低线程优先级，避免影响主线程
            };
            _logWriterThread.Start();
        }

        private void LogWriterLoop()
        {
            while (_isRunning)
            {
                if (_logEvent.WaitOne(_globalConfig.LogFlushInterval))
                {
                    FlushLogQueue();
                }
                else
                {
                    FlushLogQueue();
                }
            }
            FlushLogQueue();
        }

        private void FlushLogQueue()
        {
            if (_logQueue.IsEmpty) return;

            try
            {
                var logs = new List<LogData>();
                while (_logQueue.TryDequeue(out LogData log))
                {
                    logs.Add(log);
                }

                IEnumerable<IGrouping<string, LogData>> groupedLogs = logs.GroupBy(s => s.AssemblyDir);

                foreach (var group in groupedLogs)
                {
                    UnityEngine.Debug.LogWarning($"Group: {group.Key}");
                    string logDir = group.Key;
                    List<LogData> logDatas = group.ToList();

                    if (!Directory.Exists(logDir))
                        Directory.CreateDirectory(logDir);

                    if (!_logFiles.TryGetValue(logDir, out string logFileName))
                    {
                        logFileName = $"Log_Last.log";
                        _logFiles[logDir] = logFileName;
                        CleanupOldLogs(logDir, _globalConfig.MaxLogFiles);
                    }

                    var logFilePath = Path.Combine(logDir, logFileName);

                    lock (_fileLock)
                    {
                        File.AppendAllLines(logFilePath, logDatas.Select(s => s.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    // 尝试将错误日志写入全局日志文件
                    var errorLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][LogSystem][Error] 日志写入失败: {ex.Message}";
                    var globalLogPath = Path.Combine(Application.persistentDataPath, "GlobalLogError.log");
                    File.AppendAllText(globalLogPath, errorLog + Environment.NewLine);
                }
                catch
                {
                    // 双重异常捕获，确保不会崩溃
                }
            }
        }

        private LogData GetLogData(string message, LogLevel logLevel)
        {
            AssemblyOperations.GetCallerAssembly(out Assembly? assembly, out _);
            assembly ??= Assembly.GetExecutingAssembly();
            return new LogData(assembly.GetName().Name, Path.GetDirectoryName(assembly.Location), logLevel, message);
        }

        private void CleanupOldLogs(string logDir, int maxFiles)
        {
            try
            {
                List<FileInfo> logFiles = new DirectoryInfo(logDir)
                    .GetFiles("*.log")
                    .OrderBy(f => f.CreationTime)
                    .ToList();

                while (logFiles.Count >= maxFiles)
                {
                    var oldestFile = logFiles.First();
                    try
                    {
                        oldestFile.Delete();
                        logFiles.Remove(oldestFile);
                    }
                    catch (Exception ex)
                    {
                        // 无法删除旧文件时，记录错误并退出循环
                        var errorLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][LogSystem][Warning] 无法删除旧日志文件: {ex.Message}";

                        _logQueue.Enqueue(GetLogData(errorLog, LogLevel.Error));
                        break;
                    }
                }
                int postfix = logFiles.Count;
                foreach (FileInfo logFile in logFiles)
                {
                    string newName = "Log_Prev" + (logFiles.Count > 1 ? $"_{postfix}" : "");
                    logFile.Rename($"{newName}.log");
                    postfix--;
                }
            }
            catch (Exception ex)
            {
                string errorLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][LogSystem][Error] 清理旧日志失败: {ex.Message}";
                _logQueue.Enqueue(GetLogData(errorLog, LogLevel.Error));
            }
        }

        public void Log(object message, LogLevel level = LogLevel.Info)
        {
            if (!AssemblyOperations.GetCallerAssembly(out Assembly? callerAssembly, out StackFrame? callerFrame))
            {
                var errorLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][LogSystem][Error] 获取调用方失败";
                _logQueue.Enqueue(GetLogData(errorLog, LogLevel.Error));
                return;
            }
            string assemblyName = callerAssembly!.GetName().Name;
            MethodBase callerMethod = callerFrame!.GetMethod();
            Type callerType = callerMethod.DeclaringType;

            LogAttribute? methodAttribute = callerMethod.GetCustomAttribute<LogAttribute>();
            LogAttribute? typeAttribute = callerType.GetCustomAttribute<LogAttribute>();
            LogAttribute? assemblyAttribute = callerAssembly.GetCustomAttribute<LogAttribute>();

            LogLevel consoleLevel = methodAttribute?.ConsoleLevel ?? LogLevel.Unset;
            if (consoleLevel == LogLevel.Unset)
            {
                consoleLevel = typeAttribute?.ConsoleLevel ?? LogLevel.Unset;
                if (consoleLevel == LogLevel.Unset)
                {
                    consoleLevel = assemblyAttribute?.ConsoleLevel ?? LogLevel.Unset;
                }
            }

            LogLevel fileLevel = methodAttribute?.FileLevel ?? LogLevel.Unset;
            if (fileLevel == LogLevel.Unset)
            {
                fileLevel = typeAttribute?.FileLevel ?? LogLevel.Unset;
                if (fileLevel == LogLevel.Unset)
                {
                    fileLevel = assemblyAttribute?.FileLevel ?? LogLevel.Unset;
                }
            }

            LogOutput consoleOutput = methodAttribute?.ConsoleOutput ?? LogOutput.Unset;
            if (consoleOutput == LogOutput.Unset)
            {
                consoleOutput = typeAttribute?.ConsoleOutput ?? LogOutput.Unset;
                if (consoleOutput == LogOutput.Unset)
                {
                    consoleOutput = assemblyAttribute?.ConsoleOutput ?? LogOutput.Unset;
                }
            }

            LogOutput fileOutput = methodAttribute?.FileOutput ?? LogOutput.Unset;
            if (fileOutput == LogOutput.Unset)
            {
                fileOutput = typeAttribute?.FileOutput ?? LogOutput.Unset;
                if (fileOutput == LogOutput.Unset)
                {
                    fileOutput = assemblyAttribute?.FileOutput ?? LogOutput.Unset;
                }
            }

            string messageStr = message?.ToString() ?? "null";
            if (_globalConfig.EnableLogDesensitization)
            {
                messageStr = DesensitizeLog(messageStr);
            }
            string methodInfoString = level == LogLevel.Debug ? $" {callerFrame.GetFileName()}: {callerFrame.GetFileLineNumber()}({callerFrame.GetMethod().Name})" : "";
            string logContent = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{assemblyName}][{level}]{methodInfoString} {messageStr}";
            if (consoleOutput == LogOutput.Output && level <= consoleLevel)
            {
                switch (level)
                {
                    case LogLevel.Error: UnityEngine.Debug.LogError(logContent); break;
                    case LogLevel.Warning: UnityEngine.Debug.LogWarning(logContent); break;
                    default: UnityEngine.Debug.Log(logContent); break;
                }
            }

            if (fileOutput == LogOutput.Output && level <= fileLevel)
            {
                // 检查 Mod 目录下是否有 WWSSADADBA 文件
                bool enableFileLogging = false;
                try
                {
                    // 获取 Mod DLL 所在目录
                    string modDirectory = Path.GetDirectoryName(callerAssembly?.Location);
                    if (!string.IsNullOrEmpty(modDirectory))
                    {
                        string debugFilePath = Path.Combine(modDirectory, "WWSSADADBA");
                        enableFileLogging = File.Exists(debugFilePath);
                        
                        // 可选：输出调试信息
                        if (enableFileLogging)
                        {
                            UnityEngine.Debug.Log($"[LogManager] 检测到 {debugFilePath}，为 {assemblyName} 启用文件日志");
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[LogManager] 检查调试文件失败: {ex.Message}");
                    enableFileLogging = false;
                }

                // 只有在检测到 WWSSADADBA 文件时才写入文件日志
                if (enableFileLogging)
                {
                    string outputDir = methodAttribute?.OutputDir ?? string.Empty;
                    if (string.IsNullOrEmpty(outputDir))
                    {
                        outputDir = typeAttribute?.OutputDir ?? string.Empty;
                        if (string.IsNullOrEmpty(outputDir))
                        {
                            outputDir = assemblyAttribute?.OutputDir ?? string.Empty;
                            if (string.IsNullOrEmpty(outputDir))
                            {
                                outputDir = Path.GetDirectoryName((callerAssembly ?? Assembly.GetExecutingAssembly()).Location);
                            }
                        }
                    }

                    _logQueue.Enqueue(new LogData(assemblyName, outputDir, level, logContent));
                    // 达到批量写入阈值时，触发写入事件
                    if (_logQueue.Count >= _globalConfig.LogBatchSize)
                    {
                        _logEvent.Set();
                    }
                }
            }
        }

        // 日志内容脱敏
        private string DesensitizeLog(string message)
        {
            // 脱敏规则可以根据实际需求扩展
            message = System.Text.RegularExpressions.Regex.Replace(message, @"(?<=(\b|\D))\d{11}(?=(\b|\D))", "***********"); // 脱敏手机号
            message = System.Text.RegularExpressions.Regex.Replace(message, @"(?<=(\b|\D))\d{16,19}(?=(\b|\D))", "**** **** **** ****"); // 脱敏银行卡号
            message = System.Text.RegularExpressions.Regex.Replace(message, @"(?<=@)\w+(?=\.)", "***"); // 脱敏邮箱用户名
            return message;
        }

        public void Debug(object message)
            => Log(message, LogLevel.Debug);


        public void Info(object message)
            => Log(message, LogLevel.Info);

        public void Warning(object message)
            => Log(message, LogLevel.Warning);


        public void Error(object message)
            => Log(message, LogLevel.Error);

        public void SetGlobalConfig(LogConfig config)
        {
            if (config != null)
                _globalConfig = config;
        }

        public void Dispose()
        {
            _isRunning = false;
            _logEvent.Set(); // 唤醒日志写入线程
            _logWriterThread.Join(5000); // 等待线程退出，最多等待5秒

            _logEvent?.Dispose();
        }
    }
}