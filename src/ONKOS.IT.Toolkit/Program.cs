using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal static class Program
{
    [STAThread] private static void Main()
    {
        AppPaths.EnsureStructure();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly Panel _mainContent = new() { Dock = DockStyle.Fill, BackColor = UiColors.Bg };
    private readonly Panel _header;
    private readonly Panel _footer;
    private Control? _homeBody;
    private ProgramManagerForm? _programs;
    private PrinterManagerForm? _printers;
    private BackupForm? _backup;
    private InventoryForm? _inventory;
    private bool _fullScreenMode;

    public MainForm()
    {
        Text = "ONKOS IT Toolkit";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(850, 650);
        Size = new Size(1100, 760);
        BackColor = UiColors.Bg;
        Font = new Font("Segoe UI", 9F);
        KeyPreview = true;

        if (!AdminHelper.IsAdmin())
        {
            var banner = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(180, 130, 20), Padding = new Padding(16, 8, 8, 8) };
            var bannerLabel = new Label
            {
                Text = "⚠  Modo limitado: red, drivers e instaladores requieren administrador.",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            };
            var elevate = new Button
            {
                Text = "REINICIAR COMO ADMIN", Dock = DockStyle.Right, Width = 200,
                FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(120, 80, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand
            };
            elevate.FlatAppearance.BorderSize = 0;
            elevate.Click += (_, _) => { if (AdminHelper.RelaunchAsAdmin()) Application.Exit(); };
            banner.Controls.Add(bannerLabel); banner.Controls.Add(elevate);
            Controls.Add(banner);
        }

        _header = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = UiColors.Purple };
        var brand = new Label { Text = "ONKOS IT TOOLKIT", ForeColor = Color.White, Font = new Font("Segoe UI", 23F, FontStyle.Bold), AutoSize = true, Location = new Point(28, 17) };
        var subtitle = new Label { Text = "Centro de herramientas de soporte técnico", ForeColor = Color.FromArgb(235, 225, 250), AutoSize = true, Location = new Point(32, 64), Font = new Font("Segoe UI", 10F) };
        var tag = new Label { Text = "SISTEMAS  •  CLÍNICA ONKOS", ForeColor = Color.White, BackColor = Color.FromArgb(70, 42, 115), Font = new Font("Segoe UI", 9F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Size = new Size(230, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(Width - 270, 38) };
        _header.Controls.Add(brand); _header.Controls.Add(subtitle); _header.Controls.Add(tag);
        _header.Resize += (_, _) => tag.Left = _header.ClientSize.Width - tag.Width - 28;

        var home = new Panel { Dock = DockStyle.Fill, BackColor = UiColors.Bg, Padding = new Padding(30, 24, 30, 24) };
        var intro = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = UiColors.Bg };
        intro.Controls.Add(new Label { Text = "¿Qué necesitas hacer?", ForeColor = UiColors.TextMain, Font = new Font("Segoe UI", 17F, FontStyle.Bold), AutoSize = true, Location = new Point(2, 0) });
        intro.Controls.Add(new Label { Text = "Selecciona una herramienta para comenzar.", ForeColor = UiColors.TextMuted, Font = new Font("Segoe UI", 10F), AutoSize = true, Location = new Point(4, 39) });

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = UiColors.Bg, Padding = new Padding(0, 4, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        AddModule(grid, 0, 0, "01", "Backup y migración", "Copia documentos y configuraciones antes de formatear o cambiar de equipo.", true, () => OpenBackup());
        AddModule(grid, 1, 0, "02", "Programas", "Organiza instaladores y ejecuta instalaciones en los equipos.", true, () => OpenPrograms());
        AddModule(grid, 0, 1, "03", "Impresoras", "Guarda controladores, personaliza logos y ejecuta instalaciones.", true, () => OpenPrinters());
        AddModule(grid, 1, 1, "04", "Inventario", "Consulta y exporta información técnica del computador.", true, () => OpenInventory());

        home.Controls.Add(grid);
        home.Controls.Add(intro);
        _homeBody = home;

        _footer = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(14, 12, 20) };
        var versionLabel = new Label
        {
            Text = "PORTABLE  •  v" + (typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "1.2.0"),
            ForeColor = UiColors.TextMuted, AutoSize = true, Location = new Point(24, 9),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        };
        _footer.Controls.Add(versionLabel);


        _mainContent.Controls.Add(home);
        Controls.Add(_mainContent);
        Controls.Add(_footer);
        Controls.Add(_header);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F11) { ToggleFullScreen(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape && _fullScreenMode) { ToggleFullScreen(); e.Handled = true; }
        };

        Shown += (_, _) => AppPaths.EnsureStructure();
    }

    private void ToggleFullScreen()
    {
        _fullScreenMode = !_fullScreenMode;
        if (_fullScreenMode)
        {
            _header.Visible = false; _footer.Visible = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
        }
        else
        {
            _header.Visible = true; _footer.Visible = true;
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
            TopMost = false;
        }
    }

    private void ShowHome()
    {
        _mainContent.Controls.Clear();
        if (_homeBody != null) _mainContent.Controls.Add(_homeBody);
        _header.Visible = !_fullScreenMode;
    }

    private void ShowModule(Control module)
    {
        _mainContent.Controls.Clear();
        _mainContent.Controls.Add(module);
        if (!_fullScreenMode) _header.Visible = false;
    }

    private void OpenPrograms() { if (_programs == null) { _programs = new ProgramManagerForm(); _programs.BackRequested += ShowHome; } ShowModule(_programs); }
    private void OpenPrinters() { if (_printers == null) { _printers = new PrinterManagerForm(); _printers.BackRequested += ShowHome; } ShowModule(_printers); }
    private void OpenBackup() { if (_backup == null) { _backup = new BackupForm(); _backup.BackRequested += ShowHome; } ShowModule(_backup); }
    private void OpenInventory() { if (_inventory == null) { _inventory = new InventoryForm(); _inventory.BackRequested += ShowHome; } ShowModule(_inventory); }

    private static void AddModule(TableLayoutPanel grid, int column, int row, string number, string title, string description, bool enabled, Action? action = null)
    {
        var wrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = UiColors.Bg };
        var card = new Panel { Dock = DockStyle.Fill, BackColor = UiColors.Surface, Cursor = enabled ? Cursors.Hand : Cursors.Default };
        var accent = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = enabled ? UiColors.Accent : Color.FromArgb(70, 62, 85) };
        var numberLabel = new Label { Text = number, ForeColor = enabled ? UiColors.Accent : UiColors.TextMuted, Font = new Font("Segoe UI", 10F, FontStyle.Bold), AutoSize = true, Location = new Point(20, 19) };
        var titleLabel = new Label { Text = title, ForeColor = UiColors.TextMain, Font = new Font("Segoe UI", 14F, FontStyle.Bold), AutoSize = true, Location = new Point(20, 46), Cursor = card.Cursor };
        var descLabel = new Label { Text = description, ForeColor = UiColors.TextMuted, Font = new Font("Segoe UI", 10F), Location = new Point(22, 82), Size = new Size(350, 54), Cursor = card.Cursor };
        var status = new Label { Text = enabled ? "ABRIR HERRAMIENTA   →" : "MÓDULO EN PREPARACIÓN", ForeColor = enabled ? UiColors.Accent : Color.FromArgb(120, 111, 139), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), AutoSize = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Location = new Point(22, 145) };
        card.Controls.Add(accent); card.Controls.Add(numberLabel); card.Controls.Add(titleLabel); card.Controls.Add(descLabel); card.Controls.Add(status);
        wrapper.Controls.Add(card);
        grid.Controls.Add(wrapper, column, row);

        if (action != null)
        {
            card.Click += (_, _) => action();
            foreach (Control control in card.Controls)
                if (control != accent) control.Click += (_, _) => action();
        }
        card.Resize += (_, _) => { descLabel.Width = Math.Max(100, card.ClientSize.Width - 44); status.Top = card.ClientSize.Height - 34; };
    }
}
