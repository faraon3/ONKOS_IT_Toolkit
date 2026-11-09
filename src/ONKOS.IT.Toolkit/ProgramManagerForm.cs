using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal sealed class ProgramManagerForm : UserControl
{
    public event Action? BackRequested;
    private readonly string _library = Path.Combine(AppContext.BaseDirectory, "Data", "Programs");
    private readonly string _logos = Path.Combine(AppContext.BaseDirectory, "Data", "Programs", ".logos");
    private readonly string _metadataPath = Path.Combine(AppContext.BaseDirectory, "Data", "Programs", "programs.json");
    private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, GridLines = false, CheckBoxes = false, BorderStyle = BorderStyle.None, OwnerDraw = true, HideSelection = false };
    private readonly ImageList _images = new() { ImageSize = new Size(44, 44), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 25, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ModernProgressBar _progressBar = new() { Dock = DockStyle.Top, Height = 28, Value = 0 };
    private readonly Label _progressCaption = new() { Dock = DockStyle.Top, Height = 22, Text = "Listo para comenzar", TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _install = new() { Text = "INSTALAR SELECCIONADOS" };
    private readonly Button _add = new() { Text = "+  AGREGAR INSTALADOR" };
    private readonly Button _remove = new() { Text = "ELIMINAR DE BIBLIOTECA" };
    private readonly Button _back = new() { Text = "‹  VOLVER AL INICIO" };
    private readonly TextBox _search = new() { Width = 300, PlaceholderText = "Buscar programa..." };
    private readonly Button _clearSearch = new() { Text = "×", Width = 34, Height = 32, Font = new Font("Segoe UI", 16F, FontStyle.Bold) };
    private readonly Dictionary<string, ProgramMeta> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _checkedPaths = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Color Bg = Color.FromArgb(18, 16, 25);
    private static readonly Color Surface = Color.FromArgb(31, 27, 41);
    private static readonly Color Surface2 = Color.FromArgb(39, 34, 51);
    private static readonly Color Purple = Color.FromArgb(105, 73, 164);
    private static readonly Color PurpleHover = Color.FromArgb(126, 91, 190);
    private static readonly Color TextMain = Color.FromArgb(244, 241, 250);
    private static readonly Color TextMuted = Color.FromArgb(183, 174, 199);
    private static readonly Color Border = Color.FromArgb(65, 57, 82);

    private sealed class ProgramMeta { public string? DisplayName { get; set; } public string? LogoPath { get; set; } }

    public ProgramManagerForm()
    {
        Dock = DockStyle.Fill; BackColor = Bg; ForeColor = TextMain; Font = new Font("Segoe UI", 9.5F);
        Directory.CreateDirectory(_library); Directory.CreateDirectory(_logos); LoadMetadata();

        var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Purple };
        _back.SetBounds(22, 5, 170, 25); StyleButton(_back, true);
        _back.Click += (_, _) => BackRequested?.Invoke(); header.Controls.Add(_back);
        header.Controls.Add(new Label { Text = "PROGRAMAS  /  INSTALADORES", ForeColor = Color.White, Font = new Font("Segoe UI", 19F, FontStyle.Bold), AutoSize = true, Location = new Point(22, 30) });
        header.Controls.Add(new Label { Text = "Biblioteca local de instaladores · Clínica ONKOS", ForeColor = Color.FromArgb(235, 225, 250), AutoSize = true, Location = new Point(25, 66) });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12), BackColor = Bg };
        var group = MakeGroup("01  ·  Biblioteca de programas"); group.Dock = DockStyle.Fill;

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Surface };

        // Buscador integrado en un único contenedor visual.
        var searchBox = new Panel { Bounds = new Rectangle(10, 6, 390, 36), BackColor = Surface2 };
        searchBox.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(_search.Focused ? PurpleHover : Border, _search.Focused ? 1.8F : 1.2F);
            using var outline = RoundedRect(new Rectangle(1, 1, searchBox.Width - 3, searchBox.Height - 3), 8);
            e.Graphics.DrawPath(pen, outline);
        };
        searchBox.Resize += (_, _) => searchBox.Invalidate();
        var searchIcon = new Label { Text = "⌕", Font = new Font("Segoe UI", 21F, FontStyle.Bold), ForeColor = PurpleHover, TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(8, 0, 34, 34), BackColor = Color.Transparent };
        _search.BorderStyle = BorderStyle.None; _search.BackColor = Surface2; _search.ForeColor = TextMain; _search.Font = new Font("Segoe UI", 10F);
        _search.SetBounds(43, 8, 300, 20);
        _clearSearch.FlatStyle = FlatStyle.Flat; _clearSearch.FlatAppearance.BorderSize = 0; _clearSearch.BackColor = Surface2; _clearSearch.ForeColor = TextMuted; _clearSearch.Cursor = Cursors.Hand;
        _clearSearch.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 48, 72);
        _clearSearch.SetBounds(347, 2, 36, 30);
        _clearSearch.Click += (_, _) => { _search.Clear(); _search.Focus(); };
        _search.GotFocus += (_, _) => searchBox.Invalidate();
        _search.LostFocus += (_, _) => searchBox.Invalidate();
        searchBox.Controls.Add(searchIcon); searchBox.Controls.Add(_search); searchBox.Controls.Add(_clearSearch);
        toolbar.Controls.Add(searchBox);

        _list.BackColor = Surface; _list.ForeColor = TextMain; _list.Font = new Font("Segoe UI", 10F); _list.SmallImageList = _images;
        _list.Columns.Add("Selecciona / Programa", 650);
        _list.Resize += (_, _) => { if (_list.Columns.Count > 0) _list.Columns[0].Width = Math.Max(250, _list.ClientSize.Width - 4); };
        _list.DrawColumnHeader += (_, e) => { using var br = new SolidBrush(Purple); e.Graphics.FillRectangle(br, e.Bounds); TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", new Font("Segoe UI", 9.5F, FontStyle.Bold), e.Bounds, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter); };
        _list.DrawItem += DrawProgramRow;
        _list.DrawSubItem += (_, _) => { };
        _list.MouseDown += ListMouseDown;
        _list.MouseDoubleClick += ListMouseDoubleClick;
        _list.ItemChecked += (_, _) => { };
        group.Controls.Add(_list); group.Controls.Add(toolbar);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Surface };
        _status.ForeColor = TextMuted; _status.Padding = new Padding(8, 0, 0, 0); statusPanel.Controls.Add(_status);

        var actionBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Bg, Padding = new Padding(3, 2, 3, 2) };
        foreach (var button in new[] { _add, _remove, _install }) { button.Height = 46; button.Font = new Font("Segoe UI", 9.2F, FontStyle.Bold); }
        StyleButton(_add, false); StyleButton(_remove, true); StyleButton(_install, false);
        void LayoutActions()
        {
            int w = actionBar.ClientSize.Width; int gap = 3; int third = (w - gap * 2) / 3;
            _add.SetBounds(0, 0, third, 46); _remove.SetBounds(third + gap, 0, third, 46); _install.SetBounds((third + gap) * 2, 0, w - (third + gap) * 2, 46);
        }
        actionBar.Resize += (_, _) => LayoutActions(); LayoutActions();
        actionBar.Controls.AddRange(new Control[] { _add, _remove, _install });

        content.Controls.Add(group);
        Controls.Add(content); Controls.Add(statusPanel); Controls.Add(actionBar); Controls.Add(header);

        _add.Click += (_, _) => AddInstallers();
        _remove.Click += (_, _) => RemoveSelected();
        _install.Click += async (_, _) => await InstallSelectedAsync();
        _search.TextChanged += (_, _) => RefreshList();
        RefreshList();
    }

    private void DrawProgramRow(object? sender, DrawListViewItemEventArgs e)
    {
        if (e.Item == null) return;
        string path = e.Item.Tag as string ?? "";
        bool selected = e.Item.Selected;
        bool checkedItem = _checkedPaths.Contains(path);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(selected ? Color.FromArgb(54, 45, 72) : Surface);
        g.FillRectangle(bg, e.Bounds);
        using var separator = new Pen(Border); g.DrawLine(separator, e.Bounds.Left + 10, e.Bounds.Bottom - 1, e.Bounds.Right - 10, e.Bounds.Bottom - 1);
        var cb = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top + (e.Bounds.Height - 18) / 2, 18, 18);
        using var cbFill = new SolidBrush(checkedItem ? Purple : Color.FromArgb(46, 40, 59));
        using var cbPen = new Pen(checkedItem ? PurpleHover : Border, 1.2F);
        using var pathCb = RoundedRect(cb, 4); g.FillPath(cbFill, pathCb); g.DrawPath(cbPen, pathCb);
        if (checkedItem) { using var tick = new Pen(Color.White, 2F); g.DrawLines(tick, new[] { new Point(cb.X + 3, cb.Y + 9), new Point(cb.X + 7, cb.Y + 13), new Point(cb.X + 14, cb.Y + 4) }); }
        int imageIndex = e.Item.ImageIndex;
        if (imageIndex >= 0 && imageIndex < _images.Images.Count)\n        {\n            // Icono centrado verticalmente y separado de la línea divisoria de cada fila.\n            const int logoSize = 36;\n            int logoY = e.Bounds.Top + (e.Bounds.Height - logoSize) / 2;\n            g.DrawImage(_images.Images[imageIndex], e.Bounds.Left + 44, logoY, logoSize, logoSize);\n        }
        TextRenderer.DrawText(g, e.Item.Text, new Font("Segoe UI", 10F, FontStyle.Bold), new Rectangle(e.Bounds.Left + 92, e.Bounds.Top + 2, e.Bounds.Width - 104, e.Bounds.Height - 4), TextMain, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath(); int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }

    private void ListMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        var item = _list.GetItemAt(e.X, e.Y);
        if (item?.Tag is not string path) return;
        int relativeX = e.X - item.Bounds.Left;
        if (relativeX >= 44 && relativeX <= 82) ChangeLogo(item);
        else if (relativeX >= 88) RenameProgram(item);
    }

    private void ListMouseDown(object? sender, MouseEventArgs e)
    {
        var item = _list.GetItemAt(e.X, e.Y); if (item?.Tag is not string path) return;
        if (e.X <= item.Bounds.Left + 38) { if (_checkedPaths.Contains(path)) _checkedPaths.Remove(path); else _checkedPaths.Add(path); _list.Invalidate(item.Bounds); }
    }

    private void LoadMetadata()
    {
        try { if (File.Exists(_metadataPath)) { var loaded = JsonSerializer.Deserialize<Dictionary<string, ProgramMeta>>(File.ReadAllText(_metadataPath)); if (loaded != null) foreach (var pair in loaded) _metadata[pair.Key] = pair.Value; } } catch { }
    }
    private void SaveMetadata() { try { File.WriteAllText(_metadataPath, JsonSerializer.Serialize(_metadata, new JsonSerializerOptions { WriteIndented = true })); } catch (Exception ex) { MessageBox.Show(this, "No se pudo guardar la configuración: " + ex.Message); } }
    private ProgramMeta Meta(string file) { if (!_metadata.TryGetValue(Path.GetFileName(file), out var m)) _metadata[Path.GetFileName(file)] = m = new ProgramMeta(); return m; }
    private string DisplayName(string file) => Meta(file).DisplayName ?? Path.GetFileNameWithoutExtension(file);

    private void AddInstallers()
    {
        using var dialog = new OpenFileDialog { Title = "Selecciona instaladores", Filter = "Instaladores (*.exe;*.msi)|*.exe;*.msi", Multiselect = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        int added = 0;
        foreach (string source in dialog.FileNames)
        {
            try
            {
                string destination = Path.Combine(_library, Path.GetFileName(source));
                if (File.Exists(destination)) destination = Path.Combine(_library, Path.GetFileNameWithoutExtension(source) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + Path.GetExtension(source));
                File.Copy(source, destination); added++;
            }
            catch (Exception ex) { MessageBox.Show(this, $"Error copiando instalador: {ex.Message}", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        _status.Text = $"Agregados {added} instalador(es)."; RefreshList();
    }

    private void RenameProgram(ListViewItem selectedItem)
    {
        string file = (string)selectedItem.Tag!;
        string current = DisplayName(file);
        using var dialog = new Form { Text = "Cambiar nombre del programa", Size = new Size(420, 150), StartPosition = FormStartPosition.CenterParent, BackColor = Surface, ForeColor = TextMain, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var text = new TextBox { Text = current, Bounds = new Rectangle(15, 15, 370, 30), BackColor = Surface2, ForeColor = TextMain };
        var ok = new Button { Text = "Guardar", DialogResult = DialogResult.OK, Bounds = new Rectangle(270, 55, 115, 32) }; StyleButton(ok, false);
        dialog.Controls.Add(text); dialog.Controls.Add(ok); dialog.AcceptButton = ok;
        if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(text.Text)) { Meta(file).DisplayName = text.Text.Trim(); SaveMetadata(); RefreshList(); }
    }

    private void ChangeLogo(ListViewItem selectedItem)
    {
        string file = (string)selectedItem.Tag!;
        using var dialog = new OpenFileDialog { Title = "Selecciona logo o imagen", Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string target = Path.Combine(_logos, Path.GetFileNameWithoutExtension(file) + ".png");
            using var img = Image.FromFile(dialog.FileName); using var bmp = new Bitmap(img, new Size(64, 64)); bmp.Save(target, System.Drawing.Imaging.ImageFormat.Png);
            Meta(file).LogoPath = target; SaveMetadata(); RefreshList();
        }
        catch (Exception ex) { MessageBox.Show(this, "No se pudo cargar el logo: " + ex.Message, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private Image LoadProgramImage(string file)
    {
        var m = Meta(file);
        try { if (!string.IsNullOrWhiteSpace(m.LogoPath) && File.Exists(m.LogoPath)) { using var loaded = Image.FromFile(m.LogoPath); return new Bitmap(loaded); } } catch { }
        try { if (Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase)) { using var icon = Icon.ExtractAssociatedIcon(file); if (icon != null) return icon.ToBitmap(); } } catch { }
        return SystemIcons.Application.ToBitmap();
    }

    private void RemoveSelected()
    {
        var selected = _list.SelectedItems.Cast<ListViewItem>().ToList();
        if (selected.Count == 0) { MessageBox.Show("Selecciona un instalador de la lista."); return; }
        if (MessageBox.Show(this, $"¿Eliminar {selected.Count} instalador(es) de la biblioteca?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        foreach (var item in selected) { try { string p = (string)item.Tag!; File.Delete(p); _checkedPaths.Remove(p); _metadata.Remove(Path.GetFileName(p)); } catch { } }
        SaveMetadata(); RefreshList();
    }

    private async Task InstallSelectedAsync()
    {
        var selected = _list.Items.Cast<ListViewItem>().Where(i => i.Tag is string p && _checkedPaths.Contains(p)).ToList();
        if (selected.Count == 0) { MessageBox.Show("Marca los programas que deseas instalar con la casilla morada."); return; }
        if (MessageBox.Show($"Se ejecutarán {selected.Count} instalador(es), uno a la vez. ¿Continuar?", "Instalación secuencial", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _add.Enabled = _remove.Enabled = _install.Enabled = false; int ok = 0, failed = 0, completed = 0;
        _progressBar.Value = 0;
        _progressCaption.Text = $"Preparando instalación de {selected.Count} programa(s)...";
        _status.Text = "Ahora: —";
        try
        {
            foreach (var item in selected)
            {
                string path = (string)item.Tag!;
                _progressCaption.Text = $"Instalando {completed + 1} de {selected.Count}: {DisplayName(path)}";
                _status.Text = "Ahora: " + DisplayName(path);
                try
                {
                    var psi = new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "runas" };
                    using var proc = Process.Start(psi);
                    if (proc == null) failed++;
                    else { await Task.Run(() => proc.WaitForExit()); if (proc.ExitCode == 0) ok++; else failed++; }
                }
                catch { failed++; }
                completed++;
                _progressBar.Value = (int)Math.Round(completed * 100.0 / selected.Count);
                _progressCaption.Text = $"Progreso de instalación: {completed} de {selected.Count} ({_progressBar.Value}%)";
            }
        }
        finally { _add.Enabled = _remove.Enabled = _install.Enabled = true; }
        _progressBar.Value = 100;
        _progressCaption.Text = $"Instalación finalizada · {ok} correcto(s), {failed} error(es)";
        _status.Text = "Ahora: —";
    }

    private void RefreshList()
    {
        _list.BeginUpdate(); _list.Items.Clear(); _images.Images.Clear();
        string query = _search.Text.Trim();
        foreach (string file in Directory.GetFiles(_library).Where(f => (Path.GetExtension(f).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(f).Equals(".msi", StringComparison.OrdinalIgnoreCase)) && Path.GetFileName(f).Contains(query, StringComparison.OrdinalIgnoreCase)).OrderBy(f => DisplayName(f)))
        {
            _images.Images.Add(LoadProgramImage(file));
            var item = new ListViewItem(DisplayName(file), _images.Images.Count - 1) { Tag = file };
            _list.Items.Add(item);
        }
        _list.EndUpdate(); _list.Invalidate();
        _status.Text = $"{_list.Items.Count} programa(s) en biblioteca. Marca las casillas para instalar.";
    }

    private GroupBox MakeGroup(string title) => new() { Text = title, ForeColor = Color.FromArgb(210, 197, 235), BackColor = Surface, Padding = new Padding(10), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10), FlatStyle = FlatStyle.Flat };
    private static void StyleButton(Button b, bool secondary)
    {
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Border; b.FlatAppearance.BorderSize = secondary ? 1 : 0;
        b.BackColor = secondary ? Surface2 : Purple; b.ForeColor = TextMain; b.Cursor = Cursors.Hand;
        b.FlatAppearance.MouseOverBackColor = secondary ? Color.FromArgb(52, 45, 67) : PurpleHover;
    }
    private static void StyleTextBox(TextBox t) { t.BackColor = Surface2; t.ForeColor = TextMain; t.BorderStyle = BorderStyle.FixedSingle; t.Font = new Font("Segoe UI", 10F); }
}
