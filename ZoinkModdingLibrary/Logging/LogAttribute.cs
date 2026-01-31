using System;

namespace ZoinkModdingLibrary.Logging
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly | AttributeTargets.Method, AllowMultiple = false)]
    public class LogAttribute : Attribute
    {
        private LogOutput consoleOutput = LogOutput.Unset;
        private LogOutput fileOutput = LogOutput.Unset;
        private LogLevel consoleLevel = LogLevel.Unset;
        private LogLevel fileLevel = LogLevel.Unset;
        private string outputDir = "";

        public LogOutput ConsoleOutput { get => consoleOutput; set => consoleOutput = value; }
        public LogOutput FileOutput { get => fileOutput; set => fileOutput = value; }
        public LogLevel ConsoleLevel { get => consoleLevel; set => consoleLevel = value; }
        public LogLevel FileLevel { get => fileLevel; set => fileLevel = value; }
        public string OutputDir { get => outputDir; set => outputDir = value; }

        public LogAttribute(LogOutput consoleOutput = LogOutput.Unset, LogOutput fileOutput = LogOutput.Unset, LogLevel consoleLevel = LogLevel.Unset, LogLevel fileLevel = LogLevel.Unset)
        {
            ConsoleOutput = consoleOutput;
            FileOutput = fileOutput;
            ConsoleLevel = consoleLevel;
            FileLevel = fileLevel;
        }

        public LogAttribute(string consoleOutput, string fileOutput, string consoleLevel, string fileLevel, string outputDir = "")
        {
            if (!Enum.TryParse(consoleOutput, true, out this.consoleOutput))
            {
                this.consoleOutput = LogOutput.Unset;
            }
            if (!Enum.TryParse(fileOutput, true, out this.fileOutput))
            {
                this.fileOutput = LogOutput.Unset;
            }
            if (!Enum.TryParse(consoleLevel, true, out this.consoleLevel))
            {
                this.consoleLevel = LogLevel.Unset;
            }
            if (!Enum.TryParse(fileLevel, true, out this.fileLevel))
            {
                this.fileLevel = LogLevel.Unset;
            }
            this.outputDir = outputDir;
        }
    }
}
