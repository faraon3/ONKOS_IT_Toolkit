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
    private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, GridLines = true, CheckBoxes = true };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 34, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _install = new() { Text = "INSTALAR SELECCIONADOS", Width = 220, Height = 38 };
    private readonly Button _add = new() { Text = "+ AGREGAR INSTALADOR", Width = 190, Height = 38 };
    private readonly Button _remove = new() { Text = "ELIMINAR DE BIBLIOTECA", Width = 190, Height = 38 };
    private readonly Button _back = new() { Text = "← VOLVER", Width = 110, Height = 38 };
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly TextBox _search = new() { Width = 230, PlaceholderText = "Buscar programa..." };

    private static readonly Color Bg = Color.FromArgb(18, 16, 25);
    private static readonly Color Card = Color.FromArgb(31, 27, 41);
    private static readonly Color Purple = Color.FromArgb(85, 53, 138);
    private static readonly Color Accent = Color.FromArgb(153, 117, 214);
    private static readonly Color MainText = Color.FromArgb(244, 241, 250);
    private static readonly Color Muted = Color.FromArgb(183, 174, 199);

    public ProgramManagerForm()
    {
        Dock = DockStyle.Fill;
        BackColor = Bg;
        Padding = new Padding(24);
        Directory.CreateDirectory(_library);

        var heading = new Label { Text = "GESTOR DE PROGRAMAS", Dock = DockStyle.Top, Height = 44, ForeColor = MainText, Font = new Font("Segoe UI", 20, FontStyle.Bold) };
        var subtitle = new Label { Text = "Biblioteca local de instaladores para equipos ONKOS", Dock = DockStyle.Top, Height = 30, ForeColor = Muted, Font = new Font("Segoe UI", 10) };

        _category.Items.AddRange(new object[] { "Todos", "Navegadores", "Ofimática", "PDF", "Compresión", "Diagnóstico", "Seguridad", "Utilidades", "Otros" });
        _category.SelectedIndex = 0;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, BackColor = Bg, WrapContents = false, Padding = new Padding(0, 7, 0, 0) };
        toolbar.Controls.Add(_category); toolbar.Controls.Add(_search);

        _list.BackColor = Card; _list.ForeColor = MainText; _list.Font = new Font("Segoe UI", 9.5F);
        _list.Columns.Add("Programa / instalador", 320);
        _list.Columns.Add("Tipo", 80);
        _list.Columns.Add("Tamaño", 100);
        _list.Columns.Add("Ubicación", 450);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, BackColor = Bg, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
        Style(_add, Purple); Style(_remove, Card); Style(_install, Color.FromArgb(67, 120, 83)); Style(_back, Card);
        buttons.Controls.Add(_add); buttons.Controls.Add(_remove); buttons.Controls.Add(_install); buttons.Controls.Add(_back);

        Controls.Add(_list); Controls.Add(toolbar); Controls.Add(subtitle); Controls.Add(heading); Controls.Add(buttons); Controls.Add(_status);

        _add.Click += (_, _) => AddInstallers();
        _remove.Click += (_, _) => RemoveSelected();
        _install.Click += async (_, _) => await InstallSelectedAsync();
        _back.Click += (_, _) => BackRequested?.Invoke();
        _search.TextChanged += (_, _) => RefreshList();
        _category.SelectedIndexChanged += (_, _) => RefreshList();
        RefreshList();
    }

    private static void Style(Button b, Color color)
    {
        b.BackColor = color; b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0; b.Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }

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
                File.Copy(source, destination);
                added++;
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
        foreach (var item in selected)
        {
            try { if (File.Exists(item.Tag as string)) File.Delete((string)item.Tag!); } catch { }
        }
        RefreshList();
    }

    private async Task InstallSelectedAsync()
    {
        var selected = _list.Items.Cast<ListViewItem>().Where(i => i.Checked).ToList();
        if (selected.Count == 0) { MessageBox.Show(this, "Marca con la casilla los instaladores que deseas ejecutar.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (MessageBox.Show(this, $"Se ejecutarán {selected.Count} instalador(es), uno a la vez.\n\nLos asistentes de instalación pueden requerir intervención y permisos de administrador. ¿Continuar?", "Instalación secuencial", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

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
            item.SubItems.Add(file);
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        _status.Text = $"{_list.Items.Count} instalador(es) en biblioteca. Carpeta: {_library}";
    }
}
