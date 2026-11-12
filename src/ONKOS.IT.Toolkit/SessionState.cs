using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ONKOS.IT.Toolkit;

internal sealed class SessionState
{
    public List<string> SelectedPaths { get; set; } = new();
    public string? Destination { get; set; }
    public string? BackupLabel { get; set; }
    public bool SaveIpv4 { get; set; } = true;
    public bool SaveDrivers { get; set; } = true;

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "Data", "Config", "last-session.json");

    public static SessionState Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new SessionState();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<SessionState>(json) ?? new SessionState();
        }
        catch { return new SessionState(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
    }
}
