using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ONKOS.IT.Toolkit;

internal sealed class InventoryForm : UserControl
{
    public event Action? BackRequested;

    private readonly string _reports = AppPaths.ReportsDir;
    private readonly TextBox _output = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9F) };
    private readonly Button _refresh = new() { Text = "↻   ACTUALIZAR" };
    private readonly Button _export = new() { Text = "💾   EXPORTAR CSV" };
    private readonly Button _copy = new() { Text = "📋   COPIAR RESUMEN" };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleLeft };
    private readonly List<Dictionary<string, string>> _rows = new();
    private readonly List<string> _columns = new();
    private string _lastTextReport = "";

    public InventoryForm()
    {
        Dock = DockStyle.Fill; BackColor = OnkosTheme.Bg; ForeColor = OnkosTheme.TextMain; Font = new Font("Segoe UI", 9.5F);
        Directory.CreateDirectory(_reports);

        // ── HEADER con helper del tema ──
        var header = OnkosTheme.CreateHeader(
            "Inventario y diagnóstico",
            "Recolecta información del equipo para diagnóstico rápido",
            () => BackRequested?.Invoke());

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12), BackColor = OnkosTheme.Bg };
        var group = OnkosTheme.MakeGroup("Información del equipo");
        group.Dock = DockStyle.Fill;

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = OnkosTheme.Surface };
        OnkosTheme.StyleButton(_refresh, ButtonKind.Primary);
        OnkosTheme.StyleButton(_copy, ButtonKind.Secondary);
        OnkosTheme.StyleButton(_export, ButtonKind.Secondary);
        _refresh.SetBounds(10, 6, 150, 38);
        _copy.SetBounds(168, 6, 170, 38);
        _export.SetBounds(346, 6, 170, 38);
        _refresh.Click += (_, _) => Collect();
        _copy.Click += (_, _) => CopyToClipboard();
        _export.Click += (_, _) => ExportCsv();
        toolbar.Controls.Add(_refresh); toolbar.Controls.Add(_copy); toolbar.Controls.Add(_export);
        group.Controls.Add(toolbar);

        _output.Dock = DockStyle.Fill;
        _output.BackColor = OnkosTheme.LogBg;
        _output.ForeColor = OnkosTheme.LogFg;
        _output.BorderStyle = BorderStyle.None;
        group.Controls.Add(_output);

        _status.ForeColor = OnkosTheme.TextMuted;
        _status.Padding = new Padding(12, 0, 0, 0);

        // ── BARRA DE ESTADO INFERIOR ──
        var statusBar = OnkosTheme.CreateStatusBar("Inventario");

        Controls.Add(content); Controls.Add(_status); Controls.Add(statusBar); Controls.Add(header);
        content.Controls.Add(group);

        Load += (_, _) => Collect();
    }

    private void Collect()
    {
        _rows.Clear(); _columns.Clear(); _output.Clear();
        _status.Text = "Recolectando información del sistema…";
        try
        {
            CollectSection("Sistema operativo", new (string Col, string Query, string Key)[]
            {
                ("Caption",       "SELECT Caption FROM Win32_OperatingSystem", "Caption"),
                ("Version",       "SELECT Version FROM Win32_OperatingSystem", "Version"),
                ("BuildNumber",   "SELECT BuildNumber FROM Win32_OperatingSystem", "BuildNumber"),
                ("OSArchitecture","SELECT OSArchitecture FROM Win32_OperatingSystem", "OSArchitecture"),
                ("InstallDate",   "SELECT InstallDate FROM Win32_OperatingSystem", "InstallDate"),
                ("LastBootTime",  "SELECT LastBootUpTime FROM Win32_OperatingSystem", "LastBootUpTime"),
                ("RegisteredUser","SELECT RegisteredUser FROM Win32_OperatingSystem", "RegisteredUser"),
            });
            CollectSection("Equipo", new (string, string, string)[]
            {
                ("Nombre",     "SELECT Name FROM Win32_ComputerSystem", "Name"),
                ("Fabricante", "SELECT Manufacturer FROM Win32_ComputerSystem", "Manufacturer"),
                ("Modelo",     "SELECT Model FROM Win32_ComputerSystem", "Model"),
                ("Dominio",    "SELECT Domain FROM Win32_ComputerSystem", "Domain"),
                ("RAM_GB",     "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem", "TotalPhysicalMemory"),
                ("Usuario",    "SELECT UserName FROM Win32_ComputerSystem", "UserName"),
            });
            CollectSection("Procesador", new (string, string, string)[]
            {
                ("Nombre",      "SELECT Name FROM Win32_Processor", "Name"),
                ("Nucleos",     "SELECT NumberOfCores FROM Win32_Processor", "NumberOfCores"),
                ("Hilos",       "SELECT NumberOfLogicalProcessors FROM Win32_Processor", "NumberOfLogicalProcessors"),
                ("VelocidadMHz","SELECT MaxClockSpeed FROM Win32_Processor", "MaxClockSpeed"),
            });
            CollectSection("Discos", new (string, string, string)[]
            {
                ("Modelo",   "SELECT Model FROM Win32_DiskDrive", "Model"),
                ("Tamaño_GB","SELECT Size FROM Win32_DiskDrive", "Size"),
                ("Interfaz", "SELECT InterfaceType FROM Win32_DiskDrive", "InterfaceType"),
                ("Serial",   "SELECT SerialNumber FROM Win32_DiskDrive", "SerialNumber"),
            });
            CollectSection("Red", new (string, string, string)[]
            {
                ("Adaptador", "SELECT Name FROM Win32_NetworkAdapter WHERE NetEnabled=True", "Name"),
                ("MAC",       "SELECT MACAddress FROM Win32_NetworkAdapter WHERE NetEnabled=True", "MACAddress"),
                ("Velocidad", "SELECT Speed FROM Win32_NetworkAdapter WHERE NetEnabled=True", "Speed"),
            });
            CollectSection("Impresoras instaladas", new (string, string, string)[]
            {
                ("Nombre",  "SELECT Name FROM Win32_Printer", "Name"),
                ("Driver",  "SELECT DriverName FROM Win32_Printer", "DriverName"),
                ("Puerto",  "SELECT PortName FROM Win32_Printer", "PortName"),
                ("Default", "SELECT Default FROM Win32_Printer", "Default"),
            });
            CollectSoftware();
            BuildTextReport();
            _status.Text = $"Información recolectada · {DateTime.Now:HH:mm:ss} · {_rows.Count} registro(s)";
            ActivityLog.Write("INVENTORY", "Recolectado", "OK", $"{_rows.Count} registros");
        }
        catch (Exception ex)
        {
            _output.AppendText("ERROR recolectando inventario: " + ex.Message + Environment.NewLine);
            _status.Text = "Error al recolectar inventario";
        }
    }

    private void CollectSoftware()
    {
        _output.AppendText($"### Software instalado{Environment.NewLine}");
        try
        {
            var programs = new List<(string Name, string Version, string Publisher)>();

            void ReadUninstallKey(RegistryKey root, string subkey)
            {
                try
                {
                    using var uninstall = root.OpenSubKey(subkey);
                    if (uninstall == null) return;
                    foreach (var name in uninstall.GetSubKeyNames())
                    {
                        try
                        {
                            using var app = uninstall.OpenSubKey(name);
                            if (app == null) continue;
                            var display = app.GetValue("DisplayName")?.ToString();
                            if (string.IsNullOrWhiteSpace(display)) continue;
                            var version = app.GetValue("DisplayVersion")?.ToString() ?? "";
                            var publisher = app.GetValue("Publisher")?.ToString() ?? "";
                            programs.Add((display!, version, publisher));
                        }
                        catch { }
                    }
                }
                catch { }
            }

            try
            {
                using var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                ReadUninstallKey(hklm64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            }
            catch { }
            try
            {
                using var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                ReadUninstallKey(hklm32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            }
            catch { }
            try
            {
                using var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
                ReadUninstallKey(hkcu, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            }
            catch { }

            var unique = programs.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var p in unique)
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Programa"] = p.Name,
                    ["Version"] = p.Version,
                    ["Fabricante"] = p.Publisher
                };
                _rows.Add(row);
                foreach (var col in new[] { "Programa", "Version", "Fabricante" })
                    if (!_columns.Contains(col)) _columns.Add(col);
                _output.AppendText($"  {p.Name}  |  {p.Version}  |  {p.Publisher}{Environment.NewLine}");
            }
            if (unique.Count == 0) _output.AppendText("  [sin programas detectados]" + Environment.NewLine);
            _output.AppendText(Environment.NewLine);
        }
        catch (Exception ex)
        {
            _output.AppendText("  [software no disponible: " + ex.Message + "]" + Environment.NewLine + Environment.NewLine);
        }
    }

    private void CollectSection(string name, (string Col, string Query, string Key)[] fields)
    {
        _output.AppendText($"### {name}{Environment.NewLine}");
        try
        {
            var sectionRows = new List<Dictionary<string, string>>();
            foreach (var f in fields)
            {
                int i = 0;
                using var searcher = new ManagementObjectSearcher(f.Query);
                foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                {
                    if (sectionRows.Count <= i) sectionRows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                    object? val = null;
                    try { val = obj[f.Key]; } catch { }
                    sectionRows[i][f.Col] = FormatValue(f.Col, val);
                    i++;
                }
            }
            foreach (var row in sectionRows)
            {
                foreach (var col in fields.Select(f => f.Col))
                    if (!_columns.Contains(col)) _columns.Add(col);
                _rows.Add(row);
            }
            foreach (var row in sectionRows)
            {
                foreach (var f in fields)
                {
                    if (row.TryGetValue(f.Col, out var v) && !string.IsNullOrWhiteSpace(v))
                        _output.AppendText($"  {f.Col,-16}: {v}{Environment.NewLine}");
                }
                _output.AppendText(Environment.NewLine);
            }
            if (sectionRows.Count == 0)
                _output.AppendText("  [sin datos disponibles]" + Environment.NewLine + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _output.AppendText("  [no disponible: " + ex.Message + "]" + Environment.NewLine + Environment.NewLine);
        }
    }

    private static string FormatValue(string col, object? val)
    {
        if (val == null) return "";
        if (col.EndsWith("_GB") && long.TryParse(val.ToString(), out var bytes)) return (bytes / 1024.0 / 1024 / 1024).ToString("F1");
        if (col is "InstallDate" or "LastBootTime")
        {
            try { return ManagementDateTimeConverter.ToDateTime(val.ToString()).ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
        }
        return val.ToString() ?? "";
    }

    private void BuildTextReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"INVENTARIO ONKOS — {Environment.MachineName}");
        sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Usuario: {Environment.UserName}");
        sb.AppendLine($"Equipo: {Environment.MachineName}");
        sb.AppendLine($"Sistema: {Environment.OSVersion}");
        sb.AppendLine();
        sb.Append(_output.Text);
        _lastTextReport = sb.ToString();
    }

    private void CopyToClipboard()
    {
        if (string.IsNullOrWhiteSpace(_lastTextReport))
        {
            OnkosMessageBox.Show(this, "No hay datos. Pulsa ACTUALIZAR primero.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            Clipboard.SetText(_lastTextReport);
            _status.Text = "Resumen copiado al portapapeles.";
            OnkosMessageBox.Show(this, "Resumen copiado al portapapeles.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            OnkosMessageBox.Show(this, "No se pudo copiar: " + ex.Message, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportCsv()
    {
        if (_rows.Count == 0) { OnkosMessageBox.Show(this, "No hay datos para exportar. Pulsa ACTUALIZAR primero."); return; }
        try
        {
            string file = Path.Combine(_reports, $"Inventario_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", _columns.Select(c => "\"" + c.Replace("\"", "\"\"") + "\"")));
            foreach (var row in _rows)
                sb.AppendLine(string.Join(",", _columns.Select(c => "\"" + (row.TryGetValue(c, out var v) ? v : "").Replace("\"", "\"\"") + "\"")));
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            ActivityLog.Write("INVENTORY", "Export CSV", "OK", file);
            OnkosMessageBox.Show(this, "Inventario exportado:\n" + file, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            OnkosMessageBox.Show(this, "No se pudo exportar: " + ex.Message, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
