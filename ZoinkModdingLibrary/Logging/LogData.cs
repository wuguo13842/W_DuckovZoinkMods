namespace ZoinkModdingLibrary.Logging
{
    public struct LogData
    {
        public string AssemblyName;
        public string AssemblyDir;
        public LogLevel LogLevel;
        public string Message;

        public LogData(string assemblyName, string assemblyDir, LogLevel logLevel, string message)
        {
            AssemblyName = assemblyName;
            AssemblyDir = assemblyDir;
            LogLevel = logLevel;
            Message = message;
        }
    }
}
