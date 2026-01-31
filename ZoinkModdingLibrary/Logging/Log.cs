namespace ZoinkModdingLibrary.Logging
{
    public static class Log
    {
        public static void Debug(object message)
            => LogManager.Instance.Debug(message);
        
        public static void Info(object message)
            => LogManager.Instance.Info(message);

        public static void Warning(object message)
            => LogManager.Instance.Warning(message);
        
        public static void Error(object message)
            => LogManager.Instance.Error(message);
    }
}
