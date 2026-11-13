using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

/// <summary>
/// Resuelve la carpeta raíz de datos. Por defecto vive junto al .exe (portable clásico),
/// pero el técnico puede elegir otra ruta (ej. E:\ONKOS_Data\) la primera vez.
/// La configuración se guarda en %APPDATA%\ONKOS\config.json (solo 1 archivo, no los datos).
/// </summary>
internal static class AppPaths
{
    private static string? _dataRoot;
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ONKOS");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    /// <summary>Carpeta raíz de datos. Si el técnico no eligió nada, es AppContext.BaseDirectory.</summary>
    public static string DataRoot
    {
        get
        {
            if (_dataRoot == null) _dataRoot = LoadDataRoot();
            return _dataRoot;
        }
    }

    public static string DriversDir => Path.Combine(DataRoot, "Data", "Drivers");
    public static string ProgramsDir => Path.Combine(DataRoot, "Data", "Programs");
    public static string ReportsDir => Path.Combine(DataRoot, "Data", "Reports");
    public static string TemplatesDir => Path.Combine(DataRoot, "Data", "Templates");
    public static string ConfigDataDir => Path.Combine(DataRoot, "Data", "Config");

    /// <summary>Carpeta donde se guardarán los backups (configurable por el técnico).</summary>
    public static string BackupsDir
    {
        get
        {
            var cfg = LoadConfig();
            if (!string.IsNullOrWhiteSpace(cfg?.BackupDestination) && Directory.Exists(cfg.BackupDestination))
                return cfg.BackupDestination!;
            return DataRoot;
        }
    }

    private static string LoadDataRoot()
    {
        try
        {
            var cfg = LoadConfig();
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.DataRoot) && Directory.Exists(cfg.DataRoot))
                return cfg.DataRoot!;
        }
        catch { }
        return AppContext.BaseDirectory;
    }

    public static bool IsDefaultDataRoot => string.Equals(
        Path.GetFullPath(DataRoot).TrimEnd('\\'),
        Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\'),
        StringComparison.OrdinalIgnoreCase);

    public static void SetDataRoot(string newRoot)
    {
        if (string.IsNullOrWhiteSpace(newRoot)) return;
        newRoot = Path.GetFullPath(newRoot);
        Directory.CreateDirectory(newRoot);

        var cfg = LoadConfig() ?? new AppConfig();
        cfg.DataRoot = newRoot;
        SaveConfig(cfg);
        _dataRoot = newRoot;
        EnsureStructure();
    }

    public static void SetBackupDestination(string? path)
    {
        var cfg = LoadConfig() ?? new AppConfig();
        cfg.BackupDestination = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        SaveConfig(cfg);
    }

    /// <summary>Crea las subcarpetas estándar si no existen.</summary>
    public static void EnsureStructure()
    {
        foreach (var dir in new[] { "Data", "Data/Config", "Data/Programs", "Data/Drivers", "Data/Templates", "Data/Reports" })
        {
            try { Directory.CreateDirectory(Path.Combine(DataRoot, dir)); } catch { }
        }
    }

    /// <summary>
    /// Migra una instalación vieja. Si la carpeta elegida no tiene Data y la carpeta del .exe sí,
    /// copia los archivos automáticamente para que el técnico no pierda nada.
    /// </summary>
    public static void MigrateIfNeeded()
    {
        try
        {
            string exeData = Path.Combine(AppContext.BaseDirectory, "Data");
            string targetData = Path.Combine(DataRoot, "Data");
            if (!Directory.Exists(exeData) || !Directory.Exists(targetData)) return;
            if (string.Equals(Path.GetFullPath(exeData).TrimEnd('\\'),
                              Path.GetFullPath(targetData).TrimEnd('\\'),
                              StringComparison.OrdinalIgnoreCase)) return;

            CopyDirectorySafe(exeData, targetData);
        }
        catch { }
    }

    private static void CopyDirectorySafe(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            try
            {
                string dest = Path.Combine(target, Path.GetFileName(file));
                if (!File.Exists(dest)) File.Copy(file, dest);
            }
            catch { }
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            try { CopyDirectorySafe(dir, Path.Combine(target, Path.GetFileName(dir))); } catch { }
        }
    }

    private static AppConfig? LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigFile)) return null;
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigFile));
        }
        catch { return null; }
    }

    private static void SaveConfig(AppConfig cfg)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary>
    /// Diálogo que el técnico ve al pulsar "CAMBIAR CARPETA DE DATOS".
    /// Permite elegir una carpeta (USB, disco externo, red) y la guarda como raíz.
    /// </summary>
    public static bool PromptForDataRoot(IWin32Window? owner = null)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta donde se guardarán TODOS los datos de ONKOS: drivers, programas, inventarios, reportes y logs. Se recomienda una carpeta en la USB o disco externo.",
            SelectedPath = DataRoot,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return false;

        try
        {
            SetDataRoot(dialog.SelectedPath);
            MigrateIfNeeded();
            OnkosMessageBox.Show(owner,
                $"Carpeta de datos configurada:\n{dialog.SelectedPath}\n\nLos drivers, programas e inventarios ya se guardarán aquí.",
                "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            OnkosMessageBox.Show(owner, "No se pudo configurar la carpeta: " + ex.Message, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private sealed class AppConfig
    {
        public string? DataRoot { get; set; }
        public string? BackupDestination { get; set; }
    }
}
