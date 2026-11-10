using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal sealed class ProgramManagerForm : UserControl
{
    public event Action? BackRequested;
    private readonly string _library = Path.Combine(AppContext.BaseDirectory, "Data", "Programs");
    private readonly string _logos = Path.Combine(AppContext.BaseDirectory, "Data", "Programs", ".logos");
    private readonly string _metadataPath = Path.Combine(AppContext.BaseDirectory, "Data", "Programs", "programs.json");
    private readonly bool _isWindows64 = Environment.Is64BitOperatingSystem;
    private readonly string _architectureLabel = Environment.Is64BitOperatingSystem ? "Windows 64 bits (x64)" : "Windows 32 bits (x86)";
    private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Tile, FullRowSelect = true, MultiSelect = true, GridLines = false, CheckBoxes = false, BorderStyle = BorderStyle.None, OwnerDraw = true, HideSelection = false, TileSize = new Size(360, 76) };
    private readonly ImageList _images = new() { ImageSize = new Size(44, 44), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 25, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ModernProgressBar _progressBar = new() { Dock = DockStyle.Top, Height = 28, Value = 0 };
    private readonly Label _progressCaption = new() { Dock = DockStyle.Top, Height = 22, Text = "Listo para comenzar", TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _install = new() { Text = "INSTALAR SELECCIONADOS" };
    private readonly Button _add = new() { Text = "+  AGREGAR PORTABLE" };
    private readonly Button _addProgram = new() { Text = "+  AGREGAR PROGRAMA" };
    private readonly Button _remove = new() { Text = "ELIMINAR PROGRAMA" };
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

    private sealed class ProgramMeta { public string? DisplayName { get; set; } public string? LogoPath { get; set; } public string? Architecture { get; set; } public string? GroupId { get; set; } public string? WingetId { get; set; } public string? InstallMode { get; set; } public string? InstallArguments { get; set; } public bool RunAsAdmin { get; set; } = true; }

    public ProgramManagerForm()
    {
        Dock = DockStyle.Fill; BackColor = Bg; ForeColor = TextMain; Font = new Font("Segoe UI", 9.5F);
        Directory.CreateDirectory(_library); Directory.CreateDirectory(_logos); LoadMetadata();

        var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Purple };
        _back.SetBounds(22, 5, 170, 25); StyleButton(_back, true);
        _back.Click += (_, _) => BackRequested?.Invoke(); header.Controls.Add(_back);
        header.Controls.Add(new Label { Text = "PROGRAMAS  /  INSTALADORES", ForeColor = Color.White, Font = new Font("Segoe UI", 19F, FontStyle.Bold), AutoSize = true, Location = new Point(22, 30) });
        header.Controls.Add(new Label { Text = "Biblioteca local · " + _library, ForeColor = Color.FromArgb(235, 225, 250), AutoSize = true, Location = new Point(25, 66), MaximumSize = new Size(1500, 22) });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12), BackColor = Bg };
        var group = MakeGroup("01  ·  Biblioteca de programas  |  " + _architectureLabel); group.Dock = DockStyle.Fill;

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
        var searchIcon = new Label { Bounds = new Rectangle(8, 0, 34, 34), BackColor = Color.Transparent };
        searchIcon.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(PurpleHover, 2.2F);
            e.Graphics.DrawEllipse(pen, 9, 8, 12, 12);
            e.Graphics.DrawLine(pen, 20, 20, 26, 26);
        };
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
        // Cuadrícula de tres columnas como en la referencia enviada.
        _list.TileSize = new Size(Math.Max(280, (_list.ClientSize.Width - 36) / 3), 60);
        _list.Resize += (_, _) => _list.TileSize = new Size(Math.Max(280, (_list.ClientSize.Width - 36) / 3), 60);
        var listHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Purple };
        listHeader.Controls.Add(new Label { Text = "Selecciona / Programa", Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) });
        _list.DrawItem += DrawProgramRow;
        _list.DrawSubItem += (_, _) => { };
        _list.MouseDown += ListMouseDown;
        _list.MouseDoubleClick += ListMouseDoubleClick;
        _list.ItemChecked += (_, _) => { };
        group.Controls.Add(_list); group.Controls.Add(listHeader); group.Controls.Add(toolbar);

        var progressPanel = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Surface, Padding = new Padding(10, 4, 10, 4) };
        _progressCaption.Dock = DockStyle.Top; _progressCaption.Height = 20; _progressCaption.ForeColor = TextMuted;
        _progressBar.Dock = DockStyle.Bottom; _progressBar.Height = 25; _progressBar.Value = 0;
        progressPanel.Controls.Add(_progressBar); progressPanel.Controls.Add(_progressCaption);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Surface };
        _status.ForeColor = TextMuted; _status.Padding = new Padding(8, 0, 0, 0); statusPanel.Controls.Add(_status);

        var actionBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Bg, Padding = new Padding(3, 2, 3, 2) };
        foreach (var button in new[] { _add, _addProgram, _remove, _install }) { button.Height = 46; button.Font = new Font("Segoe UI", 9.2F, FontStyle.Bold); }
        StyleButton(_add, false); StyleButton(_remove, true); StyleButton(_install, false);
        void LayoutActions()
        {
            int w = actionBar.ClientSize.Width; int gap = 4; int each = (w - gap * 3) / 4;
            _addProgram.SetBounds(0, 0, each, 46);
            _add.SetBounds(each + gap, 0, each, 46);
            _install.SetBounds((each + gap) * 2, 0, each, 46);
            _remove.SetBounds((each + gap) * 3, 0, w - (each + gap) * 3, 46);
        }
        actionBar.Resize += (_, _) => LayoutActions(); LayoutActions();
        actionBar.Controls.AddRange(new Control[] { _add, _addProgram, _remove, _install });

        content.Controls.Add(group);
        Controls.Add(content); Controls.Add(progressPanel); Controls.Add(statusPanel); Controls.Add(actionBar); Controls.Add(header);

        _add.Click += async (_, _) => await AddPortableFolderAsync();
        _addProgram.Click += async (_, _) => await AddInstallersAsync();
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
        if (imageIndex >= 0 && imageIndex < _images.Images.Count)
        {
            // Icono centrado verticalmente y separado de la línea divisoria de cada fila.
            const int logoSize = 36;
            int logoY = e.Bounds.Top + (e.Bounds.Height - logoSize) / 2;
            g.DrawImage(_images.Images[imageIndex], e.Bounds.Left + 44, logoY, logoSize, logoSize);
        }
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
    private string MetaKey(string file) => Path.GetRelativePath(_library, file).Replace('\\', '/');
    private ProgramMeta Meta(string file) { string key = MetaKey(file); if (!_metadata.TryGetValue(key, out var m)) { if (_metadata.TryGetValue(Path.GetFileName(file), out var legacy)) { m = legacy; _metadata.Remove(Path.GetFileName(file)); _metadata[key] = m; } else _metadata[key] = m = new ProgramMeta(); } return m; }
    private static string ArchitectureFolder(string architecture) => architecture switch { "x64" => "x64", "x86" => "x86", "Both" => "Compatible_x86_x64", "Universal" => "Compatible_x86_x64", _ => "Unknown" };
    private string DisplayName(string file) => Meta(file).DisplayName ?? Path.GetFileNameWithoutExtension(file);

    private async Task AddPortableFolderAsync()
    {
        using var folderDialog = new FolderBrowserDialog { Description = "Selecciona la carpeta donde está el ejecutable portable." };
        if (folderDialog.ShowDialog(this) != DialogResult.OK) return;

        using var exeDialog = new OpenFileDialog
        {
            InitialDirectory = folderDialog.SelectedPath,
            Filter = "Ejecutable (*.exe)|*.exe",
            Title = "Selecciona el único ejecutable que deseas guardar"
        };
        if (exeDialog.ShowDialog(this) != DialogResult.OK) return;

        string root = Path.GetFullPath(folderDialog.SelectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string selectedExe = Path.GetFullPath(exeDialog.FileName);
        if (!selectedExe.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "El ejecutable debe estar dentro de la carpeta seleccionada.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string displayName = Path.GetFileNameWithoutExtension(selectedExe);
        string safeName = string.Concat(displayName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        string dest = Path.Combine(_library, "Portable_" + safeName + "_" + Guid.NewGuid().ToString("N")[..8]);
        string copiedExe = Path.Combine(dest, Path.GetFileName(selectedExe));
        SetBusy(true, "Guardando el ejecutable seleccionado...");
        try
        {
            Directory.CreateDirectory(dest);
            long total = Math.Max(1, new FileInfo(selectedExe).Length);
            long copied = 0;
            using (var input = new FileStream(selectedExe, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true))
            using (var output = new FileStream(copiedExe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
            {
                byte[] buffer = new byte[1024 * 1024];
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await output.WriteAsync(buffer, 0, read);
                    copied += read;
                    SetProgress((int)Math.Min(99, copied * 100.0 / total), "Copiando " + Path.GetFileName(selectedExe));
                }
            }

            var meta = Meta(copiedExe);
            meta.DisplayName = displayName;
            meta.Architecture = "Any";
            SaveMetadata();
            RefreshList();
            SetProgress(100, "Ejecutable guardado: " + displayName);
            MessageBox.Show(this,
                "Se agregó únicamente el ejecutable que seleccionaste.\n\nPrograma: " + displayName +
                "\nArchivo guardado: " + Path.GetFileName(selectedExe) +
                "\n\nNo se copiaron los demás EXE ni los archivos de la carpeta original. Si este programa necesita DLL, carpetas de datos u otros archivos externos, podría no funcionar como portable.",
                "ONKOS · Programa portable", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            try { if (Directory.Exists(dest)) Directory.Delete(dest, true); } catch { }
            MessageBox.Show(this, ex.Message, "Error al agregar portable", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetProgress(0, "No se pudo guardar el ejecutable");
        }
        finally { SetBusy(false); }
    }

    private async Task CopyDirectoryWithProgressAsync(string source, string destination, string caption)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        long total = Math.Max(1, files.Sum(f => new FileInfo(f).Length));
        long copied = 0;
        Directory.CreateDirectory(destination);
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        foreach (string file in files)
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
            using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
            byte[] buffer = new byte[1024 * 1024];
            int read;
            while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await output.WriteAsync(buffer, 0, read);
                copied += read;
                int percent = (int)Math.Min(99, copied * 100.0 / total);
                SetProgress(percent, caption + ": " + Path.GetFileName(file));
            }
        }
    }

    private void SetProgress(int value, string caption)
    {
        _progressBar.Value = Math.Max(0, Math.Min(100, value));
        _progressCaption.Text = caption + " · " + _progressBar.Value + "%";
    }

    private void SetBusy(bool busy, string? caption = null)
    {
        _add.Enabled = _addProgram.Enabled = _remove.Enabled = _install.Enabled = !busy;
        if (caption != null) SetProgress(0, caption);
        if (!busy) _status.Text = "Listo";
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string f in Directory.GetFiles(source)) File.Copy(f, Path.Combine(destination, Path.GetFileName(f)), true);
        foreach (string d in Directory.GetDirectories(source)) CopyDirectory(d, Path.Combine(destination, Path.GetFileName(d)));
    }

    private async Task AddInstallersAsync()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Selecciona uno o varios instaladores para agregar",
            Filter = "Instaladores (*.exe;*.msi)|*.exe;*.msi",
            Multiselect = true
        };
        if (ofd.ShowDialog(this) != DialogResult.OK || ofd.FileNames.Length == 0) return;

        int added = 0, failed = 0, completed = 0;
        var errors = new List<string>();
        long total = Math.Max(1, ofd.FileNames.Sum(f => new FileInfo(f).Length));
        long copied = 0;
        SetBusy(true, "Cargando programas a la biblioteca...");
        try
        {
            foreach (string source in ofd.FileNames)
            {
                string name = Path.GetFileNameWithoutExtension(source);
                string destination = Path.Combine(_library, Path.GetFileName(source));
                try
                {
                    if (File.Exists(destination))
                    {
                        string baseName = Path.GetFileNameWithoutExtension(source);
                        string ext = Path.GetExtension(source);
                        destination = Path.Combine(_library, baseName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ext);
                    }

                    long fileLength = new FileInfo(source).Length;
                    using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
                    using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true);
                    byte[] buffer = new byte[1024 * 1024];
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, read);
                        copied += read;
                        SetProgress((int)Math.Min(99, copied * 100.0 / total), "Cargando " + name);
                    }
                    var meta = Meta(destination);
                    meta.DisplayName = name;
                    meta.Architecture = "Any";
                    meta.WingetId = "";
                    added++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add(Path.GetFileName(source) + ": " + ex.Message);
                }
                completed++;
            }
            SaveMetadata();
            RefreshList();
            SetProgress(100, "Carga finalizada: " + added + " programa(s)");
            _status.Text = $"Agregados {added} instalador(es); errores: {failed}.";
            string summary = $"Proceso terminado.\n\nAgregados: {added}\nErrores: {failed}";
            if (errors.Count > 0) summary += "\n\n" + string.Join("\n", errors.Take(8));
            MessageBox.Show(this, summary, "ONKOS · Agregar programas", MessageBoxButtons.OK,
                failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally { SetBusy(false); }
    }

    private string? AskArchitecture(string fileName)
    {
        using var dialog = new Form { Text = "Seleccionar arquitectura", Size = new Size(520, 230), StartPosition = FormStartPosition.CenterParent, BackColor = Surface, ForeColor = TextMain, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var title = new Label { Text = "¿Qué arquitectura tiene este programa?", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = TextMain, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 48 };
        var info = new Label { Text = fileName + "\\nSelecciona la arquitectura del programa (no necesariamente la del setup).", ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 48 };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 75, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(10), BackColor = Surface };
        string? selected = null;
        Button MakeChoice(string text, string value, Color color) { var b = new Button { Text = text, Width = 145, Height = 48, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand }; b.Click += (_, _) => { selected = value; dialog.DialogResult = DialogResult.OK; dialog.Close(); }; return b; }
        buttons.Controls.Add(MakeChoice("64 BITS (x64)", "x64", Color.FromArgb(72, 104, 174)));
        buttons.Controls.Add(MakeChoice("32 BITS (x86)", "x86", Color.FromArgb(65, 125, 105)));
        buttons.Controls.Add(MakeChoice("AMBOS (x86 + x64)", "Both", Purple));
        dialog.Controls.Add(buttons); dialog.Controls.Add(info); dialog.Controls.Add(title);
        return dialog.ShowDialog(this) == DialogResult.OK ? selected : null;
    }

    private static string DetectInstallerArchitecture(string file)
    {
        // Detecta arquitectura del EXE. La arquitectura del setup puede diferir del payload instalado.
        try
        {
            if (Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(file);
                using var reader = new BinaryReader(stream);
                if (stream.Length >= 64 && reader.ReadUInt16() == 0x5A4D)
                {
                    stream.Position = 0x3C;
                    int peOffset = reader.ReadInt32();
                    if (peOffset > 0 && peOffset + 6 <= stream.Length)
                    {
                        stream.Position = peOffset;
                        if (reader.ReadUInt32() == 0x00004550)
                        {
                            ushort machine = reader.ReadUInt16();
                            if (machine == 0x8664) return "x64";
                            if (machine == 0xAA64) return "ARM64";
                            if (machine == 0x014c) return "x86";
                        }
                    }
                }
            }
        }
        catch { }

        string lower = Path.GetFileName(file).ToLowerInvariant();
        if (lower.Contains("x64") || lower.Contains("amd64") || lower.Contains("64bit") || lower.Contains("64-bit")) return "x64";
        if (lower.Contains("x86") || lower.Contains("32bit") || lower.Contains("32-bit") || lower.Contains("win32")) return "x86";
        return "Unknown";
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
        string confirmText = $"Se ejecutarán {selected.Count} programa(s), uno a la vez.\n\nONKOS no bloqueará programas por ser de 32 o 64 bits: cada instalador se abrirá tal como fue guardado.\n\nSi el propio instalador detecta incompatibilidades, mostrará su mensaje y ONKOS te informará el resultado al finalizar.\n\n¿Continuar?";
        if (MessageBox.Show(confirmText, "Ejecutar programas", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        SetBusy(true, "Preparando ejecución..."); int ok = 0, failed = 0, completed = 0;
        var failureDetails = new List<string>();
        SetProgress(0, $"Preparando instalación de {selected.Count} programa(s)");
        _status.Text = "Ahora: —";
        try
        {
            foreach (var item in selected)
            {
                string path = (string)item.Tag!;
                SetProgress((int)Math.Round(completed * 100.0 / selected.Count), $"Instalando {completed + 1} de {selected.Count}: {DisplayName(path)}");
                _status.Text = "Ahora: " + DisplayName(path);
                try
                {
                    var meta = Meta(path);
                    string mode = meta.InstallMode ?? "normal";
                    string args = meta.InstallArguments ?? "";
                    string ext = Path.GetExtension(path);
                    string executable = path;

                    if (ext.Equals(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        executable = "msiexec.exe";
                        if (mode == "normal") args = "/i " + Quote(path);
                        else if (mode == "silent") args = string.IsNullOrWhiteSpace(args) ? "/i " + Quote(path) + " /qn /norestart" : "/i " + Quote(path) + " " + args;
                        else args = "/i " + Quote(path) + " " + args;
                    }
                    else if (mode == "silent" && string.IsNullOrWhiteSpace(args))
                    {
                        // EXE no tiene un parámetro silencioso universal: se evita asumir uno.
                        mode = "normal";
                    }

                    var psi = new ProcessStartInfo { FileName = executable, Arguments = args, UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path)! };
                    if (meta.RunAsAdmin) psi.Verb = "runas";
                    using var proc = Process.Start(psi);
                    if (proc == null)
                    {
                        failed++;
                        failureDetails.Add(DisplayName(path) + ": Windows no pudo iniciar el instalador.");
                    }
                    else
                    {
                        await Task.Run(() => proc.WaitForExit());
                        if (proc.ExitCode == 0) ok++;
                        else
                        {
                            failed++;
                            failureDetails.Add(DisplayName(path) + ": el instalador terminó con código " + proc.ExitCode + ". Revisa el mensaje mostrado por el propio instalador; puede ser incompatibilidad de arquitectura, requisitos previos o una instalación anterior.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    failureDetails.Add(DisplayName(path) + ": " + ex.Message);
                }
                completed++;
                SetProgress((int)Math.Round(completed * 100.0 / selected.Count), $"Instalación: {completed} de {selected.Count}");
            }
        }
        finally { SetBusy(false); }
        SetProgress(100, $"Proceso finalizado · {ok} correcto(s), {failed} error(es)");
        string resultMessage = $"Proceso terminado.\\n\\nEjecutados correctamente: {ok}\\nCon error: {failed}";
        if (failureDetails.Count > 0)
            resultMessage += "\\n\\nDETALLE DE LOS ERRORES:\\n" + string.Join("\\n\\n", failureDetails.Take(10));
        MessageBox.Show(this, resultMessage, "ONKOS · Resultado", MessageBoxButtons.OK,
            failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private bool IsVisibleLibraryInstaller(string file)
    {
        try
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(file)!);
            while (directory != null && !directory.FullName.Equals(_library, StringComparison.OrdinalIgnoreCase))
            {
                string manifest = Path.Combine(directory.FullName, ".onko-portable-main.txt");
                if (File.Exists(manifest))
                {
                    string relative = File.ReadAllText(manifest).Trim();
                    if (string.IsNullOrWhiteSpace(relative)) return false;
                    string mainExecutable = Path.GetFullPath(Path.Combine(directory.FullName, relative));
                    return Path.GetFullPath(file).Equals(mainExecutable, StringComparison.OrdinalIgnoreCase);
                }
                directory = directory.Parent;
            }
        }
        catch { return false; }

        // Los instaladores individuales y los paquetes antiguos sin manifiesto conservan su comportamiento.
        return true;
    }

    private void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        _images.Images.Clear();
        string query = _search.Text.Trim();

        // En paquetes portables solo se muestra el ejecutable principal elegido al agregarlos.
        var installers = Directory.GetFiles(_library, "*", SearchOption.AllDirectories)
            .Where(f => (Path.GetExtension(f).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                         Path.GetExtension(f).Equals(".msi", StringComparison.OrdinalIgnoreCase)) &&
                        !Path.GetFileName(f).StartsWith("."))
            .Where(IsVisibleLibraryInstaller)
            .OrderBy(f => DisplayName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string file in installers)
        {
            string name = DisplayName(file);
            if (!name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            _images.Images.Add(LoadProgramImage(file));
            _list.Items.Add(new ListViewItem(name, _images.Images.Count - 1) { Tag = file });
        }

        _list.EndUpdate();
        _list.Invalidate();
        _status.Text = $"Windows detectado: {_architectureLabel}. Se muestran todos los programas, sin filtro de arquitectura.";
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
