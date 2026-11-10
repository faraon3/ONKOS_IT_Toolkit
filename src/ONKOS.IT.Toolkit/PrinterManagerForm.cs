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

internal sealed class PrinterManagerForm : UserControl
{
    public event Action? BackRequested;

    private readonly string _library = Path.Combine(AppContext.BaseDirectory, "Data", "Drivers");
    private readonly string _logos = Path.Combine(AppContext.BaseDirectory, "Data", "Drivers", ".logos");
    private readonly string _metadataPath = Path.Combine(AppContext.BaseDirectory, "Data", "Drivers", "printers.json");
    private readonly Dictionary<string, PrinterMeta> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Tile, FullRowSelect = true, MultiSelect = false, BorderStyle = BorderStyle.None, OwnerDraw = true, HideSelection = false };
    private readonly ImageList _images = new() { ImageSize = new Size(44, 44), ColorDepth = ColorDepth.Depth32Bit };
    private readonly TextBox _search = new() { PlaceholderText = "Buscar impresora o modelo...", BorderStyle = BorderStyle.None };
    private readonly Button _clear = new() { Text = "×", Width = 34, Height = 30, Font = new Font("Segoe UI", 16F, FontStyle.Bold) };
    private readonly Button _add = new() { Text = "+  AGREGAR DRIVER" };
    private readonly Button _install = new() { Text = "INSTALAR IMPRESORA" };
    private readonly Button _remove = new() { Text = "ELIMINAR IMPRESORA" };
    private readonly Button _back = new() { Text = "‹  VOLVER AL INICIO" };
    private readonly ModernProgressBar _progress = new();
    private readonly Label _progressText = new() { Text = "Listo para comenzar", ForeColor = TextMuted, Dock = DockStyle.Top, Height = 22 };
    private readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextMuted };

    private static readonly Color Bg = Color.FromArgb(18, 16, 25);
    private static readonly Color Surface = Color.FromArgb(31, 27, 41);
    private static readonly Color Surface2 = Color.FromArgb(39, 34, 51);
    private static readonly Color Purple = Color.FromArgb(105, 73, 164);
    private static readonly Color PurpleHover = Color.FromArgb(126, 91, 190);
    private static readonly Color TextMain = Color.FromArgb(244, 241, 250);
    private static readonly Color TextMuted = Color.FromArgb(183, 174, 199);
    private static readonly Color Border = Color.FromArgb(65, 57, 82);

    private sealed class PrinterMeta
    {
        public string DisplayName { get; set; } = "";
        public string Model { get; set; } = "";
        public string InstallerRelativePath { get; set; } = "";
        public string? LogoPath { get; set; }
        public string InstallType { get; set; } = "exe";
    }

    public PrinterManagerForm()
    {
        Dock = DockStyle.Fill; BackColor = Bg; ForeColor = TextMain; Font = new Font("Segoe UI", 9.5F);
        Directory.CreateDirectory(_library); Directory.CreateDirectory(_logos); LoadMetadata();

        var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Purple };
        _back.SetBounds(22, 5, 170, 25); StyleButton(_back, true);
        _back.Click += (_, _) => BackRequested?.Invoke(); header.Controls.Add(_back);
        header.Controls.Add(new Label { Text = "IMPRESORAS  /  CONTROLADORES", ForeColor = Color.White, Font = new Font("Segoe UI", 19F, FontStyle.Bold), AutoSize = true, Location = new Point(22, 30) });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12), BackColor = Bg };
        var group = MakeGroup("01  ·  Biblioteca de impresoras"); group.Dock = DockStyle.Fill;
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Surface };
        var searchBox = new Panel { Bounds = new Rectangle(10, 6, 390, 36), BackColor = Surface2 };
        searchBox.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(_search.Focused ? PurpleHover : Border, _search.Focused ? 1.8F : 1.2F);
            using var path = RoundedRect(new Rectangle(1, 1, searchBox.Width - 3, searchBox.Height - 3), 8);
            e.Graphics.DrawPath(pen, path);
        };
        var searchIcon = new Label { Text = "⌕", Font = new Font("Segoe UI", 21F, FontStyle.Bold), ForeColor = PurpleHover, TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(8, 0, 34, 34) };
        _search.SetBounds(43, 8, 300, 20); _search.BackColor = Surface2; _search.ForeColor = TextMain; _search.Font = new Font("Segoe UI", 10F);
        _clear.SetBounds(347, 2, 36, 30); _clear.FlatStyle = FlatStyle.Flat; _clear.FlatAppearance.BorderSize = 0; _clear.BackColor = Surface2; _clear.ForeColor = TextMuted;
        _clear.Click += (_, _) => { _search.Clear(); _search.Focus(); };
        searchBox.Controls.Add(searchIcon); searchBox.Controls.Add(_search); searchBox.Controls.Add(_clear); toolbar.Controls.Add(searchBox);

        _list.BackColor = Surface; _list.ForeColor = TextMain; _list.Font = new Font("Segoe UI", 10F); _list.SmallImageList = _images;
        _list.TileSize = new Size(350, 64);
        _list.Resize += (_, _) => _list.TileSize = new Size(Math.Max(280, (_list.ClientSize.Width - 30) / 3), 64);
        var listHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Purple };
        listHeader.Controls.Add(new Label { Text = "Impresora / Modelo", Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) });
        _list.DrawItem += DrawPrinterRow;
        _list.MouseDoubleClick += ListMouseDoubleClick;
        group.Controls.Add(_list); group.Controls.Add(listHeader); group.Controls.Add(toolbar);

        var progressPanel = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Surface, Padding = new Padding(10, 4, 10, 4) };
        _progressText.Dock = DockStyle.Top; _progress.Dock = DockStyle.Bottom; _progress.Height = 25;
        progressPanel.Controls.Add(_progress); progressPanel.Controls.Add(_progressText);
        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Surface, Padding = new Padding(8, 0, 0, 0) };
        statusPanel.Controls.Add(_status);

        var actionBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Bg, Padding = new Padding(3, 2, 3, 2) };
        foreach (var b in new[] { _add, _install, _remove }) { b.Font = new Font("Segoe UI", 9F, FontStyle.Bold); b.Height = 46; }
        StyleButton(_add, false); StyleButton(_install, false); StyleButton(_remove, true);
        void LayoutButtons()
        {
            int gap = 6, w = actionBar.ClientSize.Width, each = (w - gap * 2) / 3;
            _add.SetBounds(0, 0, each, 46); _install.SetBounds(each + gap, 0, each, 46); _remove.SetBounds((each + gap) * 2, 0, w - (each + gap) * 2, 46);
        }
        actionBar.Resize += (_, _) => LayoutButtons(); LayoutButtons();
        actionBar.Controls.AddRange(new Control[] { _add, _install, _remove });
        content.Controls.Add(group);
        Controls.Add(content); Controls.Add(progressPanel); Controls.Add(statusPanel); Controls.Add(actionBar); Controls.Add(header);

        _add.Click += async (_, _) => await AddDriverAsync();
        _install.Click += async (_, _) => await InstallSelectedAsync();
        _remove.Click += (_, _) => RemoveSelected();
        _search.TextChanged += (_, _) => RefreshList();
        RefreshList();
    }

    private async Task AddDriverAsync()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Selecciona los instaladores y archivos de los controladores que deseas agregar",
            Filter = "Todos los archivos (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK || picker.FileNames.Length == 0) return;

        string[] selectedFiles = picker.FileNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Cada carpeta de origen se trata como un controlador independiente.
        // Así se pueden seleccionar instaladores de varias impresoras a la vez.
        var groups = selectedFiles
            .GroupBy(f => Path.GetDirectoryName(f)!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                SourceFolder = g.Key,
                Files = g.ToArray(),
                Candidates = g.Where(f => new[] { ".exe", ".msi", ".inf" }
                    .Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).ToArray()
            })
            .ToList();

        var validGroups = groups.Where(g => g.Candidates.Length > 0).ToList();
        if (validGroups.Count == 0)
        {
            MessageBox.Show(this,
                "No se encontró ningún instalador EXE, MSI o INF en los archivos seleccionados.",
                "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int skipped = groups.Count - validGroups.Count;
        long total = Math.Max(1, validGroups.Sum(g => g.Files.Sum(f => new FileInfo(f).Length)));
        long copied = 0;
        int added = 0;
        var addedNames = new List<string>();
        var createdFolders = new List<string>();

        SetBusy(true, "Preparando carga de controladores...");
        try
        {
            foreach (var group in validGroups)
            {
                string display = Path.GetFileName(group.SourceFolder.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(display))
                    display = Path.GetFileNameWithoutExtension(group.Candidates[0]);

                // Selección automática del instalador principal: primero busca
                // coincidencia con el nombre de la carpeta y luego nombres típicos.
                string installer = group.Candidates
                    .OrderByDescending(f => Path.GetFileNameWithoutExtension(f).Contains(display, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(f => new[] { "setup", "install", "driver" }
                        .Any(k => Path.GetFileNameWithoutExtension(f).Contains(k, StringComparison.OrdinalIgnoreCase)))
                    .ThenBy(f => Path.GetExtension(f).ToLowerInvariant() switch
                    {
                        ".exe" => 0,
                        ".msi" => 1,
                        ".inf" => 2,
                        _ => 3
                    })
                    .ThenBy(f => Path.GetFileName(f).Length)
                    .First();

                string safe = SafeName(display);
                string key = safe + "_" + Guid.NewGuid().ToString("N")[..8];
                string destination = Path.Combine(_library, key);
                Directory.CreateDirectory(destination);
                createdFolders.Add(destination);

                foreach (string file in group.Files)
                {
                    string target = Path.Combine(destination, Path.GetFileName(file));
                    using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
                    using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
                    byte[] buffer = new byte[1024 * 1024];
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, read);
                        copied += read;
                        SetProgress((int)Math.Min(99, copied * 100.0 / total), "Cargando " + display);
                    }
                }

                _metadata[key] = new PrinterMeta
                {
                    DisplayName = display,
                    Model = display,
                    InstallerRelativePath = Path.GetFileName(installer),
                    InstallType = Path.GetExtension(installer).TrimStart('.').ToLowerInvariant()
                };
                added++;
                addedNames.Add(display);
            }

            SaveMetadata();
            RefreshList();
            SetProgress(100, added + " controlador(es) agregado(s)");
            string summary = "Se agregaron " + added + " controlador(es) a la biblioteca.";
            if (skipped > 0)
                summary += "\n\nSe omitieron " + skipped + " carpeta(s) porque no contenían archivos EXE, MSI o INF.";
            summary += "\n\n" + string.Join("\n", addedNames);
            MessageBox.Show(this, summary, "ONKOS · Controladores agregados",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            foreach (string folder in createdFolders)
            {
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
            }
            foreach (string key in _metadata.Keys.Where(k => createdFolders.Contains(Path.Combine(_library, k), StringComparer.OrdinalIgnoreCase)).ToList())
                _metadata.Remove(key);
            try { SaveMetadata(); } catch { }
            MessageBox.Show(this, ex.Message, "Error al agregar controladores",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetProgress(0, "No se pudieron cargar todos los controladores");
        }
        finally { SetBusy(false); }
    }

    private static async Task CopyFolderWithProgressAsync(string source, string destination, long total, Action<long> report)
    {
        Directory.CreateDirectory(destination);
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        long copied = 0;
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
            using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
            byte[] buffer = new byte[1024 * 1024]; int read;
            while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await output.WriteAsync(buffer, 0, read); copied += read; report(copied);
            }
        }
    }

    private async Task InstallSelectedAsync()
    {
        var item = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null;
        if (item?.Tag is not string key || !_metadata.TryGetValue(key, out var meta))
        { MessageBox.Show(this, "Selecciona una impresora de la biblioteca.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        string root = Path.Combine(_library, key);
        string installer = Path.GetFullPath(Path.Combine(root, meta.InstallerRelativePath));
        if (!installer.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(installer))
        { MessageBox.Show(this, "No se encuentra el instalador guardado para esta impresora.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        string msg = meta.InstallType == "inf"
            ? "Se agregará el controlador INF al almacén de controladores de Windows. Si la impresora está conectada o ya tiene un puerto configurado, Windows podrá asociar el controlador.\n\n¿Continuar?"
            : "Se abrirá el instalador del controlador seleccionado. Sigue los pasos que muestre el fabricante.\n\n¿Continuar?";
        if (MessageBox.Show(this, msg, "Instalar " + meta.DisplayName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        SetBusy(true, "Instalando controlador " + meta.DisplayName + "...");
        try
        {
            int exitCode;
            if (meta.InstallType == "inf")
            {
                var psi = new ProcessStartInfo { FileName = "pnputil.exe", Arguments = "/add-driver \"" + installer + "\" /install", UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(installer)! };
                using var process = Process.Start(psi) ?? throw new InvalidOperationException("Windows no pudo iniciar pnputil.");
                await Task.Run(() => process.WaitForExit()); exitCode = process.ExitCode;
            }
            else
            {
                var psi = new ProcessStartInfo { FileName = installer, UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(installer)! };
                using var process = Process.Start(psi) ?? throw new InvalidOperationException("Windows no pudo iniciar el instalador.");
                await Task.Run(() => process.WaitForExit()); exitCode = process.ExitCode;
            }
            SetProgress(100, "Proceso de instalación finalizado");
            if (exitCode == 0)
                MessageBox.Show(this, meta.InstallType == "inf" ? "Windows aceptó el controlador INF. Esto no garantiza que la cola de impresión o el puerto de red hayan quedado creados." : "El instalador terminó correctamente.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show(this, "El proceso terminó con código " + exitCode + ". Revisa el mensaje del instalador o del controlador.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            SetProgress(0, "Error durante la instalación");
            MessageBox.Show(this, ex.Message, "ONKOS · Error de instalación", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false); }
    }

    private void RemoveSelected()
    {
        var item = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null;
        if (item?.Tag is not string key) { MessageBox.Show(this, "Selecciona una impresora para eliminar."); return; }
        if (MessageBox.Show(this, "¿Eliminar " + item.Text + " y sus archivos de la biblioteca local? Esto no desinstala la impresora de Windows.", "Eliminar impresora", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { Directory.Delete(Path.Combine(_library, key), true); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        _metadata.Remove(key); SaveMetadata(); RefreshList();
    }

    // Igual que en el módulo Programas: doble clic en el logo para cambiarlo
    // y doble clic en el nombre para editar el nombre visible.
    private void ListMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        var item = _list.GetItemAt(e.X, e.Y);
        if (item?.Tag is not string key) return;

        int relativeX = e.X - item.Bounds.Left;
        if (relativeX >= 12 && relativeX <= 64)
        {
            ChangeLogo(item);
        }
        else if (relativeX >= 72)
        {
            RenameSelected();
        }
    }

    private void RenameSelected()
    {
        var item = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null;
        if (item?.Tag is not string key || !_metadata.TryGetValue(key, out var meta)) { MessageBox.Show(this, "Selecciona una impresora."); return; }
        string? value = Prompt("Nombre visible de la impresora:", meta.DisplayName);
        if (string.IsNullOrWhiteSpace(value)) return;
        meta.DisplayName = value.Trim(); SaveMetadata(); RefreshList();
    }

    private void ChangeLogo(ListViewItem? selectedItem = null)
    {
        var item = selectedItem ?? (_list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null);
        if (item?.Tag is not string key || !_metadata.TryGetValue(key, out var meta)) { MessageBox.Show(this, "Selecciona una impresora."); return; }
        using var dialog = new OpenFileDialog { Title = "Selecciona el logo de la impresora", Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string target = Path.Combine(_logos, key + ".png");
            using var image = Image.FromFile(dialog.FileName); using var bitmap = new Bitmap(image, new Size(64, 64)); bitmap.Save(target, System.Drawing.Imaging.ImageFormat.Png);
            meta.LogoPath = target; SaveMetadata(); RefreshList();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "ONKOS · Logo", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void RefreshList()
    {
        _list.BeginUpdate(); _list.Items.Clear(); _images.Images.Clear();
        string query = _search.Text.Trim();
        foreach (var pair in _metadata.OrderBy(p => p.Value.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            if (!pair.Value.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) && !pair.Value.Model.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            _images.Images.Add(LoadLogo(pair.Key, pair.Value));
            _list.Items.Add(new ListViewItem(pair.Value.DisplayName, _images.Images.Count - 1) { Tag = pair.Key });
        }
        _list.EndUpdate(); _list.Invalidate(); _status.Text = _metadata.Count + " impresora(s) en la biblioteca local.";
    }

    private Image LoadLogo(string key, PrinterMeta meta)
    {
        try { if (!string.IsNullOrWhiteSpace(meta.LogoPath) && File.Exists(meta.LogoPath)) { using var image = Image.FromFile(meta.LogoPath); return new Bitmap(image); } } catch { }
        return SystemIcons.Application.ToBitmap();
    }

    private void DrawPrinterRow(object? sender, DrawListViewItemEventArgs e)
    {
        if (e.Item == null) return;
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(e.Item.Selected ? Color.FromArgb(54, 45, 72) : Surface); g.FillRectangle(bg, e.Bounds);
        using var line = new Pen(Border); g.DrawLine(line, e.Bounds.Left + 10, e.Bounds.Bottom - 1, e.Bounds.Right - 10, e.Bounds.Bottom - 1);
        int imageIndex = e.Item.ImageIndex;
        if (imageIndex >= 0 && imageIndex < _images.Images.Count) g.DrawImage(_images.Images[imageIndex], e.Bounds.Left + 16, e.Bounds.Top + 9, 42, 42);
        TextRenderer.DrawText(g, e.Item.Text, new Font("Segoe UI", 10F, FontStyle.Bold), new Rectangle(e.Bounds.Left + 72, e.Bounds.Top + 2, e.Bounds.Width - 84, e.Bounds.Height - 4), TextMain, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void SetBusy(bool busy, string? caption = null)
    {
        _add.Enabled = _install.Enabled = _remove.Enabled = !busy;
        if (caption != null) SetProgress(0, caption);
    }
    private void SetProgress(int value, string caption) { _progress.Value = Math.Clamp(value, 0, 100); _progressText.Text = caption + " · " + _progress.Value + "%"; }
    private void LoadMetadata()
    {
        try
        {
            if (!File.Exists(_metadataPath)) return;
            var data = JsonSerializer.Deserialize<Dictionary<string, PrinterMeta>>(File.ReadAllText(_metadataPath));
            if (data != null) foreach (var pair in data) if (Directory.Exists(Path.Combine(_library, pair.Key))) _metadata[pair.Key] = pair.Value;
        }
        catch { }
    }
    private void SaveMetadata() => File.WriteAllText(_metadataPath, JsonSerializer.Serialize(_metadata, new JsonSerializerOptions { WriteIndented = true }));
    private static GroupBox MakeGroup(string title) => new() { Text = title, ForeColor = Color.FromArgb(210, 197, 235), BackColor = Surface, Padding = new Padding(10), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
    private static void StyleButton(Button b, bool secondary)
    {
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Border; b.FlatAppearance.BorderSize = secondary ? 1 : 0;
        b.BackColor = secondary ? Surface2 : Purple; b.ForeColor = TextMain; b.Cursor = Cursors.Hand;
        b.FlatAppearance.MouseOverBackColor = secondary ? Color.FromArgb(52, 45, 67) : PurpleHover;
    }
    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath(); int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    private static string SafeName(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    private static string? Prompt(string title, string value)
    {
        using var form = new Form { Text = "ONKOS", StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ClientSize = new Size(430, 130), BackColor = Surface, ForeColor = TextMain, Font = new Font("Segoe UI", 9F) };
        var label = new Label { Text = title, Location = new Point(16, 16), AutoSize = true, ForeColor = TextMain };
        var box = new TextBox { Text = value, Location = new Point(16, 44), Width = 398, BackColor = Surface2, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle };
        var ok = new Button { Text = "Guardar", DialogResult = DialogResult.OK, Location = new Point(230, 82), Size = new Size(88, 30) };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(326, 82), Size = new Size(88, 30) };
        StyleButton(ok, false); StyleButton(cancel, true); form.Controls.AddRange(new Control[] { label, box, ok, cancel }); form.AcceptButton = ok; form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? box.Text : null;
    }

    private sealed class SelectInstallerForm : Form
    {
        public string? SelectedPath { get; private set; }
        public SelectInstallerForm(List<string> candidates)
        {
            Text = "Seleccionar instalador principal"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(620, 390); BackColor = Bg; ForeColor = TextMain; Font = new Font("Segoe UI", 9F);
            var list = new ListBox { Dock = DockStyle.Fill, BackColor = Surface2, ForeColor = TextMain, BorderStyle = BorderStyle.None };
            foreach (string path in candidates) list.Items.Add(path);
            list.SelectedIndex = 0;
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Surface };
            var ok = new Button { Text = "Seleccionar", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 130 };
            StyleButton(ok, false); bottom.Controls.Add(ok); Controls.Add(list); Controls.Add(bottom);
            list.DoubleClick += (_, _) => { if (list.SelectedItem != null) { SelectedPath = list.SelectedItem.ToString(); DialogResult = DialogResult.OK; Close(); } };
            FormClosing += (_, _) => { if (DialogResult == DialogResult.OK && list.SelectedItem != null) SelectedPath = list.SelectedItem.ToString(); };
        }
    }
}
