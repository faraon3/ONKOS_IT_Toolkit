using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal static class Program
{
    [STAThread] private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly string _appRoot = AppContext.BaseDirectory;
    private readonly Panel _mainContent = new() { Dock = DockStyle.Fill };
    private Control? _homeBody;
    public MainForm()
    {
        Text = "ONKOS IT Toolkit"; StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 540); Size = new Size(900, 640);
        BackColor = Color.FromArgb(246, 244, 249); Font = new Font("Segoe UI", 9F);
        var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.FromArgb(85, 53, 138) };
        header.Controls.Add(new Label { Text = "ONKOS IT TOOLKIT", ForeColor = Color.White, Font = new Font("Segoe UI", 19F, FontStyle.Bold), AutoSize = true, Location = new Point(18, 12) });
        header.Controls.Add(new Label { Text = "Herramientas locales de soporte técnico", ForeColor = Color.WhiteSmoke, AutoSize = true, Location = new Point(21, 52) });
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), AutoScroll = true, WrapContents = true };
        _homeBody = body;
        AddModule(body, "Backup y migración", "Copiar información antes de formatear.", () => OpenBackup());
        AddModule(body, "Red e IPv4", "Consultar y respaldar la configuración de red.");
        AddModule(body, "Drivers", "Organizar controladores por fabricante y modelo.");
        AddModule(body, "Programas", "Administrar instaladores almacenados en la USB.");
        AddModule(body, "Impresoras", "Organizar controladores y datos de impresoras.");
        AddModule(body, "Inventario", "Consultar y registrar información del equipo.");
        var footer = new Label { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(18, 10, 10, 6), ForeColor = Color.FromArgb(80, 80, 80), AutoEllipsis = true, Text = "Aplicación portable | " + _appRoot };
        _mainContent.Controls.Add(body); Controls.Add(_mainContent); Controls.Add(footer); Controls.Add(header);
        Shown += (_, _) => { foreach (var f in new[] { "Data", "Data/Config", "Data/Programs", "Data/Drivers", "Data/Templates", "Data/Reports" }) Directory.CreateDirectory(Path.Combine(_appRoot, f)); };
    }

    private void OpenBackup()
    {
        var backup = new BackupForm();
        backup.BackRequested += () => { _mainContent.Controls.Clear(); if (_homeBody != null) _mainContent.Controls.Add(_homeBody); };
        _mainContent.Controls.Clear();
        _mainContent.Controls.Add(backup);
    }

    private static void AddModule(Control parent, string title, string description, Action? action = null)
    {
        var card = new Panel { Width = 255, Height = 118, Margin = new Padding(0, 0, 14, 14), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Cursor = action == null ? Cursors.Default : Cursors.Hand };
        var name = new Label { Text = title, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(65, 43, 100), Location = new Point(14, 13), AutoSize = true };
        var desc = new Label { Text = description, Location = new Point(14, 43), Size = new Size(220, 55), ForeColor = Color.FromArgb(75, 75, 75), Cursor = card.Cursor };
        card.Controls.Add(name); card.Controls.Add(desc);
        if (action != null) { card.Click += (_, _) => action(); name.Click += (_, _) => action(); desc.Click += (_, _) => action(); }
        parent.Controls.Add(card);
    }
    private static void AddModule(Control parent, string title, string description) => AddModule(parent, title, description, null);
}