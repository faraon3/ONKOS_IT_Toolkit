using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace ONKOS.IT.Toolkit;

internal sealed class BackupState
{
    public string RootPath { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, FileState> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static string FilePath(string root) => Path.Combine(root, ".onkos-state.json");

    public static BackupState Load(string root)
    {
        try
        {
            var p = FilePath(root);
            if (!File.Exists(p)) return new BackupState { RootPath = root, StartedAt = DateTime.Now };
            var json = File.ReadAllText(p);
            return JsonSerializer.Deserialize<BackupState>(json) ?? new BackupState { RootPath = root, StartedAt = DateTime.Now };
        }
        catch { return new BackupState { RootPath = root, StartedAt = DateTime.Now }; }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(RootPath);
            File.WriteAllText(FilePath(RootPath), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static string ComputeHash(string file)
    {
        try
        {
            using var sha = SHA256.Create();
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
        catch { return ""; }
    }
}

internal sealed class FileState
{
    public long Size { get; set; }
    public string Hash { get; set; } = "";
    public DateTime CopiedAt { get; set; }
}
