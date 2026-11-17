using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal sealed class PrinterManagerForm : UserControl
{
    public event Action? BackRequested;

    private readonly string _library = Path.Combine(AppPaths.DriversDir);
    private readonly string _logos = Path.Combine(AppPaths.DriversDir, ".logos");
    private readonly string _metadataPath = Path.Combine(AppPaths.DriversDir, "printers.json");
    private readonly Dictionary<string, PrinterMeta> _metadata = new(StringComparer.OrdinalIgnoreCase);

    private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Tile, FullRowSelect = true, MultiSelect = false, BorderStyle = BorderStyle.None, OwnerDraw = true, HideSelection = false };
    private readonly ImageList _images = new() { ImageSize = new Size(44, 44), ColorDepth = ColorDepth.Depth32Bit };

    private readonly ListView _installedList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, BorderStyle = BorderStyle.None, HideSelection = false };
    private readonly Button _refreshInstalled = new() { Text = "↻   ACTUALIZAR" };
    private readonly Button _openQueue = new() { Text = "ABRIR COLA" };
    private readonly Button _setDefault = new() { Text = "PREDETERMINADA" };
    private readonly Button _removeInstalled = new() { Text = "ELIMINAR" };

    private readonly TextBox _search = new() { PlaceholderText = "Buscar impresora o modelo...", BorderStyle = BorderStyle.None };
    private readonly Button _clear = new() { Text = "×", Width = 34, Height = 30, Font = new Font("Segoe UI", 16F, FontStyle.Bold) };
    private readonly Button _add = new() { Text = "+   AGREGAR DRIVER" };
    private readonly Button _install = new() { Text = "▶   INSTALAR IMPRESORA" };
    private readonly Button _remove = new() { Text = "✕   ELIMINAR IMPRESORA" };
    private readonly ModernProgressBar _progress = new();
    private readonly Label _progressText = new() { Text = "Listo para comenzar", ForeColor = OnkosTheme.TextMuted, Dock = DockStyle.Top, Height = 22 };
    private readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = OnkosTheme.TextMuted };

    private TabControl? _tabs;
    private Panel? _libraryActionBar;
    private Panel? _installedActionBar;

    private sealed class PrinterMeta
    {
        public string DisplayName { get; set; } = "";
        public string Model { get; set; } = "";
        public string InstallerRelativePath { get; set; } = "";
        public string? LogoPath { get; set; }
        public string InstallType { get; set; } = "exe";
    }

    private sealed record InstalledPrinter(string Name, string Driver, string Port, bool IsDefault, string Status);

    public PrinterManagerForm()
    {
        Dock = DockStyle.Fill; BackColor = OnkosTheme.Bg; ForeColor = OnkosTheme.TextMain; Font = new Font("Segoe UI", 9.5F);
        Directory.CreateDirectory(_library); Directory.CreateDirectory(_logos); LoadMetadata();

        // ── HEADER con helper del tema ──
        var header = OnkosTheme.CreateHeader(
            "Impresoras y controladores",
            "Biblioteca local de drivers e impresoras instaladas en el equipo",
            () => BackRequested?.Invoke());

        _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Padding = new Point(16, 8) };
        StyleTabs(_tabs);

        // ── Pestaña Biblioteca ──
        var libraryTab = new TabPage("Biblioteca de drivers") { BackColor = OnkosTheme.Bg, Padding = new Padding(0) };
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12), BackColor = OnkosTheme.Bg };
        var group = OnkosTheme.MakeGroup("01  ·  Biblioteca de impresoras"); group.Dock = DockStyle.Fill;
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = OnkosTheme.Surface };
        var searchBox = new Panel { Bounds = new Rectangle(10, 6, 390, 36), BackColor = OnkosTheme.Surface2 };
        searchBox.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(_search.Focused ? OnkosTheme.PurpleHover : OnkosTheme.Border, _search.Focused ? 1.8F : 1.2F);
            using var path = OnkosTheme.RoundedRect(new Rectangle(1, 1, searchBox.Width - 3, searchBox.Height - 3), 8);
            e.Graphics.DrawPath(pen, path);
        };
        var searchIcon = new Label { Text = "⌕", Font = new Font("Segoe UI", 21F, FontStyle.Bold), ForeColor = OnkosTheme.PurpleHover, TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(8, 0, 34, 34) };
        _search.SetBounds(43, 8, 300, 20); _search.BackColor = OnkosTheme.Surface2; _search.ForeColor = OnkosTheme.TextMain; _search.Font = new Font("Segoe UI", 10F);
        _clear.SetBounds(347, 2, 36, 30); _clear.FlatStyle = FlatStyle.Flat; _clear.FlatAppearance.BorderSize = 0; _clear.BackColor = OnkosTheme.Surface2; _clear.ForeColor = OnkosTheme.TextMuted;
        _clear.Click += (_, _) => { _search.Clear(); _search.Focus(); };
        searchBox.Controls.Add(searchIcon); searchBox.Controls.Add(_search); searchBox.Controls.Add(_clear); toolbar.Controls.Add(searchBox);

        _list.BackColor = OnkosTheme.Surface; _list.ForeColor = OnkosTheme.TextMain; _list.Font = new Font("Segoe UI", 10F); _list.SmallImageList = _images;
        _list.TileSize = new Size(350, 64);
        _list.Resize += (_, _) => _list.TileSize = new Size(Math.Max(280, (_list.ClientSize.Width - 30) / 3), 64);
        var listHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = OnkosTheme.Purple };
        listHeader.Controls.Add(new Label { Text = "Impresora / Modelo", Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) });
        _list.DrawItem += DrawPrinterRow;
        _list.MouseDoubleClick += ListMouseDoubleClick;
        group.Controls.Add(_list); group.Controls.Add(listHeader); group.Controls.Add(toolbar);

        _libraryActionBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = OnkosTheme.Bg, Padding = new Padding(3, 2, 3, 2) };
        foreach (var b in new[] { _add, _install, _remove }) { b.Font = new Font("Segoe UI", 9F, FontStyle.Bold); b.Height = 46; }
        OnkosTheme.StyleButton(_add, ButtonKind.Primary);
        OnkosTheme.StyleButton(_install, ButtonKind.Success);
        OnkosTheme.StyleButton(_remove, ButtonKind.Danger);
        void LayoutLibrary()
        {
            int gap = 6, w = _libraryActionBar.ClientSize.Width, each = (w - gap * 2) / 3;
            _add.SetBounds(0, 0, each, 46); _install.SetBounds(each + gap, 0, each, 46); _remove.SetBounds((each + gap) * 2, 0, w - (each + gap) * 2, 46);
        }
        _libraryActionBar.Resize += (_, _) => LayoutLibrary(); LayoutLibrary();
        _libraryActionBar.Controls.AddRange(new Control[] { _add, _install, _remove });
        content.Controls.Add(group);
        libraryTab.Controls.Add(content);
        libraryTab.Controls.Add(_libraryActionBar);
        _tabs.TabPages.Add(libraryTab);

        // ── Pestaña Instaladas ──
        var installedTab = new TabPage("Impresoras instaladas") { BackColor = OnkosTheme.Bg, Padding = new Padding(0) };
        var installedContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12), BackColor = OnkosTheme.Bg };
        var installedGroup = OnkosTheme.MakeGroup("Impresoras en este computador"); installedGroup.Dock = DockStyle.Fill;

        var installedToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = OnkosTheme.Surface };
        OnkosTheme.StyleButton(_refreshInstalled, ButtonKind.Primary);
        _refreshInstalled.SetBounds(10, 6, 150, 38);
        _refreshInstalled.Click += (_, _) => RefreshInstalled();
        installedToolbar.Controls.Add(_refreshInstalled);
        installedToolbar.Controls.Add(new Label
        {
            Text = "Estas son las impresoras instaladas en Windows. Haz doble clic para abrir la cola de impresión.",
            ForeColor = OnkosTheme.TextMuted, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(170, 6, 700, 38)
        });

        _installedList.BackColor = OnkosTheme.Surface; _installedList.ForeColor = OnkosTheme.TextMain; _installedList.Font = new Font("Segoe UI", 9.5F);
        _installedList.Columns.Add("Nombre", 260);
        _installedList.Columns.Add("Driver", 260);
        _installedList.Columns.Add("Puerto", 180);
        _installedList.Columns.Add("Estado", 200);
        _installedList.Columns.Add("Predeterminada", 120);
        _installedList.Resize += (_, _) =>
        {
            if (_installedList.Columns.Count < 5) return;
            int total = _installedList.ClientSize.Width;
            _installedList.Columns[0].Width = (int)(total * 0.28);
            _installedList.Columns[1].Width = (int)(total * 0.28);
            _installedList.Columns[2].Width = (int)(total * 0.18);
            _installedList.Columns[3].Width = (int)(total * 0.16);
            _installedList.Columns[4].Width = (int)(total * 0.10);
        };
        _installedList.MouseDoubleClick += (_, e) =>
        {
            var hit = _installedList.GetItemAt(e.X, e.Y);
            if (hit?.Tag is InstalledPrinter ip) OpenPrinterQueue(ip.Name);
        };
        installedGroup.Controls.Add(_installedList); installedGroup.Controls.Add(installedToolbar);

        _installedActionBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = OnkosTheme.Bg, Padding = new Padding(3, 2, 3, 2) };
        foreach (var b in new[] { _openQueue, _setDefault, _removeInstalled }) { b.Font = new Font("Segoe UI", 9F, FontStyle.Bold); b.Height = 46; }
        OnkosTheme.StyleButton(_openQueue, ButtonKind.Primary);
        OnkosTheme.StyleButton(_setDefault, ButtonKind.Secondary);
        OnkosTheme.StyleButton(_removeInstalled, ButtonKind.Danger);
        void LayoutInstalled()
        {
            int gap = 6, w = _installedActionBar.ClientSize.Width, each = (w - gap * 2) / 3;
            _openQueue.SetBounds(0, 0, each, 46); _setDefault.SetBounds(each + gap, 0, each, 46); _removeInstalled.SetBounds((each + gap) * 2, 0, w - (each + gap) * 2, 46);
        }
        _installedActionBar.Resize += (_, _) => LayoutInstalled(); LayoutInstalled();
        _installedActionBar.Controls.AddRange(new Control[] { _openQueue, _setDefault, _removeInstalled });
        installedContent.Controls.Add(installedGroup);
        installedTab.Controls.Add(installedContent);
        installedTab.Controls.Add(_installedActionBar);
        _tabs.TabPages.Add(installedTab);

        var progressPanel = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = OnkosTheme.Surface, Padding = new Padding(10, 4, 10, 4) };
        _progressText.Dock = DockStyle.Top; _progress.Dock = DockStyle.Bottom; _progress.Height = 25;
        progressPanel.Controls.Add(_progress); progressPanel.Controls.Add(_progressText);
        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = OnkosTheme.Surface, Padding = new Padding(8, 0, 0, 0) };
        statusPanel.Controls.Add(_status);

        // ── BARRA DE ESTADO INFERIOR ──
        var statusBar = OnkosTheme.CreateStatusBar("Impresoras");

        Controls.Add(_tabs); Controls.Add(progressPanel); Controls.Add(statusPanel); Controls.Add(statusBar); Controls.Add(header);

        _add.Click += async (_, _) => await AddDriverAsync();
        _install.Click += async (_, _) => await InstallSelectedAsync();
        _remove.Click += (_, _) => RemoveSelected();
        _search.TextChanged += (_, _) => RefreshList();
        _openQueue.Click += (_, _) => { var p = SelectedInstalled(); if (p != null) OpenPrinterQueue(p.Name); };
        _setDefault.Click += (_, _) => { var p = SelectedInstalled(); if (p != null) SetDefaultPrinter(p.Name); };
        _removeInstalled.Click += (_, _) => { var p = SelectedInstalled(); if (p != null) DeleteInstalledPrinter(p.Name); };
        _tabs.SelectedIndexChanged += (_, _) => { if (_tabs.SelectedIndex == 1) RefreshInstalled(); };

        RefreshList();
        RefreshInstalled();
    }

    private async Task AddDriverAsync()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Selecciona instaladores (.exe/.msi/.inf) y sus archivos auxiliares",
            Filter = "Todos los archivos (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK || picker.FileNames.Length == 0) return;

        var selectedFiles = picker.FileNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var candidates = selectedFiles.Where(f => IsInstallerCandidate(f)).ToList();
        var auxiliaries = selectedFiles.Where(f => !IsInstallerCandidate(f)).ToList();

        if (candidates.Count == 0)
        {
            OnkosMessageBox.Show(this,
                "No se encontró ningún instalador (.exe, .msi, .inf) entre los archivos seleccionados.",
                "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var groups = new List<(string Installer, List<string> Files)>();
        foreach (var candidate in candidates)
        {
            var groupFiles = new List<string> { candidate };
            string candidateDir = Path.GetDirectoryName(candidate)!;
            foreach (var aux in auxiliaries)
            {
                if (string.Equals(Path.GetDirectoryName(aux), candidateDir, StringComparison.OrdinalIgnoreCase))
                    groupFiles.Add(aux);
            }
            groups.Add((candidate, groupFiles));
        }

        long total = Math.Max(1, groups.Sum(g => g.Files.Sum(f => new FileInfo(f).Length)));
        long copied = 0;
        int added = 0;
        var addedNames = new List<string>();
        var createdFolders = new List<string>();

        SetBusy(true, "Preparando carga de controladores...");
        try
        {
            foreach (var group in groups)
            {
                string installer = group.Installer;
                string display = Path.GetFileNameWithoutExtension(installer);
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
                ActivityLog.Write("PRINTERS", "Add driver", "OK", display);
            }

            SaveMetadata();
            RefreshList();
            SetProgress(100, added + " controlador(es) agregado(s)");
            string summary = "Se agregaron " + added + " controlador(es) a la biblioteca.";
            if (auxiliaries.Count > 0) summary += "\n\nSe copiaron " + auxiliaries.Count + " archivo(s) auxiliar(es) junto a su instalador.";
            summary += "\n\n" + string.Join("\n", addedNames);
            OnkosMessageBox.Show(this, summary, "ONKOS · Controladores agregados", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            foreach (string folder in createdFolders) { try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { } }
            foreach (string key in _metadata.Keys.Where(k => createdFolders.Contains(Path.Combine(_library, k), StringComparer.OrdinalIgnoreCase)).ToList()) _metadata.Remove(key);
            try { SaveMetadata(); } catch { }
            OnkosMessageBox.Show(this, ex.Message, "Error al agregar controladores", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetProgress(0, "No se pudieron cargar todos los controladores");
        }
        finally { SetBusy(false); }
    }

    private static bool IsInstallerCandidate(string file)
    {
        var ext = Path.GetExtension(file);
        return ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".msi", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".inf", StringComparison.OrdinalIgnoreCase);
    }

    private InstalledPrinter? SelectedInstalled()
    {
        if (_installedList.SelectedItems.Count == 0)
        {
            OnkosMessageBox.Show(this, "Selecciona una impresora de la lista.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }
        return _installedList.SelectedItems[0].Tag as InstalledPrinter;
    }

    private void RefreshInstalled()
    {
        _installedList.BeginUpdate();
        _installedList.Items.Clear();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DriverName, PortName, Default, WorkOffline FROM Win32_Printer");
            foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
            {
                string name = obj["Name"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name.StartsWith("Microsoft ", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("OneNote", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Fax", StringComparison.OrdinalIgnoreCase)) continue;
                string driver = obj["DriverName"]?.ToString() ?? "";
                string port = obj["PortName"]?.ToString() ?? "";
                bool isDefault = obj["Default"] is bool b && b;
                bool offline = obj["WorkOffline"] is bool w && w;
                string status = offline ? "Offline" : "Normal";
                var ip = new InstalledPrinter(name, driver, port, isDefault, status);
                var item = new ListViewItem(name) { Tag = ip };
                item.SubItems.Add(driver);
                item.SubItems.Add(port);
                item.SubItems.Add(status);
                item.SubItems.Add(isDefault ? "Sí" : "");
                if (isDefault) item.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                _installedList.Items.Add(item);
            }
            _status.Text = _installedList.Items.Count + " impresora(s) instalada(s) en el sistema.";
        }
        catch (Exception ex)
        {
            _status.Text = "No se pudo leer la lista de impresoras: " + ex.Message;
        }
        finally { _installedList.EndUpdate(); }
    }

    private void OpenPrinterQueue(string printerName)
    {
        try { Process.Start(new ProcessStartInfo { FileName = "rundll32.exe", Arguments = "printui.dll,PrintUIEntry /o /n \"" + printerName + "\"", UseShellExecute = true }); }
        catch (Exception ex) { OnkosMessageBox.Show(this, "No se pudo abrir la cola: " + ex.Message, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void SetDefaultPrinter(string printerName)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo { FileName = "rundll32.exe", Arguments = "printui.dll,PrintUIEntry /y /n \"" + printerName + "\"", UseShellExecute = true, CreateNoWindow = true });
            p?.WaitForExit(5000);
            ActivityLog.Write("PRINTERS", "Set default", "OK", printerName);
            RefreshInstalled();
        }
        catch (Exception ex) { OnkosMessageBox.Show(this, "No se pudo cambiar la predeterminada: " + ex.Message, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void DeleteInstalledPrinter(string printerName)
    {
        if (OnkosMessageBox.Show(this, $"¿Eliminar la impresora \"{printerName}\"?", "Eliminar impresora instalada", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            using var p = Process.Start(new ProcessStartInfo { FileName = "rundll32.exe", Arguments = "printui.dll,PrintUIEntry /dl /n \"" + printerName + "\" /q", UseShellExecute = true, CreateNoWindow = true });
            p?.WaitForExit(10000);
            ActivityLog.Write("PRINTERS", "Delete installed", "OK", printerName);
            RefreshInstalled();
        }
        catch (Exception ex) { OnkosMessageBox.Show(this, "No se pudo eliminar: " + ex.Message, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static void StyleTabs(TabControl tabs)
    {
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.ItemSize = new Size(220, 38);
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.DrawItem += (_, e) =>
        {
            var tab = tabs.TabPages[e.Index];
            var rect = tabs.GetTabRect(e.Index);
            bool selected = tabs.SelectedIndex == e.Index;
            using var bg = new SolidBrush(selected ? OnkosTheme.Purple : OnkosTheme.Surface);
            e.Graphics.FillRectangle(bg, rect);
            using var text = new SolidBrush(selected ? Color.White : OnkosTheme.TextMuted);
            var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(tab.Text, tabs.Font, text, rect, format);
        };
    }

    private async Task InstallSelectedAsync()
    {
        var item = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null;
        if (item?.Tag is not string key || !_metadata.TryGetValue(key, out var meta))
        { OnkosMessageBox.Show(this, "Selecciona una impresora de la biblioteca.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        string root = Path.Combine(_library, key);
        string installer = Path.GetFullPath(Path.Combine(root, meta.InstallerRelativePath));
        if (!installer.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(installer))
        { OnkosMessageBox.Show(this, "No se encuentra el instalador guardado para esta impresora.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        string msg = meta.InstallType == "inf"
            ? "Se agregará el controlador INF al almacén de controladores de Windows.\n\n¿Continuar?"
            : "Se abrirá el instalador del controlador seleccionado.\n\n¿Continuar?";
        if (OnkosMessageBox.Show(this, msg, "Instalar " + meta.DisplayName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

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
            ActivityLog.Write("PRINTERS", "Install driver", exitCode == 0 ? "OK" : "ERROR", $"{meta.DisplayName} | ExitCode={exitCode}");
            if (exitCode == 0)
                OnkosMessageBox.Show(this, "El instalador terminó correctamente.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                OnkosMessageBox.Show(this, "El proceso terminó con código " + exitCode + ".", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            SetProgress(0, "Error durante la instalación");
            ActivityLog.Write("PRINTERS", "Install driver", "ERROR", ex.Message);
            OnkosMessageBox.Show(this, ex.Message, "ONKOS · Error de instalación", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false); }
    }

    private void RemoveSelected()
    {
        var item = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null;
        if (item?.Tag is not string key) { OnkosMessageBox.Show(this, "Selecciona una impresora para eliminar."); return; }
        if (OnkosMessageBox.Show(this, "¿Eliminar " + item.Text + " y sus archivos de la biblioteca local?", "Eliminar impresora", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { Directory.Delete(Path.Combine(_library, key), true); } catch (Exception ex) { OnkosMessageBox.Show(this, ex.Message, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        _metadata.Remove(key); SaveMetadata(); RefreshList();
    }

    private void ListMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        var item = _list.GetItemAt(e.X, e.Y);
        if (item?.Tag is not string key) return;
        int relativeX = e.X - item.Bounds.Left;
        if (relativeX >= 12 && relativeX <= 64) ChangeLogo(item);
        else if (relativeX >= 72) RenameSelected();
    }

    private void RenameSelected()
    {
        var item = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null;
        if (item?.Tag is not string key || !_metadata.TryGetValue(key, out var meta)) { OnkosMessageBox.Show(this, "Selecciona una impresora."); return; }
        string? value = Prompt("Nombre visible de la impresora:", meta.DisplayName);
        if (string.IsNullOrWhiteSpace(value)) return;
        meta.DisplayName = value.Trim(); SaveMetadata(); RefreshList();
    }

    private void ChangeLogo(ListViewItem? selectedItem = null)
    {
        var item = selectedItem ?? (_list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null);
        if (item?.Tag is not string key || !_metadata.TryGetValue(key, out var meta)) { OnkosMessageBox.Show(this, "Selecciona una impresora."); return; }
        using var dialog = new OpenFileDialog { Title = "Selecciona el logo de la impresora", Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string target = Path.Combine(_logos, key + ".png");
            using var image = Image.FromFile(dialog.FileName); using var bitmap = new Bitmap(image, new Size(64, 64)); bitmap.Save(target, System.Drawing.Imaging.ImageFormat.Png);
            meta.LogoPath = target; SaveMetadata(); RefreshList();
        }
        catch (Exception ex) { OnkosMessageBox.Show(this, ex.Message, "ONKOS · Logo", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
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
        using var bg = new SolidBrush(e.Item.Selected ? Color.FromArgb(54, 45, 72) : OnkosTheme.Surface); g.FillRectangle(bg, e.Bounds);
        using var line = new Pen(OnkosTheme.Border); g.DrawLine(line, e.Bounds.Left + 10, e.Bounds.Bottom - 1, e.Bounds.Right - 10, e.Bounds.Bottom - 1);
        int imageIndex = e.Item.ImageIndex;
        if (imageIndex >= 0 && imageIndex < _images.Images.Count) g.DrawImage(_images.Images[imageIndex], e.Bounds.Left + 16, e.Bounds.Top + 9, 42, 42);
        TextRenderer.DrawText(g, e.Item.Text, new Font("Segoe UI", 10F, FontStyle.Bold), new Rectangle(e.Bounds.Left + 72, e.Bounds.Top + 2, e.Bounds.Width - 84, e.Bounds.Height - 4), OnkosTheme.TextMain, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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
    private static string SafeName(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    private static string? Prompt(string title, string value)
    {
        using var form = new Form { Text = "ONKOS", StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ClientSize = new Size(430, 130), BackColor = OnkosTheme.Surface, ForeColor = OnkosTheme.TextMain, Font = new Font("Segoe UI", 9F) };
        var label = new Label { Text = title, Location = new Point(16, 16), AutoSize = true, ForeColor = OnkosTheme.TextMain };
        var box = new TextBox { Text = value, Location = new Point(16, 44), Width = 398, BackColor = OnkosTheme.Surface2, ForeColor = OnkosTheme.TextMain, BorderStyle = BorderStyle.FixedSingle };
        var ok = new Button { Text = "Guardar", DialogResult = DialogResult.OK, Location = new Point(230, 82), Size = new Size(88, 30) };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(326, 82), Size = new Size(88, 30) };
        OnkosTheme.StyleButton(ok, ButtonKind.Primary);
        OnkosTheme.StyleButton(cancel, ButtonKind.Secondary);
        form.Controls.AddRange(new Control[] { label, box, ok, cancel }); form.AcceptButton = ok; form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? box.Text : null;
    }
}
