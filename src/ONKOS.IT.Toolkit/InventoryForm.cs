using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal sealed class InventoryForm : UserControl
{
    public event Action? BackRequested;

    private readonly string _reports = Path.Combine(AppContext.BaseDirectory, "Data", "Reports");
    private readonly TextBox _output = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9F) };
    private readonly Button _back = new() { Text = "‹  VOLVER AL INICIO" };
    private readonly Button _refresh = new() { Text = "ACTUALIZAR" };
    private readonly Button _export = new() { Text = "EXPORTAR CSV" };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleLeft };
    private readonly List<Dictionary<string, string>> _rows = new();
    private readonly List<string> _columns = new();

    private static readonly Color Bg = Color.FromArgb(18, 16, 25);
    private static readonly Color Surface = Color.FromArgb(31, 27, 41);
    private static readonly Color Surface2 = Color.FromArgb(39, 34, 51);
    private static readonly Color Purple = Color.FromArgb(105, 73, 164);
    private static readonly Color PurpleHover = Color.FromArgb(126, 91, 190);
    private static readonly Color TextMain = Color.FromArgb(244, 241, 250);
    private static readonly Color TextMuted = Color.FromArgb(183, 174, 199);
    private static readonly Color Border = Color.FromArgb(65, 57, 82);

    public InventoryForm()
    {
        Dock = DockStyle.Fill; BackColor = Bg; ForeColor = TextMain; Font = new Font("Segoe UI", 9.5F);
        Directory.CreateDirectory(_reports);

        var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Purple };
        _back.SetBounds(22, 5, 170, 25); StyleButton(_back, true);
        _back.Click += (_, _) => BackRequested?.Invoke(); header.Controls.Add(_back);
        header.Controls.Add(new Label { Text = "INVENTARIO  /  DIAGNÓSTICO", ForeColor = Color.White, Font = new Font("Segoe UI", 19F, FontStyle.Bold), AutoSize = true, Location = new Point(22, 30) });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12), BackColor = Bg };
        var group = MakeGroup("Información del equipo");
        group.Dock = DockStyle.Fill;

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Surface };
        StyleButton(_refresh, false); StyleButton(_export, true);
        _refresh.SetBounds(10, 6, 160, 38);
        _export.SetBounds(178, 6, 160, 38);
        _refresh.Click += (_, _) => Collect();
        _export.Click += (_, _) => ExportCsv();
        toolbar.Controls.Add(_refresh); toolbar.Controls.Add(_export);
        group.Controls.Add(toolbar);

        _output.Dock = DockStyle.Fill;
        _output.BackColor = Color.FromArgb(13, 12, 18);
        _output.ForeColor = Color.FromArgb(220, 213, 235);
        _output.BorderStyle = BorderStyle.None;
        group.Controls.Add(_output);

        Controls.Add(content); Controls.Add(_status); Controls.Add(header);
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

            _status.Text = $"Información recolectada · {DateTime.Now:HH:mm:ss} · {_rows.Count} registro(s)";
        }
        catch (Exception ex)
        {
            _output.AppendText("ERROR recolectando inventario: " + ex.Message + Environment.NewLine);
            _status.Text = "Error al recolectar inventario";
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

    private void ExportCsv()
    {
        if (_rows.Count == 0) { MessageBox.Show(this, "No hay datos para exportar. Pulsa ACTUALIZAR primero."); return; }
        try
        {
            string file = Path.Combine(_reports, $"Inventario_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", _columns.Select(c => "\"" + c.Replace("\"", "\"\"") + "\"")));
            foreach (var row in _rows)
                sb.AppendLine(string.Join(",", _columns.Select(c => "\"" + (row.TryGetValue(c, out var v) ? v : "").Replace("\"", "\"\"") + "\"")));
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(this, "Inventario exportado:\n" + file, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo exportar: " + ex.Message, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static GroupBox MakeGroup(string title) => new() { Text = title, ForeColor = Color.FromArgb(210, 197, 235), BackColor = Surface, Padding = new Padding(10), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
    private static void StyleButton(Button b, bool secondary)
    {
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Border; b.FlatAppearance.BorderSize = secondary ? 1 : 0;
        b.BackColor = secondary ? Surface2 : Purple; b.ForeColor = TextMain; b.Cursor = Cursors.Hand;
        b.FlatAppearance.MouseOverBackColor = secondary ? Color.FromArgb(52, 45, 67) : PurpleHover;
    }
}
