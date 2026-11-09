using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal sealed class ProgramManagerForm : UserControl
{
    public event Action? BackRequested;
    private readonly string _library = Path.Combine(AppContext.BaseDirectory, "Data", "Programs");
    private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, GridLines = false, CheckBoxes = true, BorderStyle = BorderStyle.None };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _install = new() { Text = "INSTALAR SELECCIONADOS" };
    private readonly Button _add = new() { Text = "+  AGREGAR INSTALADOR" };
    private readonly Button _remove = new() { Text = "ELIMINAR DE BIBLIOTECA" };
    private readonly Button _back = new() { Text = "‹  VOLVER AL INICIO" };
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly TextBox _search = new() { Width = 260, PlaceholderText = "Buscar programa..." };

    private static readonly Color Bg = Color.FromArgb(18, 16, 25);
    private static readonly Color Surface = Color.FromArgb(31, 27, 41);
    private static readonly Color Surface2 = Color.FromArgb(39, 34, 51);
    private static readonly Color Purple = Color.FromArgb(105, 73, 164);
    private static readonly Color PurpleHover = Color.FromArgb(126, 91, 190);
    private static readonly Color TextMain = Color.FromArgb(244, 241, 250);
    private static readonly Color TextMuted = Color.FromArgb(183, 174, 199);
    private static readonly Color Border = Color.FromArgb(65, 57, 82);

    public ProgramManagerForm()
    {
        Dock = DockStyle.Fill; BackColor = Bg; ForeColor = TextMain; Font = new Font("Segoe UI", 9.5F);
        Directory.CreateDirectory(_library);

        var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Purple };
        _back.SetBounds(22, 5, 170, 25); StyleButton(_back, true);
        _back.Click += (_, _) => BackRequested?.Invoke();
        header.Controls.Add(_back);
        header.Controls.Add(new Label { Text = "PROGRAMAS  /  INSTALADORES", ForeColor = Color.White, Font = new Font("Segoe UI", 19F, FontStyle.Bold), AutoSize = true, Location = new Point(22, 30) });
        header.Controls.Add(new Label { Text = "Biblioteca local de instaladores · Clínica ONKOS", ForeColor = Color.FromArgb(235, 225, 250), AutoSize = true, Location = new Point(25, 66) });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12), AutoScroll = true, BackColor = Bg };
        var libraryGroup = MakeGroup("01  ·  Biblioteca de programas");
        libraryGroup.Dock = DockStyle.Fill;

        _category.Items.AddRange(new object[] { "Todos", "Navegadores", "Ofimática", "PDF", "Compresión", "Diagnóstico", "Seguridad", "Utilidades", "Otros" });
        _category.SelectedIndex = 0;
        StyleCombo(_category); _category.Height = 32;
        StyleTextBox(_search); _search.Height = 32;

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Surface };
        _category.SetBounds(8, 7, 190, 32); _search.SetBounds(208, 7, 300, 32);
        toolbar.Controls.Add(_category); toolbar.Controls.Add(_search);

        _list.BackColor = Surface; _list.ForeColor = TextMain; _list.Font = new Font("Segoe UI", 9.5F);
        _list.Columns.Add("Programa / instalador", 300);
        _list.Columns.Add("Tipo", 75);
        _list.Columns.Add("Tamaño", 100);
        _list.Columns.Add("Ubicación", 500);
        _list.Resize += (_, _) => { if (_list.Columns.Count >= 4) _list.Columns[3].Width = Math.Max(180, _list.ClientSize.Width - _list.Columns[0].Width - _list.Columns[1].Width - _list.Columns[2].Width - 4); };

        libraryGroup.Controls.Add(_list);
        libraryGroup.Controls.Add(toolbar);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Surface };
        _status.ForeColor = TextMuted; _status.Padding = new Padding(8, 0, 0, 0);
        statusPanel.Controls.Add(_status);

        var actionBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Bg, Padding = new Padding(3, 2, 3, 2) };
        _add.Height = _remove.Height = _install.Height = 46;
        StyleButton(_add, false); StyleButton(_remove, true); StyleButton(_install, false);
        _add.Font = _remove.Font = _install.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        void LayoutActions()
        {
            int width = actionBar.ClientSize.Width;
            int third = Math.Max(1, (width - 6) / 3);
            _add.SetBounds(0, 0, third, actionBar.ClientSize.Height);
            _remove.SetBounds(third + 3, 0, third, actionBar.ClientSize.Height);
            _install.SetBounds((third * 2) + 6, 0, Math.Max(1, width - (third * 2) - 6), actionBar.ClientSize.Height);
        }
        actionBar.Resize += (_, _) => LayoutActions(); LayoutActions();
        actionBar.Controls.Add(_add); actionBar.Controls.Add(_remove); actionBar.Controls.Add(_install);

        content.Controls.Add(libraryGroup);
        Controls.Add(content); Controls.Add(statusPanel); Controls.Add(actionBar); Controls.Add(header);

        _add.Click += (_, _) => AddInstallers();
        _remove.Click += (_, _) => RemoveSelected();
        _install.Click += async (_, _) => await InstallSelectedAsync();
        _search.TextChanged += (_, _) => RefreshList();
        _category.SelectedIndexChanged += (_, _) => RefreshList();
        RefreshList();
    }

    private GroupBox MakeGroup(string title) => new() { Text = title, ForeColor = Color.FromArgb(210, 197, 235), BackColor = Surface, Padding = new Padding(10), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10), FlatStyle = FlatStyle.Flat };
    private static void StyleButton(Button b, bool secondary)
    {
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Border; b.FlatAppearance.BorderSize = secondary ? 1 : 0;
        b.BackColor = secondary ? Surface2 : Purple; b.ForeColor = TextMain; b.Cursor = Cursors.Hand;
        b.FlatAppearance.MouseOverBackColor = secondary ? Color.FromArgb(52, 45, 67) : PurpleHover;
    }
    private static void StyleCombo(ComboBox c) { c.BackColor = Surface2; c.ForeColor = TextMain; c.FlatStyle = FlatStyle.Flat; }
    private static void StyleTextBox(TextBox t) { t.BackColor = Surface2; t.ForeColor = TextMain; t.BorderStyle = BorderStyle.FixedSingle; t.Font = new Font("Segoe UI", 10F); }

    private void AddInstallers()
    {
        using var dialog = new OpenFileDialog { Title = "Selecciona instaladores", Filter = "Instaladores (*.exe;*.msi)|*.exe;*.msi|Todos los archivos (*.*)|*.*", Multiselect = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        int added = 0;
        foreach (string source in dialog.FileNames)
        {
            try
            {
                string destination = Path.Combine(_library, Path.GetFileName(source));
                if (File.Exists(destination))
                {
                    string name = Path.GetFileNameWithoutExtension(source);
                    string ext = Path.GetExtension(source);
                    destination = Path.Combine(_library, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                File.Copy(source, destination); added++;
            }
            catch (Exception ex) { MessageBox.Show(this, $"No se pudo copiar {Path.GetFileName(source)}:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        _status.Text = $"Agregados {added} instalador(es) a la biblioteca local.";
        RefreshList();
    }

    private void RemoveSelected()
    {
        var selected = _list.SelectedItems.Cast<ListViewItem>().ToList();
        if (selected.Count == 0) return;
        if (MessageBox.Show(this, $"¿Eliminar {selected.Count} instalador(es) de la biblioteca?\nNo se desinstalarán programas del computador.", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        foreach (var item in selected) { try { if (File.Exists(item.Tag as string)) File.Delete((string)item.Tag!); } catch { } }
        RefreshList();
    }

    private async Task InstallSelectedAsync()
    {
        var selected = _list.Items.Cast<ListViewItem>().Where(i => i.Checked).ToList();
        if (selected.Count == 0) { MessageBox.Show(this, "Marca con la casilla los instaladores que deseas ejecutar.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (MessageBox.Show(this, $"Se ejecutarán {selected.Count} instalador(es), uno a la vez.\n\nLos asistentes pueden requerir intervención y permisos de administrador. ¿Continuar?", "Instalación secuencial", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _add.Enabled = _remove.Enabled = _install.Enabled = false;
        int ok = 0, failed = 0;
        try
        {
            foreach (var item in selected)
            {
                string path = (string)item.Tag!;
                _status.Text = $"Ejecutando: {Path.GetFileName(path)}";
                try
                {
                    var psi = new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "runas" };
                    using var process = Process.Start(psi);
                    if (process == null) { failed++; continue; }
                    await Task.Run(() => process.WaitForExit());
                    if (process.ExitCode == 0) ok++;
                    else { failed++; MessageBox.Show(this, $"{Path.GetFileName(path)} finalizó con código {process.ExitCode}.", "Instalador", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
                catch (Exception ex) { failed++; MessageBox.Show(this, $"No se pudo ejecutar {Path.GetFileName(path)}:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
        finally { _add.Enabled = _remove.Enabled = _install.Enabled = true; }
        _status.Text = $"Proceso terminado. Finalizados con código 0: {ok}; con error/cancelados: {failed}.";
    }

    private void RefreshList()
    {
        _list.BeginUpdate(); _list.Items.Clear();
        string query = _search.Text.Trim();
        foreach (string file in Directory.GetFiles(_library).Where(f => Path.GetExtension(f).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(f).Equals(".msi", StringComparison.OrdinalIgnoreCase)).OrderBy(Path.GetFileName))
        {
            string name = Path.GetFileName(file);
            if (!name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            var info = new FileInfo(file);
            var item = new ListViewItem(name) { Tag = file };
            item.SubItems.Add(info.Extension.TrimStart('.').ToUpperInvariant());
            item.SubItems.Add(info.Length < 1024 * 1024 ? $"{info.Length / 1024.0:0} KB" : $"{info.Length / 1024.0 / 1024.0:0.0} MB");
            item.SubItems.Add(file); _list.Items.Add(item);
        }
        _list.EndUpdate();
        _status.Text = $"{_list.Items.Count} instalador(es) en biblioteca. Carpeta: {_library}";
    }
}
