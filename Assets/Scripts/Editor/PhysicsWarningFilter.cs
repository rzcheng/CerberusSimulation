using UnityEngine;

// Suppresses the PhysX "large triangles" warning that fires when Cesium bakes
// physics meshes for high-altitude LOD tiles. The warning is benign — physics
// still works correctly for object placement. Remove this file if you ever need
// to debug actual physics mesh issues.
[UnityEditor.InitializeOnLoad]
public static class PhysicsWarningFilter
{
    static PhysicsWarningFilter()
    {
        Application.logMessageReceived += FilterLog;
    }

    static void FilterLog(string message, string stackTrace, LogType type)
    {
        // This fires BEFORE Unity's default handler, so we can't suppress here.
        // The actual suppression is done via the custom log handler below.
    }
}

// Wraps Unity's default log handler to drop the specific PhysX triangle warning.
public class FilteredLogHandler : ILogHandler
{
    readonly ILogHandler defaultHandler;

    public FilteredLogHandler(ILogHandler handler)
    {
        defaultHandler = handler;
    }

    public void LogFormat(LogType logType, Object context, string format, params object[] args)
    {
        if (logType == LogType.Warning && format != null &&
            format.Contains("distance between any 2 vertices"))
            return;

        defaultHandler.LogFormat(logType, context, format, args);
    }

    public void LogException(System.Exception exception, Object context)
    {
        defaultHandler.LogException(exception, context);
    }
}

// Installs the filter on editor load and on every domain reload.
[UnityEditor.InitializeOnLoad]
public static class LogFilterInstaller
{
    static LogFilterInstaller()
    {
        if (Debug.unityLogger.logHandler is not FilteredLogHandler)
            Debug.unityLogger.logHandler = new FilteredLogHandler(Debug.unityLogger.logHandler);
    }
}
