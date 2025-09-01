using System;
using System.IO;
using UnityEngine;

public static class MMLog
{
    private static string _logPath;

    static MMLog()
    {
        try
        {
            var baseDir = Application.persistentDataPath;
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(baseDir);
            _logPath = Path.Combine(baseDir, "mod_manager.log");
        }
        catch
        {
            _logPath = Path.Combine(Directory.GetCurrentDirectory(), "mod_manager.log");
        }
    }

    public static void Write(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }
    }
}