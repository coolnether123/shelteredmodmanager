using System;
using System.IO;
using UnityEngine;

public static class MMLog
{
    private static string _logPath;

    static MMLog()
    {
        var baseDir = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(baseDir);
        _logPath = Path.Combine(baseDir, "mod_manager.log");
    }

    /// <summary>
    /// Writes a line to mod_manager.log with the time it was writen
    /// </summary>
    public static void Write(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }
    }
}