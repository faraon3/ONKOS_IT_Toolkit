using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal sealed class BackupForm : UserControl
{
    public event Action? BackRequested;
    private readonly ListView _sources = new();
    private readonly ImageList _folderIcons = new();
    private readonly ManualDriveComboBox _drivePicker = new();
    private readonly Label _destination = new();
    private readonly TextBox _backupLabel = new();
    private readonly PurpleCheckBox _ipv4 = new() { Text = "Guardar configuración de red", Checked = true, AutoSize = false };
    private readonly ModernProgressBar _bar = new();
    private readonly Label _status = new();
    private readonly Label _currentFile = new();
    private readonly TextBox _log = new();
    private readonly Button _run = new();
    private readonly Button _restore = new();
    private readonly Button _addFolder = new();
    private readonly Button _selectAllFolders = new();
    private readonly Button _cancel = new() { Text = "CANCELAR", Visible = false };
    private readonly Panel _actionBar = new() { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.FromArgb(18, 16, 25), Padding = new Padding(3, 2, 3, 2) };
    private readonly Dictionary<string, long> _sizeCache = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private bool _busy;
    private readonly System.Windows.Forms.Timer _driveMonitor = new() { Interval = 2000 };
    private string _selectedDriveRoot = string.Empty;
    private bool _refreshingDrives;

    private static readonly Color Bg = Color.FromArgb(18, 16, 25);
    private static readonly Color Surface = Color.FromArgb(31, 27, 41);
    private static readonly Color Surface2 = Color.FromArgb(39, 34, 51);
    private static readonly Color Purple = Color.FromArgb(105, 73, 164);
    private static readonly Color PurpleHover = Color.FromArgb(126, 91, 190);
    private static readonly Color TextMain = Color.FromArgb(244, 241, 250);
    private static readonly Color TextMuted = Color.FromArgb(183, 174, 199);
    private static readonly Color Border = Color.FromArgb(65, 57, 82);

    public BackupForm()
    {
        Dock = DockStyle.Fill; BackColor = Bg; ForeColor = TextMain; Font = new Font("Segoe UI", 9.5F);

        var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Purple };
        var back = new Button { Text = "‹  VOLVER AL INICIO", Location = new Point(22, 5), Size = new Size(170, 25) };
        StyleButton(back, true); back.Click += (_, _) => BackRequested?.Invoke(); header.Controls.Add(back);
        header.Controls.Add(new Label { Text = "BACKUP  /  MIGRACIÓN", ForeColor = Color.White, Font = new Font("Segoe UI", 19F, FontStyle.Bold), AutoSize = true, Location = new Point(22, 30) });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12), AutoScroll = true, BackColor = Bg };
        var sourcesGroup = MakeGroup("01  ·  Selecciona la información");
        sourcesGroup.Dock = DockStyle.Top; sourcesGroup.Height = 310;
        _folderIcons.ColorDepth = ColorDepth.Depth32Bit; _folderIcons.ImageSize = new Size(24, 24);
        _sources.Dock = DockStyle.Fill; _sources.View = View.Tile; _sources.TileSize = new Size(250, 38); _sources.CheckBoxes = false;
        // Distribuye los elementos en cuatro columnas para que Chrome y las carpetas agregadas
        // aparezcan hacia la derecha y no queden ocultos debajo de la lista.
        void UpdateSourceTileWidth()
        {
            int available = Math.Max(840, _sources.ClientSize.Width - 16);
            int tileWidth = Math.Max(205, Math.Min(270, available / 4));
            _sources.TileSize = new Size(tileWidth, 38);
        }
        _sources.Resize += (_, _) => UpdateSourceTileWidth();
        _sources.FullRowSelect = true; _sources.HideSelection = false; _sources.BorderStyle = BorderStyle.None; _sources.CheckBoxes = false; _sources.OwnerDraw = true;
        _sources.DrawColumnHeader += (_, e) => { if (e.Header is null) return; using var brush = new SolidBrush(Purple); using var font = new Font("Segoe UI", 9.5F, FontStyle.Bold); e.Graphics.FillRectangle(brush, e.Bounds); TextRenderer.DrawText(e.Graphics, e.Header.Text ?? string.Empty, font, e.Bounds, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter); };
        _sources.DrawItem += DrawSourceTile;
        _sources.MouseDown += SourceMouseDown;
        _sources.KeyDown += (_, e) => { if (e.KeyCode == Keys.Space && _sources.SelectedItems.Count > 0) { var item = _sources.SelectedItems[0]; ToggleItem(item); e.Handled = true; } };
        _sources.BackColor = Surface; _sources.ForeColor = TextMain; _sources.Font = new Font("Segoe UI", 9.5F);
        _sources.SmallImageList = _folderIcons;
        _sources.Columns.Add("Carpeta / ubicación", 220); _sources.Columns.Add("Ruta", 500);
        _sources.Resize += (_, _) => { if (_sources.Columns.Count > 1) { _sources.Columns[0].Width = Math.Min(230, Math.Max(170, _sources.ClientSize.Width / 3)); _sources.Columns[1].Width = Math.Max(120, _sources.ClientSize.Width - _sources.Columns[0].Width - 4); } };
        LoadKnownFolders();
        var sourceButtons = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Surface };
        _addFolder.Text = "Agregar carpeta"; StyleButton(_addFolder, true);
        _addFolder.SetBounds(10, 4, 210, 36);
        _addFolder.ImageAlign = ContentAlignment.MiddleLeft;
        _addFolder.TextImageRelation = TextImageRelation.ImageBeforeText;
        _addFolder.Padding = new Padding(8, 0, 6, 0);
        string addFolderIcon = Path.Combine(AppContext.BaseDirectory, "Assets", "AgregarCarpeta.png");
        if (File.Exists(addFolderIcon)) { using var icon = Image.FromFile(addFolderIcon); _addFolder.Image = new Bitmap(icon, new Size(20, 20)); }
        _addFolder.Click += (_, _) => AddCustomFolder();
        var addUser = new Button { Text = "Otro usuario", Location = new Point(286, 6), Size = new Size(190, 30), ImageAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText, Padding = new Padding(8, 0, 6, 0) };
        StyleButton(addUser, true);
        string otherUserIcon = Path.Combine(AppContext.BaseDirectory, "Assets", "OtroUsuario.png");
        if (File.Exists(otherUserIcon)) { using var icon = Image.FromFile(otherUserIcon); addUser.Image = new Bitmap(icon, new Size(20, 20)); }
        addUser.Click += (_, _) => AddOtherUserFolders();
        sourceButtons.Controls.Add(_addFolder);
        // Fila inferior según el diseño solicitado.
        addUser.Location = new Point(225, 4);
        addUser.Size = new Size(240, 36);
        sourceButtons.Controls.Add(addUser);
        StyleCheck(_ipv4);
        _ipv4.SetBounds(475, 4, 330, 36);
        _ipv4.Text = "Guardar configuración de red";
        _ipv4.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        string networkSaveIcon = Path.Combine(AppContext.BaseDirectory, "Assets", "GuardarConfiguracionRed.png");
        if (File.Exists(networkSaveIcon))
        {
            try { using var networkImage = Image.FromFile(networkSaveIcon); _ipv4.Image = new Bitmap(networkImage, new Size(24, 24)); _ipv4.ImageAlign = ContentAlignment.MiddleLeft; _ipv4.TextImageRelation = TextImageRelation.ImageBeforeText; _ipv4.Padding = new Padding(6, 0, 0, 0); } catch { }
        }
        sourceButtons.Controls.Add(_ipv4);
        var selectionBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Surface, Padding = new Padding(0, 2, 8, 2) };
        _selectAllFolders.Text = "Seleccionar todo";
        _selectAllFolders.Size = new Size(150, 28);
        _selectAllFolders.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _selectAllFolders.Location = new Point(8, 2);
        StyleButton(_selectAllFolders, true);
        _selectAllFolders.Click += (_, _) => SetAllFoldersSelected(_sources.Items.Cast<ListViewItem>().Any(item => !IsItemChecked(item)));
        selectionBar.Controls.Add(_selectAllFolders);
        sourcesGroup.Controls.Add(_sources); sourcesGroup.Controls.Add(sourceButtons); sourcesGroup.Controls.Add(selectionBar);
        UpdateSelectAllButtonText();

        var destinationGroup = MakeGroup("02  ·  Elige dónde guardar el respaldo");
        destinationGroup.Dock = DockStyle.Top; destinationGroup.Height = 84;
        var destinationRow = new Panel { Dock = DockStyle.Fill, BackColor = Surface };
        _drivePicker.DropDownStyle = ComboBoxStyle.DropDownList; StyleCombo(_drivePicker); _drivePicker.DrawMode = DrawMode.OwnerDrawFixed; _drivePicker.ItemHeight = 25; _drivePicker.DrawItem += DrawDriveItem;
        _drivePicker.SetBounds(10, 8, 300, 34);
        _drivePicker.SelectedIndexChanged += (_, _) => { if (!_refreshingDrives && _drivePicker.SelectedItem is DriveChoice d) { _selectedDriveRoot = d.Root; _destination.Text = d.Root; } };
        _drivePicker.DropDown += (_, _) => RefreshDrives();
        var choose = new Button { Text = "Examinar carpeta", Width = 180, Height = 34, Location = new Point(322, 8), ImageAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText, Padding = new Padding(8, 0, 6, 0) };
        string browseIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ExaminarCarpeta.png");
        if (File.Exists(browseIconPath)) { try { using var img = Image.FromFile(browseIconPath); choose.Image = new Bitmap(img, new Size(20, 20)); } catch { } }
        StyleButton(choose, true); choose.Click += (_, _) => ChooseDestination();
        _destination.AutoEllipsis = false;
        _destination.TextAlign = ContentAlignment.MiddleLeft;
        _destination.BackColor = Surface;
        _destination.ForeColor = TextMain;
        _destination.Font = new Font("Segoe UI", 9F);
        _destination.SetBounds(514, 8, 480, 34);
        _destination.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var destinationTip = new ToolTip();
        void FitDestinationPath()
        {
            int available = Math.Max(80, _destination.ClientSize.Width - 4);
            float fontSize = 9F;
            while (fontSize > 6.5F && TextRenderer.MeasureText(_destination.Text, new Font("Segoe UI", fontSize), new Size(int.MaxValue, 34), TextFormatFlags.SingleLine).Width > available)
                fontSize -= 0.25F;
            _destination.Font = new Font("Segoe UI", fontSize, FontStyle.Regular);
            destinationTip.SetToolTip(_destination, _destination.Text);
        }
        _destination.Resize += (_, _) => FitDestinationPath();
        _destination.TextChanged += (_, _) => FitDestinationPath();
        destinationRow.Controls.Add(_drivePicker); destinationRow.Controls.Add(choose); destinationRow.Controls.Add(_destination);
        void LayoutDestinationRow()
        {
            int width = destinationRow.ClientSize.Width;
            int comboWidth = Math.Clamp((int)(width * 0.31), 190, 300);
            int browseWidth = Math.Clamp((int)(width * 0.19), 155, 180);
            int x = 10;
            int controlHeight = 34;
            int y = Math.Max(4, (destinationRow.ClientSize.Height - controlHeight) / 2);
            _drivePicker.SetBounds(x, y, comboWidth, controlHeight);
            choose.SetBounds(x + comboWidth + 12, y, browseWidth, controlHeight);
            int pathX = x + comboWidth + 12 + browseWidth + 12;
            _destination.SetBounds(pathX, y, Math.Max(40, width - pathX - 12), controlHeight);
        }
        destinationRow.Resize += (_, _) => LayoutDestinationRow();
        LayoutDestinationRow();
        destinationGroup.Controls.Add(destinationRow);
        RefreshDrives();
        _driveMonitor.Tick += (_, _) => { if (!IsDisposed && IsHandleCreated && Visible) RefreshDrives(); };
        _driveMonitor.Start();
        Disposed += (_, _) => { _driveMonitor.Stop(); _driveMonitor.Dispose(); };

        var progressGroup = MakeGroup("03  ·  Progreso de la copia");
        progressGroup.Dock = DockStyle.Top; progressGroup.Height = 112;
        var progressPanel = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(3), BackColor = Surface2 };
        _bar.Dock = DockStyle.Fill; _bar.Value = 0;
        progressPanel.Controls.Add(_bar);
        _status.Dock = DockStyle.Top; _status.Height = 24; _status.Text = "Listo para comenzar"; _status.ForeColor = TextMain;
        _currentFile.Dock = DockStyle.Top; _currentFile.Height = 25; _currentFile.Text = "Ahora: —"; _currentFile.ForeColor = TextMuted; _currentFile.AutoEllipsis = true;
        progressGroup.Controls.Add(_currentFile); progressGroup.Controls.Add(_status); progressGroup.Controls.Add(progressPanel);

        var logGroup = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Surface, Margin = new Padding(0, 0, 0, 8) };
        var logToggle = new Button { Text = "Ver detalles técnicos  ▸", Location = new Point(8, 5), Size = new Size(280, 32), TextAlign = ContentAlignment.MiddleLeft, ImageAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText, Padding = new Padding(8, 0, 6, 0) };
        StyleButton(logToggle, true);
        string technicalIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "VerDetallesTecnicos.png");
        if (File.Exists(technicalIconPath)) { try { using var img = Image.FromFile(technicalIconPath); logToggle.Image = new Bitmap(img, new Size(22, 22)); } catch { } }
        _log.SetBounds(8, 42, 850, 75); _log.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _log.Multiline = true; _log.ReadOnly = true; _log.ScrollBars = ScrollBars.Vertical;
        _log.Font = new Font("Consolas", 9F); _log.BackColor = Color.FromArgb(13, 12, 18); _log.ForeColor = Color.FromArgb(203, 195, 218); _log.BorderStyle = BorderStyle.None;
        _log.Visible = false;
        logToggle.Click += (_, _) =>
        {
            _log.Visible = !_log.Visible;
            logGroup.Height = _log.Visible ? 125 : 42;
            logToggle.Text = _log.Visible ? "Ocultar detalles técnicos  ▾" : "Ver detalles técnicos  ▸";
        };
        logGroup.Controls.Add(_log); logGroup.Controls.Add(logToggle);

        _run.Text = "INICIAR COPIA DE SEGURIDAD"; StyleButton(_run, false);
        _run.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        _restore.Text = "RESTAURAR BACKUP"; StyleButton(_restore, true);
        _restore.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        StyleButton(_cancel, true);
        _cancel.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        _actionBar.Resize += (_, _) => LayoutActions();
        LayoutActions();
        _run.Click += async (_, _) => await StartBackupAsync();
        _restore.Click += async (_, _) => await RestoreBackupAsync();
        _cancel.Click += (_, _) => { try { _cts?.Cancel(); } catch { } };
        _actionBar.Controls.Add(_run); _actionBar.Controls.Add(_cancel); _actionBar.Controls.Add(_restore);
        content.Controls.Add(logGroup); content.Controls.Add(progressGroup); content.Controls.Add(destinationGroup); content.Controls.Add(sourcesGroup);
        Controls.Add(content); Controls.Add(_actionBar); Controls.Add(header);

        ApplyLastSession();
        _ = ScanFoldersAndSelectWithDataAsync();
    }

    private async Task ScanFoldersAndSelectWithDataAsync()
    {
        if (_sources.Items.Count == 0) return;

        _status.Text = "Analizando carpetas y buscando archivos...";
        WriteLog("Análisis inicial: calculando el tamaño de todas las carpetas para preseleccionar solo las que contienen datos.");

        var items = _sources.Items.Cast<ListViewItem>()
            .Where(item => item.Tag is FolderEntry)
            .ToList();

        var results = await Task.Run(() => items.Select(item =>
        {
            var entry = (FolderEntry)item.Tag!;
            return (Item: item, Entry: entry, Scan: ScanFolderContents(entry.Path));
        }).ToList());

        if (IsDisposed || Disposing) return;

        int selectedCount = 0;
        int emptyCount = 0;
        int inaccessibleCount = 0;
        foreach (var result in results)
        {
            var scan = result.Scan;
            if (scan.AccessError)
            {
                // No marcar como vacía una carpeta cuyo contenido no se pudo revisar.
                SetItemChecked(result.Item, false);
                result.Item.Text = result.Entry.Label + " (No analizado)";
                result.Item.SubItems[1].Text = ShortenFolderPath(result.Entry.Path);
                inaccessibleCount++;
                WriteLog($"No se pudo analizar (sin selección automática): {result.Entry.Path}");
                continue;
            }

            _sizeCache[result.Entry.Path] = scan.TotalBytes;
            SetItemChecked(result.Item, scan.TotalBytes > 0);
            ApplySizeToItem(result.Item, result.Entry.Path, scan.TotalBytes);
            if (scan.TotalBytes > 0) selectedCount++;
            else emptyCount++;
        }

        UpdateSelectAllButtonText();
        _status.Text = $"Análisis terminado: {selectedCount} con datos · {emptyCount} vacías · {inaccessibleCount} sin acceso";
        WriteLog($"Análisis inicial terminado. Con archivos y peso: {selectedCount}; carpetas vacías (0 B): {emptyCount}; no analizadas por permisos u otros errores: {inaccessibleCount}. Solo las carpetas con peso mayor que 0 B quedaron preseleccionadas.");
    }

    private void LayoutActions()
    {
        int w = _actionBar.ClientSize.Width;
        if (_cancel.Visible)
        {
            int each = Math.Max(1, (w - 6) / 3);
            _run.SetBounds(0, 0, each, _actionBar.ClientSize.Height);
            _cancel.SetBounds(each + 3, 0, each, _actionBar.ClientSize.Height);
            _restore.SetBounds((each + 3) * 2, 0, Math.Max(1, w - (each + 3) * 2), _actionBar.ClientSize.Height);
        }
        else
        {
            int half = Math.Max(1, (w - 6) / 2);
            _run.SetBounds(0, 0, half, _actionBar.ClientSize.Height);
            _restore.SetBounds(half + 3, 0, Math.Max(1, w - half - 3), _actionBar.ClientSize.Height);
        }
    }

    private GroupBox MakeGroup(string title) => new() { Text = title, ForeColor = Color.FromArgb(210, 197, 235), BackColor = Surface, Padding = new Padding(10), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10), FlatStyle = FlatStyle.Flat };
    private static void StyleButton(Button b, bool secondary) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Border; b.FlatAppearance.BorderSize = secondary ? 1 : 0; b.BackColor = secondary ? Surface2 : Purple; b.ForeColor = TextMain; b.Cursor = Cursors.Hand; b.FlatAppearance.MouseOverBackColor = secondary ? Color.FromArgb(52, 45, 67) : PurpleHover; }
    private static void StyleCombo(ComboBox c) { c.BackColor = Surface2; c.ForeColor = TextMain; c.FlatStyle = FlatStyle.Flat; }
    private static void StyleTextBox(TextBox t) { t.BackColor = Surface2; t.ForeColor = TextMain; t.BorderStyle = BorderStyle.FixedSingle; t.Font = new Font("Segoe UI", 10F); }
    private static void StyleCheck(CheckBox c) { c.ForeColor = TextMain; c.BackColor = Surface; c.Padding = new Padding(4); c.Cursor = Cursors.Hand; }

    private void LoadKnownFolders()
    {
        AddKnown("Escritorio", Environment.SpecialFolder.Desktop, true, "Escritorio");
        AddKnown("Documentos", Environment.SpecialFolder.MyDocuments, true, "Documentos");
        AddKnown("Descargas", null, true, "Descargas", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        AddKnown("Imágenes", Environment.SpecialFolder.MyPictures, true, "imagenes");
        AddKnown("Música", Environment.SpecialFolder.MyMusic, false, "musica");
        AddKnown("Videos", Environment.SpecialFolder.MyVideos, false, "videos");
        AddKnown("Favoritos", Environment.SpecialFolder.Favorites, false, "Favoritos");
        AddKnown("Contactos", null, false, "Contactos", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Contacts"));
        AddKnown("Búsquedas", null, false, "Busquedas", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Searches"));
        AddKnown("Juegos guardados", null, false, "Juegos Guardados", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games"));
        AddKnown("OneDrive", null, false, "One Drive", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive"));
        AddKnown("Vínculos", null, false, "Vinculos", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Links"));
        AddKnown("Objetos 3D", null, false, "Objeto-3d", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "3D Objects"));
        AddChromeProfiles();
    }

    private void AddChromeProfiles()
    {
        // Cada carpeta User Data contiene los perfiles locales de Chrome:
        // Default, Profile 1, Profile 2, etc.
        string usersRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "..", "Users");
        try
        {
            usersRoot = Path.GetFullPath(usersRoot);
            if (!Directory.Exists(usersRoot)) return;

            var excluded = new HashSet<string>(new[]
            {
                "Public", "Default", "Default User", "All Users", "defaultuser0"
            }, StringComparer.OrdinalIgnoreCase);

            foreach (string profilePath in Directory.GetDirectories(usersRoot))
            {
                string userName = Path.GetFileName(profilePath);
                if (excluded.Contains(userName)) continue;

                string chromeUserData = Path.Combine(profilePath, "AppData", "Local", "Google", "Chrome", "User Data");
                if (!Directory.Exists(chromeUserData)) continue;

                if (_sources.Items.Cast<ListViewItem>().Any(item =>
                    item.Tag is FolderEntry existing &&
                    string.Equals(existing.Path, chromeUserData, StringComparison.OrdinalIgnoreCase)))
                    continue;

                AddListItem($"Google Chrome – {userName}", chromeUserData, false, "GoogleChrome");
            }
        }
        catch (Exception ex)
        {
            WriteLog("No se pudieron detectar todos los perfiles de Chrome: " + ex.Message);
        }
    }

    private void ApplyLastSession()
    {
        try
        {
            var s = SessionState.Load();
            if (s.SelectedPaths.Count > 0)
            {
                foreach (ListViewItem item in _sources.Items)
                    if (item.Tag is FolderEntry fe && s.SelectedPaths.Contains(fe.Path, StringComparer.OrdinalIgnoreCase))
                        SetItemChecked(item, true);
            }
            if (!string.IsNullOrWhiteSpace(s.Destination)) _destination.Text = s.Destination;
            if (!string.IsNullOrWhiteSpace(s.BackupLabel)) _backupLabel.Text = s.BackupLabel;
            _ipv4.Checked = s.SaveIpv4;
            if (s.SelectedPaths.Count > 0)
                WriteLog($"Última sesión cargada: {s.SelectedPaths.Count} carpeta(s), destino {s.Destination}");
        }
        catch { }
    }

    private void SaveSession(IEnumerable<FolderEntry> selected, string dest)
    {
        new SessionState
        {
            SelectedPaths = selected.Select(x => x.Path).ToList(),
            Destination = dest,
            BackupLabel = _backupLabel.Text.Trim(),
            SaveIpv4 = _ipv4.Checked,
        }.Save();
    }

    private void ToggleItem(ListViewItem item)
    {
        SetItemChecked(item, !IsItemChecked(item));
        _sources.Invalidate(item.Bounds);
        if (IsItemChecked(item) && item.Tag is FolderEntry fe)
            _ = UpdateFolderSizeAsync(item, fe);
        UpdateSelectAllButtonText();
    }

    private void SetAllFoldersSelected(bool selected)
    {
        foreach (ListViewItem item in _sources.Items)
        {
            SetItemChecked(item, selected);
            if (selected && item.Tag is FolderEntry folder)
                _ = UpdateFolderSizeAsync(item, folder);
        }
        _sources.Invalidate();
        UpdateSelectAllButtonText();
        WriteLog(selected
            ? $"Se seleccionaron todas las carpetas disponibles ({_sources.Items.Count})."
            : "Se desmarcaron todas las carpetas.");
    }

    private void UpdateSelectAllButtonText()
    {
        if (_selectAllFolders.IsDisposed) return;
        bool allSelected = _sources.Items.Count > 0 && _sources.Items.Cast<ListViewItem>().All(item => IsItemChecked(item));
        _selectAllFolders.Text = allSelected ? "Deseleccionar todo" : "Seleccionar todo";
    }

    private async Task UpdateFolderSizeAsync(ListViewItem item, FolderEntry entry)
    {
        try
        {
            if (_sizeCache.TryGetValue(entry.Path, out var cached))
            {
                ApplySizeToItem(item, entry.Path, cached);
                return;
            }
            long size = await Task.Run(() => GetFolderSizeSafe(entry.Path));
            _sizeCache[entry.Path] = size;
            if (IsDisposed) return;
            ApplySizeToItem(item, entry.Path, size);
        }
        catch { }
    }

    private void ApplySizeToItem(ListViewItem item, string path, long size)
    {
        try
        {
            if (item.Tag is FolderEntry entry)
                item.Text = $"{entry.Label} ({FormatSize(size)})";
            if (item.SubItems.Count > 1)
                item.SubItems[1].Text = ShortenFolderPath(path);
            _sources.Invalidate(item.Bounds);
        }
        catch { }
    }

    private static string ShortenFolderPath(string path)
    {
        try
        {
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] parts = trimmed.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 3) return trimmed;

            // Conserva los últimos niveles para identificar la carpeta sin ocupar toda la línea.
            return "…\\" + string.Join("\\", parts[^3..]);
        }
        catch { return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)); }
    }

    private static (long TotalBytes, bool AccessError) ScanFolderContents(string path)
    {
        long total = 0;
        bool accessError = false;
        try
        {
            if (!Directory.Exists(path)) return (0, true);

            var pending = new Stack<string>();
            pending.Push(path);
            while (pending.Count > 0)
            {
                string current = pending.Pop();

                try
                {
                    foreach (string file in Directory.EnumerateFiles(current))
                    {
                        try { total += new FileInfo(file).Length; }
                        catch { accessError = true; }
                    }
                }
                catch { accessError = true; }

                try
                {
                    foreach (string directory in Directory.EnumerateDirectories(current))
                    {
                        try
                        {
                            var attributes = File.GetAttributes(directory);
                            if ((attributes & FileAttributes.ReparsePoint) == 0)
                                pending.Push(directory);
                        }
                        catch { accessError = true; }
                    }
                }
                catch { accessError = true; }
            }
        }
        catch { accessError = true; }

        return (total, accessError);
    }

    private static long GetFolderSizeSafe(string path)
    {
        // Utilizado para mostrar el tamaño cuando se marca manualmente una carpeta.
        return ScanFolderContents(path).TotalBytes;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
        if (bytes < 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024).ToString("F1") + " MB";
        return (bytes / 1024.0 / 1024 / 1024).ToString("F2") + " GB";
    }

    private void AddKnown(string label, Environment.SpecialFolder? special, bool selected, string iconKey, string? customPath = null)
    {
        string path = customPath ?? Environment.GetFolderPath(special!.Value);
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) AddListItem(label, path, selected, iconKey);
    }
    private void AddListItem(string label, string path, bool selected, string iconKey)
    {
        int icon = GetIconIndex(iconKey, path);
        var item = new ListViewItem(label, icon) { Tag = new FolderEntry(label, path, !string.IsNullOrWhiteSpace(iconKey), selected) };
        item.SubItems.Add(path); _sources.Items.Add(item);
        if (selected) _ = UpdateFolderSizeAsync(item, (FolderEntry)item.Tag!);
        UpdateSelectAllButtonText();
    }
    private int GetIconIndex(string key, string path)
    {
        string asset = Path.Combine(AppContext.BaseDirectory, "Assets", key + ".png");
        if (File.Exists(asset)) { try { using var img = Image.FromFile(asset); _folderIcons.Images.Add(key, new Bitmap(img)); return _folderIcons.Images.Count - 1; } catch { } }
        try { var icon = ShellIcon.Get(path); _folderIcons.Images.Add(icon); icon.Dispose(); return _folderIcons.Images.Count - 1; }
        catch { _folderIcons.Images.Add(SystemIcons.WinLogo); return _folderIcons.Images.Count - 1; }
    }
    private void DrawSourceTile(object? sender, DrawListViewItemEventArgs e)
    {
        var item = e.Item; if (item is null) return;
        var bounds = e.Bounds; using var bg = new SolidBrush(item.Selected ? Color.FromArgb(58, 46, 78) : Surface); e.Graphics.FillRectangle(bg, bounds);
        int x=bounds.X+8, y=bounds.Y+(bounds.Height-16)/2; var box=new Rectangle(x,y,16,16);
        using var cb=new SolidBrush(IsItemChecked(item) ? Purple : Color.FromArgb(46,40,59)); using var pen=new Pen(IsItemChecked(item) ? PurpleHover : Border,1.2f); using var path=RoundedRect(box,4); e.Graphics.FillPath(cb,path); e.Graphics.DrawPath(pen,path);
        if(IsItemChecked(item)){using var check=new Pen(Color.White,2.2f); e.Graphics.DrawLines(check,new[]{new Point(x+3,y+8),new Point(x+6,y+11),new Point(x+13,y+4)});}
        if(item.ImageIndex>=0 && _folderIcons.Images.Count>item.ImageIndex){var img=_folderIcons.Images[item.ImageIndex]; if(img!=null)e.Graphics.DrawImage(img,x+23,bounds.Y+(bounds.Height-24)/2,24,24);}
        TextRenderer.DrawText(e.Graphics,item.Text,_sources.Font,new Rectangle(x+52,bounds.Y+2, bounds.Width-58,18),TextMain,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        string pathText=item.SubItems.Count>1?item.SubItems[1].Text:string.Empty; TextRenderer.DrawText(e.Graphics,pathText,new Font("Segoe UI",7.5F),new Rectangle(x+52,bounds.Y+19,bounds.Width-58,17),TextMuted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }

    private void SourceMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var hit=_sources.HitTest(e.Location);
        if(hit.Item!=null && e.X < hit.Item.Bounds.X+42)
        { ToggleItem(hit.Item); }
    }
    private void DrawDriveItem(object? sender, DrawItemEventArgs e)
    {
        if(e.Index<0)return;
        e.DrawBackground();
        string label = _drivePicker.Items[e.Index] is DriveChoice drive
            ? drive.Label
            : Convert.ToString(_drivePicker.Items[e.Index]) ?? "Unidad no disponible";

        var iconPath=Path.Combine(AppContext.BaseDirectory,"Assets","Unidad_USB.png");
        if(File.Exists(iconPath))
        {
            try { using var img=Image.FromFile(iconPath); e.Graphics.DrawImage(img,e.Bounds.X+4,e.Bounds.Y+2,20,20); }
            catch { }
        }
        Color fore=(e.State&DrawItemState.Selected)!=0?Color.White:TextMain;
        TextRenderer.DrawText(e.Graphics,label,_drivePicker.Font,new Rectangle(e.Bounds.X+28,e.Bounds.Y,e.Bounds.Width-30,e.Bounds.Height),fore,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }
    private static GraphicsPath RoundedRect(Rectangle r,int radius)
    {
        var p=new GraphicsPath(); int d=radius*2;
        p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90);
        p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
    }
    private void AddCustomFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Selecciona una carpeta específica, incluso en D:, E: u otra unidad." };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string path = dialog.SelectedPath;
        if (_sources.Items.Cast<ListViewItem>().Any(x => string.Equals(((FolderEntry)x.Tag!).Path, path, StringComparison.OrdinalIgnoreCase)))
        { OnkosMessageBox.Show(this, "Esta carpeta ya está en la lista.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        AddListItem(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), path, true, "Unidad");
    }
    private void AddOtherUserFolders()
    {
        string usersRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "..", "Users");
        usersRoot = Path.GetFullPath(usersRoot);
        if (!Directory.Exists(usersRoot))
        {
            OnkosMessageBox.Show(this, "No se encontró la carpeta de perfiles de Windows: " + usersRoot, "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var profiles = Directory.GetDirectories(usersRoot)
            .Where(path =>
            {
                string name = Path.GetFileName(path);
                return !new[] { "Public", "Default", "Default User", "All Users", "defaultuser0" }
                    .Contains(name, StringComparer.OrdinalIgnoreCase);
            })
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (profiles.Count == 0)
        {
            OnkosMessageBox.Show(this, "No se encontraron perfiles de usuario.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new Form
        {
            Text = "Seleccionar usuarios para el backup",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(560, 430),
            MinimumSize = new Size(520, 380),
            BackColor = Bg,
            ForeColor = TextMain,
            Font = Font
        };
        var info = new Label { Text = "Marca uno o varios perfiles. Se agregarán sus carpetas personales comunes; después podrás elegir cuáles respaldar.", Dock = DockStyle.Top, Height = 54, Padding = new Padding(12), ForeColor = TextMain };
        var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, BackColor = Surface, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10F) };
        foreach (string profile in profiles) list.Items.Add(new ProfileChoice(Path.GetFileName(profile), profile), false);

        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Surface };
        var selectAll = new Button { Text = "Seleccionar todos", Location = new Point(8, 8), Size = new Size(140, 30) };
        StyleButton(selectAll, true);
        selectAll.Click += (_, _) => { for (int i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, true); };
        var ok = new Button { Text = "Agregar seleccionados", Location = new Point(310, 8), Size = new Size(220, 30), DialogResult = DialogResult.OK };
        StyleButton(ok, false);
        buttons.Controls.Add(selectAll); buttons.Controls.Add(ok);
        dialog.Controls.Add(list); dialog.Controls.Add(buttons); dialog.Controls.Add(info);
        dialog.AcceptButton = ok;

        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        int added = 0;
        foreach (ProfileChoice profile in list.CheckedItems)
        {
            var folders = new[]
            {
                ("Escritorio", "Desktop"), ("Documentos", "Documents"), ("Descargas", "Downloads"),
                ("Imágenes", "Pictures"), ("Música", "Music"), ("Videos", "Videos"),
                ("Favoritos", "Favorites"), ("OneDrive", "OneDrive"), ("Contactos", "Contacts"),
                ("Juegos guardados", "Saved Games"), ("Búsquedas", "Searches"), ("Vínculos", "Links")
            };
            foreach (var folder in folders)
            {
                string path = Path.Combine(profile.Path, folder.Item2);
                if (!Directory.Exists(path)) continue;
                if (_sources.Items.Cast<ListViewItem>().Any(x => string.Equals(((FolderEntry)x.Tag!).Path, path, StringComparison.OrdinalIgnoreCase))) continue;
                string label = $"{profile.Name} · {folder.Item1}";
                AddListItem(label, path, true, folder.Item1);
                added++;
            }
        }
        if (added == 0)
            OnkosMessageBox.Show(this, "No se agregaron carpetas. Los perfiles seleccionados pueden no tener carpetas personales disponibles o ya estaban agregados.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            WriteLog($"Se agregaron {added} carpetas de otros perfiles. Revisa las casillas antes de iniciar el backup.");
    }

    private void RefreshDrives()
    {
        if (_refreshingDrives || IsDisposed) return;
        _refreshingDrives = true;
        try
        {
            string previousSelectedRoot = _selectedDriveRoot;
            string previousDestination = _destination.Text.Trim();
            string systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";
            string appRoot = Path.GetPathRoot(AppContext.BaseDirectory) ?? string.Empty;
            var usbLetters = GetUsbPhysicalDriveLetters();
            var drives = new List<DriveChoice>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady) continue;
                    string root = d.RootDirectory.FullName;
                    if (string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase)) continue;
                    bool isAppDrive = !string.IsNullOrWhiteSpace(appRoot) && string.Equals(root, appRoot, StringComparison.OrdinalIgnoreCase);
                    bool isRemovable = d.DriveType == DriveType.Removable;
                    bool isUsbFixed = d.DriveType == DriveType.Fixed && usbLetters.Contains(root.TrimEnd('\\', ':').ToUpperInvariant());
                    if (!isAppDrive && !isRemovable && !isUsbFixed) continue;
                    string label = isAppDrive ? $"UNIDAD DEL PROGRAMA {root}" : isRemovable ? $"USB {root}" : $"DISCO EXTERNO {root}";
                    drives.Add(new DriveChoice(label, root, d.DriveType));
                }
                catch { }
            }
            _drivePicker.BeginUpdate();
            try
            {
                _drivePicker.Items.Clear();
                foreach (var drive in drives.OrderBy(d => d.Root.Equals(appRoot, StringComparison.OrdinalIgnoreCase) ? 0 : d.Type == DriveType.Removable ? 1 : 2)) _drivePicker.Items.Add(drive);
                int previousIndex = -1;
                for (int i = 0; i < _drivePicker.Items.Count; i++)
                    if (_drivePicker.Items[i] is DriveChoice choice && string.Equals(choice.Root, previousSelectedRoot, StringComparison.OrdinalIgnoreCase)) { previousIndex = i; break; }
                if (previousIndex >= 0)
                {
                    _drivePicker.SelectedIndex = previousIndex;
                    _selectedDriveRoot = previousSelectedRoot;
                }
                else if (_drivePicker.Items.Count > 0)
                {
                    _drivePicker.SelectedIndex = 0;
                    _selectedDriveRoot = ((_drivePicker.Items[0] as DriveChoice)?.Root) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(previousSelectedRoot))
                    {
                        _destination.Clear();
                        OnkosMessageBox.ShowWarning(this, "Destino desconectado", $"La unidad {previousSelectedRoot} ya no está disponible. Selecciona otra unidad para continuar.");
                    }
                    else
                    {
                        _destination.Text = _selectedDriveRoot;
                    }
                }
                else
                {
                    _drivePicker.Items.Add("SIN UNIDADES EXTERNAS");
                    _drivePicker.SelectedIndex = 0;
                    _selectedDriveRoot = string.Empty;
                    if (!string.IsNullOrWhiteSpace(previousSelectedRoot))
                    {
                        _destination.Clear();
                        OnkosMessageBox.ShowWarning(this, "Destino desconectado", $"La unidad {previousSelectedRoot} ya no está disponible. Conecta otra memoria USB o disco duro externo.");
                    }
                    else if (string.IsNullOrWhiteSpace(previousDestination)) _destination.Text = "Conecta una memoria USB o disco duro externo";
                }
            }
            finally { _drivePicker.EndUpdate(); }
        }
        finally { _refreshingDrives = false; }
    }

    private static HashSet<string> GetUsbPhysicalDriveLetters()
    {
        var letters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var usbPartitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var disks = new ManagementObjectSearcher("SELECT DeviceID, InterfaceType, PNPDeviceID FROM Win32_DiskDrive"))
            using (var diskResults = disks.Get())
            {
                foreach (ManagementObject disk in diskResults)
                {
                    string interfaceType = Convert.ToString(disk["InterfaceType"]) ?? "";
                    string pnp = Convert.ToString(disk["PNPDeviceID"]) ?? "";
                    if (!interfaceType.Equals("USB", StringComparison.OrdinalIgnoreCase) &&
                        !pnp.Contains("USB", StringComparison.OrdinalIgnoreCase)) continue;
                    string deviceId = Convert.ToString(disk["DeviceID"]) ?? "";
                    string escaped = deviceId.Replace("\\\\", "\\\\\\\\").Replace("'", "\\\\'");
                    using var partitions = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{escaped}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                    using var partitionResults = partitions.Get();
                    foreach (ManagementObject partition in partitionResults)
                        usbPartitions.Add(Convert.ToString(partition["DeviceID"]) ?? "");
                }
            }

            using var logicalLinks = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition");
            using var links = logicalLinks.Get();
            foreach (ManagementObject link in links)
            {
                string antecedent = Convert.ToString(link["Antecedent"]) ?? "";
                // Comparar el identificador de partición extraído de la referencia WMI.
                var partitionMatch = System.Text.RegularExpressions.Regex.Match(
                    antecedent, "DeviceID=\\"([^\\"]+)\\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!partitionMatch.Success ||
                    !usbPartitions.Contains(partitionMatch.Groups[1].Value)) continue;

                string dependent = Convert.ToString(link["Dependent"]) ?? "";
                var driveMatch = System.Text.RegularExpressions.Regex.Match(
                    dependent, "DeviceID=\\"([A-Z]:)\\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (driveMatch.Success) letters.Add(driveMatch.Groups[1].Value.TrimEnd(':'));
            }
        }
        catch { /* Si WMI no está disponible, las memorias extraíbles siguen apareciendo por DriveType.Removable. */ }
        return letters;
    }
    private void ChooseDestination()
    {
        using var dialog = new FolderBrowserDialog { Description = "Selecciona el destino del backup (USB, disco externo o red)." };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            string selectedRoot = Path.GetPathRoot(dialog.SelectedPath) ?? "";
            string systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";
            if (string.Equals(selectedRoot, systemRoot, StringComparison.OrdinalIgnoreCase))
            {
                OnkosMessageBox.ShowWarning(this, "Destino no permitido", "No se permite guardar el respaldo en el disco donde está instalado Windows. Selecciona una memoria USB o un disco externo.");
                return;
            }
            _destination.Text = dialog.SelectedPath;
        }
    }

    private bool TryPromptBackupName(out string backupName)
    {
        backupName = string.Empty;
        using var dialog = new Form
        {
            Text = "Nombre del respaldo",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(430, 160),
            BackColor = Surface,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 9.5F)
        };

        var label = new Label
        {
            Text = "Escribe un nombre para identificar esta copia (opcional):",
            AutoSize = false,
            Location = new Point(18, 18),
            Size = new Size(394, 28),
            ForeColor = TextMain
        };
        var input = new TextBox
        {
            Location = new Point(18, 52),
            Size = new Size(394, 30),
            Text = _backupLabel.Text,
            BackColor = Surface2,
            ForeColor = TextMain,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10F)
        };
        var accept = new Button { Text = "CONTINUAR", DialogResult = DialogResult.OK, Size = new Size(120, 34), Location = new Point(164, 108) };
        StyleButton(accept, false);
        var cancel = new Button { Text = "CANCELAR", DialogResult = DialogResult.Cancel, Size = new Size(120, 34), Location = new Point(292, 108) };
        StyleButton(cancel, true);
        dialog.Controls.Add(label);
        dialog.Controls.Add(input);
        dialog.Controls.Add(accept);
        dialog.Controls.Add(cancel);
        dialog.AcceptButton = accept;
        dialog.CancelButton = cancel;
        input.SelectAll();

        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        backupName = input.Text.Trim();
        return true;
    }

    private async Task StartBackupAsync()
    {
        if (_busy) return;
        var selected = _sources.Items.Cast<ListViewItem>().Where(x => IsItemChecked(x)).Select(x => (FolderEntry)x.Tag!).ToList();

        selected = selected
            .Where(entry => !selected.Any(other =>
                !string.Equals(entry.Path, other.Path, StringComparison.OrdinalIgnoreCase) &&
                IsPathInside(entry.Path, other.Path)))
            .ToList();

        bool chromeSelected = selected.Any(entry =>
            entry.Label.StartsWith("Google Chrome", StringComparison.OrdinalIgnoreCase));
        if (chromeSelected && Process.GetProcessesByName("chrome").Length > 0)
        {
            OnkosMessageBox.Show(this,
                "Google Chrome está abierto. Cierra todas las ventanas de Chrome antes de respaldar sus perfiles para evitar archivos bloqueados o datos incompletos.",
                "Cierra Google Chrome", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (selected.Count == 0 && !_ipv4.Checked) { OnkosMessageBox.Show(this, "Selecciona carpetas o activa la opción de guardar configuración de red.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        string dest = _destination.Text.Trim();
        string systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";
        string destinationRoot = Path.GetPathRoot(dest) ?? "";
        if (string.Equals(destinationRoot, systemRoot, StringComparison.OrdinalIgnoreCase))
        {
            OnkosMessageBox.ShowWarning(this, "Destino no permitido", "El disco de Windows no puede utilizarse como destino del respaldo. Conecta una memoria USB o un disco duro externo.");
            return;
        }
        if (!Directory.Exists(dest))
        {
            OnkosMessageBox.ShowWarning(this, "Destino no disponible", "El destino seleccionado no está disponible. Conecta la unidad externa o selecciona otra carpeta.");
            return;
        }
        if (string.IsNullOrWhiteSpace(dest) || !Directory.Exists(dest)) { OnkosMessageBox.Show(this, "Selecciona primero el destino del respaldo.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        foreach (var f in selected)
            if (IsSameOrChild(dest, f.Path)) { OnkosMessageBox.Show(this, $"El destino está dentro de la carpeta origen ({f.Label}). Elige otra ubicación.", "Destino no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        if (!TryPromptBackupName(out string enteredBackupName)) return;
        _backupLabel.Text = enteredBackupName;
        SaveSession(selected, dest);

        string customPrefix = SafeName(enteredBackupName.Trim());
        string folderPrefix = string.IsNullOrWhiteSpace(customPrefix) ? "" : customPrefix + "_";
        string backupPattern = $"{folderPrefix}{Environment.MachineName}_*";
        string? previousBackup = null;

        // Busca respaldos anteriores del mismo equipo para permitir reanudar una copia interrumpida.
        try
        {
            previousBackup = Directory.GetDirectories(dest, backupPattern, SearchOption.TopDirectoryOnly)
                .Where(path => File.Exists(Path.Combine(path, ".onkos-state.json"))
                    && Directory.Exists(Path.Combine(path, "Archivos")))
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            WriteLog("No se pudieron revisar respaldos anteriores: " + ex.Message);
        }

        string root;
        if (!string.IsNullOrWhiteSpace(previousBackup))
        {
            var choice = OnkosMessageBox.AskYesNoCancel(this,
                "ONKOS — Reanudar respaldo",
                $"Se encontró un respaldo anterior de este equipo:\n\n{previousBackup}\n\n¿Deseas continuar ese respaldo?\n\nSí = reanudar el respaldo anterior\nNo = crear un respaldo nuevo\nCancelar = no iniciar",
                "Sí, reanudar", "No, crear nuevo", "Cancelar");
            if (choice == DialogResult.Cancel) return;
            root = choice == DialogResult.Yes
                ? previousBackup!
                : Path.Combine(dest, $"{folderPrefix}{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}");
        }
        else
        {
            root = Path.Combine(dest, $"{folderPrefix}{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}");
        }

        _busy = true; _run.Enabled = false; _log.Clear(); SetProgress(0, 0, "Preparando respaldo…", "Revisando lo seleccionado");
        _cts = new CancellationTokenSource();
        _cancel.Visible = true; LayoutActions();
        ActivityLog.Write("BACKUP", "Inicio backup", "OK", $"Destino: {dest} | Carpetas: {selected.Count}");
        try
        {
            Directory.CreateDirectory(root);
            var fileList = new List<(string Source, string Relative, string FolderName)>();
            var directoryList = new List<(string Source, string Relative, string FolderName)>();
            var errorDetails = new List<string>();
            WriteLog($"Carpetas seleccionadas: {selected.Count}");
            foreach (var entry in selected)
            {
                WriteLog($"Revisando: {entry.Label} | {entry.Path}");
                if (!Directory.Exists(entry.Path))
                {
                    string detail = $"ORIGEN: {entry.Path} | DESTINO: No aplica | MOTIVO: La carpeta no existe o no está disponible.";
                    errorDetails.Add(detail);
                    WriteLog("ERROR: " + detail);
                    continue;
                }
                CollectFilesSafe(entry.Path, entry.Path, SafeName(entry.Label), fileList, directoryList, errorDetails);
            }
            WriteLog($"Archivos encontrados para copiar: {fileList.Count}");

            long totalBytes = 0;
            try { totalBytes = fileList.Sum(f => new FileInfo(f.Source).Length); } catch { }
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(root)!);
                if (drive.IsReady && totalBytes > 0 && drive.AvailableFreeSpace < totalBytes * 1.1)
                {
                    var answer = OnkosMessageBox.Show(this,
                        $"Espacio insuficiente en destino.\n\nRequerido aprox.: {totalBytes / 1024.0 / 1024 / 1024:F1} GB\nDisponible: {drive.AvailableFreeSpace / 1024.0 / 1024 / 1024:F1} GB\n\n¿Continuar de todas formas?",
                        "Espacio en disco", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (answer != DialogResult.Yes)
                    {
                        WriteLog("Backup cancelado por falta de espacio.");
                        SetProgress(0, 0, "Backup cancelado por falta de espacio.", "Cancelado");
                        ActivityLog.Write("BACKUP", "Cancelado", "Espacio insuficiente");
                        return;
                    }
                }
            }
            catch { }

            string filesRoot = Path.Combine(root, "Archivos");
            Directory.CreateDirectory(filesRoot);

            // Adjuntar el último inventario al backup (si existe).
            AttachLatestInventory(filesRoot, errorDetails);

            var state = BackupState.Load(root);
            int alreadyDone = 0;

            int errors = errorDetails.Count;

            foreach (var dir in directoryList)
            {
                string targetDir = Path.Combine(filesRoot, dir.FolderName, dir.Relative);
                try { Directory.CreateDirectory(targetDir); }
                catch (Exception ex) { errors++; ReportError(errorDetails, dir.Source, targetDir, "No se pudo crear la carpeta en el respaldo: " + ex.Message); }
            }

            int total = Math.Max(fileList.Count + (_ipv4.Checked ? 1 : 0), 1);
            int done = 0, copied = 0, verified = 0;
            int networkAdaptersBackedUp = 0;
            bool networkBackupSucceeded = false;
            var manifestRows = new List<string> { CsvRow("Estado", "Verificación", "Origen", "Destino", "Bytes", "Fecha", "Detalle") };
            IProgress<(int done, int total, string message, string current)> progress = new Progress<(int done, int total, string message, string current)>(p => { SetProgress(p.done, p.total, $"Copiando… {p.done} de {p.total}", p.current); if (!string.IsNullOrWhiteSpace(p.message)) WriteLog(p.message); });
            var token = _cts.Token;
            await Task.Run(() =>
            {
                foreach (var item in fileList)
                {
                    if (token.IsCancellationRequested) break;
                    string targetDir = Path.Combine(root, "Archivos", item.FolderName, Path.GetDirectoryName(item.Relative) ?? "");
                    string targetFile = Path.Combine(targetDir, Path.GetFileName(item.Source));
                    long sourceBytes = 0;
                    string copyStatus = "ERROR", verifyStatus = "No verificado", detail = "";

                    try
                    {
                        var sInfo = new FileInfo(item.Source);
                        sourceBytes = sInfo.Length;
                        if (state.Files.TryGetValue(item.Source, out var prev)
                            && prev.Size == sourceBytes
                            && !string.IsNullOrWhiteSpace(prev.Hash)
                            && File.Exists(targetFile))
                        {
                            string sourceHash = BackupState.ComputeHash(item.Source);
                            string targetHash = BackupState.ComputeHash(targetFile);
                            if (!string.IsNullOrWhiteSpace(sourceHash)
                                && !string.IsNullOrWhiteSpace(targetHash)
                                && string.Equals(sourceHash, prev.Hash, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(targetHash, prev.Hash, StringComparison.OrdinalIgnoreCase))
                            {
                                copyStatus = "YA COPIADO";
                                verifyStatus = "Reanudado (SHA-256 correcto)";
                                alreadyDone++;
                                manifestRows.Add(CsvRow(copyStatus, verifyStatus, item.Source, targetFile, sourceBytes.ToString(), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "Omitido: origen y destino coinciden con el hash registrado"));
                                done++; progress.Report((done, total, "", Path.GetFileName(item.Source)));
                                continue;
                            }
                            WriteLog($"Revalidación: se volverá a copiar porque cambió el origen o el respaldo no coincide: {item.Source}");
                        }
                    }
                    catch { }

                    try
                    {
                        var sourceInfo = new FileInfo(item.Source);
                        sourceBytes = sourceInfo.Length;
                        string sourceHashAfterCopyCheck = BackupState.ComputeHash(item.Source);
                        if (string.IsNullOrWhiteSpace(sourceHashAfterCopyCheck))
                            throw new IOException("No se pudo calcular SHA-256 del archivo de origen.");
                        Directory.CreateDirectory(targetDir);
                        File.Copy(item.Source, targetFile, true);
                        copied++;
                        var targetInfo = new FileInfo(targetFile);
                        if (!targetInfo.Exists) throw new IOException("La copia no aparece en el destino.");
                        if (targetInfo.Length != sourceInfo.Length)
                            throw new IOException($"Tamaño diferente: origen {sourceInfo.Length} bytes, copia {targetInfo.Length} bytes.");
                        string hash = BackupState.ComputeHash(targetFile);
                        if (string.IsNullOrWhiteSpace(hash))
                            throw new IOException("No se pudo calcular SHA-256 del archivo copiado.");
                        if (!string.Equals(sourceHashAfterCopyCheck, hash, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("La verificación SHA-256 falló: el origen y la copia no coinciden.");
                        state.Files[item.Source] = new FileState { Size = targetInfo.Length, Hash = hash, CopiedAt = DateTime.Now };
                        copyStatus = "COPIADO";
                        verifyStatus = "Verificado (tamaño + SHA-256)";
                        verified++;
                    }
                    catch (Exception ex)
                    {
                        errors++; detail = ex.Message; copyStatus = "ERROR"; verifyStatus = "Falló";
                        ReportError(errorDetails, item.Source, targetFile, ex.Message);
                    }
                    manifestRows.Add(CsvRow(copyStatus, verifyStatus, item.Source, targetFile, sourceBytes.ToString(), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), detail));
                    done++; progress.Report((done, total, "", Path.GetFileName(item.Source)));

                    if (done % 50 == 0)
                    {
                        try { state.Save(); } catch { }
                    }
                }
                try { state.Save(); } catch { }
            });

            bool canceled = token.IsCancellationRequested;
            if (!canceled && _ipv4.Checked)
            {
                string networkFile = Path.Combine(root, "Red", "IPv4_netsh_dump.txt");
                try
                {
                    progress.Report((done, total, "Exportando configuración de red…", "Configuración IPv4"));
                    Directory.CreateDirectory(Path.GetDirectoryName(networkFile)!);
                    string netshOutput = await RunNetshWithTimeoutAsync("interface dump", 30000);
                    File.WriteAllText(networkFile, netshOutput, Encoding.UTF8);

                    string structuredFile = Path.Combine(root, "Red", "IPv4_config.json");
                    string captureScript = @"
$ErrorActionPreference = 'Stop'
$result = @()
$configs = Get-NetIPConfiguration -ErrorAction Stop
foreach ($cfg in $configs) {
    $ipif = Get-NetIPInterface -InterfaceIndex $cfg.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue
    if (-not $ipif) { continue }
    $addresses = @($cfg.IPv4Address | Where-Object { $_.IPAddress -and $_.IPAddress -notlike '127.*' } | ForEach-Object {
        [pscustomobject]@{ Address = $_.IPAddress; PrefixLength = [int]$_.PrefixLength }
    })
    $gateways = @($cfg.IPv4DefaultGateway | ForEach-Object { $_.NextHop } | Where-Object { $_ })
    $dnsEntry = Get-DnsClientServerAddress -InterfaceIndex $cfg.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue
    $dns = @($dnsEntry.ServerAddresses | Where-Object { $_ })
    if ($addresses.Count -gt 0 -or $gateways.Count -gt 0 -or $dns.Count -gt 0) {
        $result += [pscustomobject]@{
            InterfaceAlias = [string]$cfg.InterfaceAlias
            InterfaceIndex = [int]$cfg.InterfaceIndex
            Dhcp = [string]$ipif.Dhcp
            Addresses = $addresses
            Gateways = $gateways
            DnsServers = $dns
        }
    }
}
ConvertTo-Json -InputObject @($result) -Depth 6 | Set-Content -LiteralPath $env:ONKOS_IPV4_OUTPUT -Encoding UTF8
";
                    string captureScriptPath = Path.Combine(Path.GetTempPath(), "ONKOS_Capturar_IPV4.ps1");
                    Directory.CreateDirectory(Path.GetDirectoryName(structuredFile)!);
                    File.WriteAllText(captureScriptPath, captureScript, new UTF8Encoding(true));
                    try
                    {
                        var captureStart = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{captureScriptPath}\"")
                        {
                            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
                        };
                        captureStart.Environment["ONKOS_IPV4_OUTPUT"] = structuredFile;
                        using var captureProcess = Process.Start(captureStart) ?? throw new InvalidOperationException("No se pudo iniciar PowerShell para capturar IPv4.");
                        Task<string> captureOut = captureProcess.StandardOutput.ReadToEndAsync();
                        Task<string> captureErr = captureProcess.StandardError.ReadToEndAsync();
                        if (!captureProcess.WaitForExit(30000))
                        {
                            try { captureProcess.Kill(true); } catch { }
                            throw new TimeoutException("La captura estructurada de IPv4 superó 30 segundos.");
                        }
                        string captureError = await captureErr;
                        _ = await captureOut;
                        if (captureProcess.ExitCode != 0 || !File.Exists(structuredFile))
                            throw new IOException("No se pudo generar IPv4_config.json. " + captureError);
                        string jsonCheck = File.ReadAllText(structuredFile, Encoding.UTF8).TrimStart('\uFEFF', ' ', '\r', '\n');
                        using var jsonDocument = System.Text.Json.JsonDocument.Parse(jsonCheck);
                        int adapterCount = jsonDocument.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                            ? jsonDocument.RootElement.GetArrayLength() : 0;
                        if (adapterCount == 0) throw new IOException("No se detectaron adaptadores con configuración IPv4 para respaldar.");
                        networkAdaptersBackedUp = adapterCount;
                        networkBackupSucceeded = true;
                        File.WriteAllText(Path.Combine(root, "Red", "LEEME.txt"),
                            "Respaldo de red: IPv4_netsh_dump.txt (referencia) e IPv4_config.json (IP, prefijo/máscara, puerta de enlace, DHCP y DNS por adaptador). " +
                            "La restauración estructurada solo aplicará configuraciones a adaptadores cuyo nombre coincida exactamente; revise el mapeo antes de confirmar." +
                            Environment.NewLine + $"Adaptadores capturados: {adapterCount}", Encoding.UTF8);
                        WriteLog($"Configuración IPv4 estructurada exportada. Adaptadores detectados: {adapterCount}.");
                    }
                    finally
                    {
                        try { if (File.Exists(captureScriptPath)) File.Delete(captureScriptPath); } catch { }
                    }
                }
                catch (Exception ex) { errors++; ReportError(errorDetails, "Configuración IPv4", networkFile, ex.Message); }
                finally { done++; progress.Report((done, total, "Configuración de red procesada.", "Configuración IPv4")); }
            }

            File.WriteAllText(Path.Combine(root, "MANIFIESTO_BACKUP.csv"), string.Join(Environment.NewLine, manifestRows), Encoding.UTF8);

            string errorReportPath = Path.Combine(root, "INFORME_ERRORES_BACKUP.txt");
            File.WriteAllText(errorReportPath,
                "INFORME DE ERRORES - ONKOS IT TOOLKIT BACKUP" + Environment.NewLine +
                $"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                $"Equipo: {Environment.MachineName}{Environment.NewLine}" +
                $"Usuario: {Environment.UserName}{Environment.NewLine}" +
                $"Errores detectados: {errorDetails.Count}{Environment.NewLine}{Environment.NewLine}" +
                (errorDetails.Count == 0 ? "Backup sin errores detectados." :
                    "Revisar o copiar manualmente los siguientes elementos:" + Environment.NewLine +
                    "ORIGEN | DESTINO | MOTIVO" + Environment.NewLine +
                    new string('-', 90) + Environment.NewLine +
                    string.Join(Environment.NewLine + Environment.NewLine, errorDetails)));

            File.WriteAllText(Path.Combine(root, "BACKUP_INFO.txt"),
                $"ONKOS IT Toolkit backup{Environment.NewLine}" +
                $"Equipo: {Environment.MachineName}{Environment.NewLine}" +
                $"Usuario: {Environment.UserName}{Environment.NewLine}" +
                $"Sistema: {Environment.OSVersion}{Environment.NewLine}" +
                $"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                $"Archivos copiados: {copied}{Environment.NewLine}" +
                $"Archivos verificados: {verified}{Environment.NewLine}" +
                $"Archivos ya existentes: {alreadyDone}{Environment.NewLine}" +
                $"Errores: {errors}{Environment.NewLine}" +
                $"Cancelado: {(canceled ? "Sí" : "No")}{Environment.NewLine}" +
                $"Manifiesto: MANIFIESTO_BACKUP.csv{Environment.NewLine}" +
                $"Informe detallado: INFORME_ERRORES_BACKUP.txt{Environment.NewLine}");

            if (canceled)
            {
                SetProgress(done, total, "Backup cancelado por el usuario.", "Cancelado");
                WriteLog($"Backup cancelado. Copiados: {copied}. Reanudados: {alreadyDone}. Errores: {errors}.{Environment.NewLine}Ruta: {root}");
                OnkosMessageBox.Show(this,
                    $"Backup cancelado por el usuario.{Environment.NewLine}{Environment.NewLine}Copiados ahora: {copied}{Environment.NewLine}Reanudados (ya existían): {alreadyDone}{Environment.NewLine}Verificados: {verified}{Environment.NewLine}Pendientes: {errors}{Environment.NewLine}{Environment.NewLine}{root}{Environment.NewLine}{Environment.NewLine}Puedes reanudar más tarde abriendo esta misma carpeta como destino.",
                    "ONKOS Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ActivityLog.Write("BACKUP", "Cancelado por usuario", "OK", $"Copiados: {copied} | Reanudados: {alreadyDone} | Errores: {errors}");
            }
            else
            {
                SetProgress(total, total, errors == 0 ? "¡Respaldo completado!" : "Respaldo completado con algunos problemas.", "Terminado");
                WriteLog($"Finalizado. Copiados: {copied}. Reanudados: {alreadyDone}. Errores: {errors}.{Environment.NewLine}Ruta: {root}");
                ActivityLog.Write("BACKUP", "Backup finalizado", errors == 0 ? "OK" : "CON ERRORES", $"Copiados: {copied} | Reanudados: {alreadyDone} | Errores: {errors} | Ruta: {root}");

                if (errors > 0)
                    ShowErrorsModal(errorDetails, root);
                else
                    OnkosMessageBox.Show(this,
                        $"Respaldo completado correctamente.{Environment.NewLine}{Environment.NewLine}" +
                    $"Archivos copiados: {copied}{Environment.NewLine}" +
                    $"Archivos reanudados (ya existían): {alreadyDone}{Environment.NewLine}" +
                    $"Archivos verificados: {verified}{Environment.NewLine}" +
                    (networkBackupSucceeded
                        ? $"Configuración de red respaldada: Sí ({networkAdaptersBackedUp} adaptador(es) IPv4){Environment.NewLine}"
                        : (_ipv4.Checked ? $"Configuración de red respaldada: No{Environment.NewLine}" : "")) +
                    (fileList.Count == 0 && networkBackupSucceeded
                        ? $"Tipo de respaldo: Solo configuración de red (no se seleccionaron carpetas).{Environment.NewLine}"
                        : "") +
                    $"{Environment.NewLine}{root}",
                        "ONKOS Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex) { _status.Text = "El backup falló."; WriteLog("ERROR GENERAL: " + ex.Message); ActivityLog.Write("BACKUP", "Error general", "ERROR", ex.Message); OnkosMessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            _busy = false; _run.Enabled = true;
            _cancel.Visible = false; LayoutActions();
            _cts?.Dispose(); _cts = null;
        }
    }

    // Copia el último inventario que haya en Data/Reports/Inventario_*.csv
    // dentro de la carpeta Archivos/Inventario_ONKOS/ del backup.
    private void AttachLatestInventory(string filesRoot, List<string> errorDetails)
    {
        try
        {
            string reportsDir = AppPaths.ReportsDir;
            if (!Directory.Exists(reportsDir)) return;
            var latest = Directory.GetFiles(reportsDir, "Inventario_*.csv")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (latest == null) return;

            string targetDir = Path.Combine(filesRoot, "Inventario_ONKOS");
            Directory.CreateDirectory(targetDir);
            string targetFile = Path.Combine(targetDir, Path.GetFileName(latest));
            File.Copy(latest, targetFile, true);
            WriteLog($"Inventario adjuntado al backup: {Path.GetFileName(latest)}");
        }
        catch (Exception ex)
        {
            errorDetails.Add($"ORIGEN: {AppPaths.ReportsDir} | DESTINO: Archivos/Inventario_ONKOS/ | MOTIVO: No se pudo adjuntar el inventario: {ex.Message}");
        }
    }

    private void ShowErrorsModal(List<string> errorDetails, string root)
    {
        using var dialog = new Form
        {
            Text = "⚠  Backup completado con errores",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(760, 520),
            MinimumSize = new Size(600, 400),
            BackColor = Bg,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 9.5F)
        };
        var title = new Label
        {
            Text = $"Se copiaron los archivos, pero {errorDetails.Count} elemento(s) NO se pudieron copiar.{Environment.NewLine}" +
                   "Revisa la lista antes de dar por bueno el respaldo. Los elementos listados no están en el destino.",
            Dock = DockStyle.Top, Height = 60, Padding = new Padding(12, 8, 12, 8),
            ForeColor = Color.FromArgb(255, 200, 100), Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        var list = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9F),
            BackColor = Color.FromArgb(13, 12, 18),
            ForeColor = Color.FromArgb(240, 200, 200),
            BorderStyle = BorderStyle.None,
            Text = string.Join(Environment.NewLine + Environment.NewLine, errorDetails)
        };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Surface };
        var understood = new CheckBox
        {
            Text = "Entiendo que estos elementos NO están en el backup.",
            Location = new Point(12, 16), AutoSize = true, ForeColor = TextMain, Cursor = Cursors.Hand
        };
        var ok = new Button
        {
            Text = "CERRAR", DialogResult = DialogResult.OK, Location = new Point(640, 12), Size = new Size(100, 32),
            BackColor = Purple, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false
        };
        ok.FlatAppearance.BorderSize = 0;
        understood.CheckedChanged += (_, _) => ok.Enabled = understood.Checked;
        bottom.Controls.Add(understood); bottom.Controls.Add(ok);
        dialog.Controls.Add(list); dialog.Controls.Add(bottom); dialog.Controls.Add(title);
        dialog.AcceptButton = ok;
        dialog.ShowDialog(this);
        WriteLog($"Errores no bloqueantes: {errorDetails.Count}. Informe: {Path.Combine(root, "INFORME_ERRORES_BACKUP.txt")}");
    }

    private async Task RestoreBackupAsync()
    {
        if (_busy) return;
        using var picker = new FolderBrowserDialog { Description = "Selecciona la carpeta del respaldo (debe contener MANIFIESTO_BACKUP.csv)." };
        if (picker.ShowDialog(this) != DialogResult.OK) return;

        string backupRoot = picker.SelectedPath;
        string manifestPath = Path.Combine(backupRoot, "MANIFIESTO_BACKUP.csv");
        if (!File.Exists(manifestPath))
        {
            OnkosMessageBox.Show(this, "No se encontró MANIFIESTO_BACKUP.csv. Selecciona la carpeta principal del backup generado por ONKOS IT Toolkit.", "Respaldo no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        List<string[]> rows;
        try { rows = ParseCsv(File.ReadAllText(manifestPath, Encoding.UTF8)); }
        catch (Exception ex) { OnkosMessageBox.Show(this, "No se pudo leer el manifiesto: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        var files = rows.Skip(1)
            .Where(r => r.Length >= 4 && (string.Equals(r[0], "COPIADO", StringComparison.OrdinalIgnoreCase) || string.Equals(r[0], "YA COPIADO", StringComparison.OrdinalIgnoreCase)))
            .Select(r =>
            {
                string manifestDestination = r[3];
                string marker = Path.DirectorySeparatorChar + "Archivos" + Path.DirectorySeparatorChar;
                int idx = manifestDestination.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                string backupFile = idx >= 0
                    ? Path.Combine(backupRoot, "Archivos", manifestDestination.Substring(idx + marker.Length))
                    : manifestDestination;
                long expectedBytes = 0;
                long.TryParse(r.Length > 4 ? r[4] : null, out expectedBytes);
                return (BackupFile: backupFile, OriginalFile: r[2], ExpectedBytes: expectedBytes);
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.BackupFile) && !string.IsNullOrWhiteSpace(x.OriginalFile))
            .ToList();

        string structuredNetworkBackup = Path.Combine(backupRoot, "Red", "IPv4_config.json");
        string legacyNetworkBackup = Path.Combine(backupRoot, "Red", "IPv4_netsh_dump.txt");
        bool hasNetworkBackup = File.Exists(structuredNetworkBackup) || File.Exists(legacyNetworkBackup);

        if (files.Count == 0 && !hasNetworkBackup)
        {
            OnkosMessageBox.Show(this, "El manifiesto no contiene archivos respaldados y tampoco se encontró un respaldo de red válido.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var backupState = BackupState.Load(backupRoot);

        var oldProfiles = files.Select(x => GetWindowsProfileRoot(x.OriginalFile))
            .OfType<string>()
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var profileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string currentProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (oldProfiles.Count > 0)
        {
            using var mapping = new Form
            {
                Text = "Destino de restauración — perfiles de usuario",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                ClientSize = new Size(700, Math.Min(180 + oldProfiles.Count * 76, 520)),
                BackColor = Bg, ForeColor = TextMain, Font = new Font("Segoe UI", 9.5F)
            };
            var intro = new Label { Text = "El backup contiene carpetas de otro usuario. Selecciona dónde restaurar cada perfil.", Location = new Point(14, 12), Size = new Size(670, 42), ForeColor = TextMain };
            mapping.Controls.Add(intro);
            var rowsPanel = new Panel { Location = new Point(12, 58), Size = new Size(676, oldProfiles.Count * 76), AutoScroll = true, BackColor = Bg };
            mapping.Controls.Add(rowsPanel);
            int y = 0;
            foreach (string oldProfile in oldProfiles)
            {
                var label = new Label { Text = "Perfil original: " + oldProfile, Location = new Point(4, y), Size = new Size(650, 22), ForeColor = TextMuted };
                var target = new TextBox { Text = currentProfile, Location = new Point(4, y + 25), Size = new Size(545, 28), BackColor = Surface2, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle };
                var browse = new Button { Text = "Examinar…", Location = new Point(555, y + 23), Size = new Size(105, 30), BackColor = Surface2, ForeColor = TextMain, FlatStyle = FlatStyle.Flat };
                browse.Click += (_, _) =>
                {
                    using var folder = new FolderBrowserDialog { Description = "Selecciona la carpeta del perfil de usuario destino.", SelectedPath = Directory.Exists(target.Text) ? target.Text : currentProfile };
                    if (folder.ShowDialog(mapping) == DialogResult.OK) target.Text = folder.SelectedPath;
                };
                rowsPanel.Controls.Add(label); rowsPanel.Controls.Add(target); rowsPanel.Controls.Add(browse);
                profileMap[oldProfile] = target.Text;
                target.TextChanged += (_, _) => profileMap[oldProfile] = target.Text.Trim();
                y += 76;
            }
            var note = new Label { Text = "Se conservará la estructura interna. Las rutas fuera de C:\\Users\\… no se modificarán.", Location = new Point(14, rowsPanel.Bottom + 4), Size = new Size(670, 34), ForeColor = TextMuted };
            mapping.Controls.Add(note);
            var ok = new Button { Text = "CONTINUAR", DialogResult = DialogResult.OK, Location = new Point(460, mapping.ClientSize.Height - 42), Size = new Size(105, 32), BackColor = Purple, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var cancel = new Button { Text = "CANCELAR", DialogResult = DialogResult.Cancel, Location = new Point(575, mapping.ClientSize.Height - 42), Size = new Size(105, 32), BackColor = Surface2, ForeColor = TextMain, FlatStyle = FlatStyle.Flat };
            mapping.Controls.Add(ok); mapping.Controls.Add(cancel); mapping.AcceptButton = ok; mapping.CancelButton = cancel;
            if (mapping.ShowDialog(this) != DialogResult.OK) return;
            if (profileMap.Any(kv => string.IsNullOrWhiteSpace(kv.Value) || !Directory.Exists(kv.Value)))
            {
                OnkosMessageBox.Show(this, "Selecciona una carpeta de usuario destino válida para cada perfil.", "Destino inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        bool overwrite = false;
        if (files.Count > 0)
        {
            var overwriteChoice = OnkosMessageBox.Show(this,
                $"Se encontraron {files.Count:N0} archivos respaldados.\n\n¿Deseas reemplazar archivos que ya existan en el destino seleccionado?\n\nSí = reemplazar\nNo = conservar los existentes y omitirlos",
                "Confirmar restauración", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (overwriteChoice == DialogResult.Cancel) return;
            overwrite = overwriteChoice == DialogResult.Yes;
        }
        else
        {
            WriteLog("Respaldo de red detectado sin archivos de usuario. Se continuará directamente con la revisión IPv4.");
        }

        _busy = true; _run.Enabled = false; _log.Clear();
        SetProgress(0, files.Count, "Preparando restauración…", "Leyendo manifiesto");
        ActivityLog.Write("RESTORE", "Inicio restauración", "OK", $"Backup: {backupRoot} | Archivos: {files.Count}");
        int restored = 0, skipped = 0, errors = 0;
        var report = new List<string> { "INFORME DE RESTAURACIÓN ONKOS", $"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", $"Backup: {backupRoot}", "MAPEO DE PERFILES:" };
        foreach (var map in profileMap) report.Add($"  {map.Key} => {map.Value}");
        report.Add("");
        try
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var item = files[i];
                    string source = item.BackupFile;
                    string selectedFolder = GetBackupFolderLabel(item.BackupFile, backupRoot);
                    string target = RemapProfilePath(item.OriginalFile, profileMap, selectedFolder);
                    try
                    {
                        if (!File.Exists(source)) throw new FileNotFoundException("No se encuentra el archivo dentro del respaldo.", source);
                        var backupInfo = new FileInfo(source);
                        if (item.ExpectedBytes > 0 && backupInfo.Length != item.ExpectedBytes)
                            throw new IOException($"El archivo del respaldo tiene tamaño incorrecto. Esperado: {item.ExpectedBytes}; encontrado: {backupInfo.Length}.");
                        if (backupState.Files.TryGetValue(item.OriginalFile, out var savedState)
                            && !string.IsNullOrWhiteSpace(savedState.Hash))
                        {
                            string actualHash = BackupState.ComputeHash(source);
                            if (string.IsNullOrWhiteSpace(actualHash)
                                || !string.Equals(actualHash, savedState.Hash, StringComparison.OrdinalIgnoreCase))
                                throw new IOException("El archivo del respaldo no superó la verificación SHA-256. No se restauró para evitar copiar información dañada.");
                        }
                        if (File.Exists(target) && !overwrite)
                        {
                            skipped++;
                            report.Add($"OMITIDO (ya existe): {target}");
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                            File.Copy(source, target, overwrite);
                            restored++;
                            report.Add($"RESTAURADO Y VALIDADO: {target}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        report.Add($"ERROR: {target} | {ex.Message}");
                    }
                    int current = i + 1;
                    if (current % 10 == 0 || current == files.Count)
                    {
                        int percentage = (int)(current * 100.0 / files.Count);
                        if (IsHandleCreated) BeginInvoke(new Action(() => SetProgress(current, files.Count, $"Restaurando… {current:N0} de {files.Count:N0} ({percentage}%)", Path.GetFileName(target))));
                    }
                }
            });

            string structuredNetworkFile = Path.Combine(backupRoot, "Red", "IPv4_config.json");
            string legacyNetworkDump = Path.Combine(backupRoot, "Red", "IPv4_netsh_dump.txt");
            if (File.Exists(structuredNetworkFile))
            {
                var networkChoice = OnkosMessageBox.Show(this,
                    "El respaldo contiene configuración IPv4 estructurada por adaptador, incluyendo IP, máscara/prefijo, puerta de enlace, DHCP y DNS cuando estaban configurados.\n\n" +
                    "ONKOS identificará los adaptadores por nombre exacto. Si un nombre no coincide, ese adaptador se omitirá; no se asignará la configuración a otro adaptador automáticamente.\n\n" +
                    "¿Deseas revisar la configuración antes de restaurarla?",
                    "Revisión de configuración IPv4", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (networkChoice == DialogResult.Yes)
                {
                    try
                    {
                        string currentNetworkDump = await RunNetshWithTimeoutAsync("interface dump", 30000);
                        string currentNetworkFile = Path.Combine(backupRoot, "Red", "IPv4_antes_de_restaurar.txt");
                        File.WriteAllText(currentNetworkFile, currentNetworkDump, Encoding.UTF8);
                        report.Add("Configuración actual guardada en: " + currentNetworkFile);

                        string json = File.ReadAllText(structuredNetworkFile, Encoding.UTF8).TrimStart('\uFEFF');
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        var preview = new StringBuilder("Adaptadores incluidos en el respaldo:\r\n\r\n");
                        foreach (var adapter in doc.RootElement.EnumerateArray())
                        {
                            string alias = adapter.TryGetProperty("InterfaceAlias", out var a) ? a.GetString() ?? "(sin nombre)" : "(sin nombre)";
                            preview.AppendLine("• " + alias);
                            if (adapter.TryGetProperty("Addresses", out var addresses) && addresses.ValueKind == System.Text.Json.JsonValueKind.Array)
                                foreach (var address in addresses.EnumerateArray())
                                    preview.AppendLine("   IP: " + address.GetProperty("Address").GetString() + "/" + address.GetProperty("PrefixLength").GetInt32());
                            if (adapter.TryGetProperty("Gateways", out var gateways) && gateways.ValueKind == System.Text.Json.JsonValueKind.Array)
                                preview.AppendLine("   Puerta de enlace: " + string.Join(", ", gateways.EnumerateArray().Select(x => x.GetString())));
                            if (adapter.TryGetProperty("DnsServers", out var dns) && dns.ValueKind == System.Text.Json.JsonValueKind.Array)
                                preview.AppendLine("   DNS: " + string.Join(", ", dns.EnumerateArray().Select(x => x.GetString())));
                            preview.AppendLine();
                        }
                        var finalNetworkChoice = OnkosMessageBox.Show(this,
                            preview + "\r\nSe aplicará únicamente a interfaces con el mismo nombre. La operación puede interrumpir la conexión. ¿CONFIRMAS?",
                            "Confirmación final — restaurar IPv4", MessageBoxButtons.YesNo, MessageBoxIcon.Stop);
                        if (finalNetworkChoice == DialogResult.Yes)
                        {
                            string restoreScript = @"
$ErrorActionPreference = 'Stop'
$items = @(Get-Content -LiteralPath $env:ONKOS_IPV4_INPUT -Raw | ConvertFrom-Json)
$log = New-Object System.Collections.Generic.List[string]
foreach ($item in $items) {
    $alias = [string]$item.InterfaceAlias
    $adapter = Get-NetAdapter -Name $alias -ErrorAction SilentlyContinue
    if (-not $adapter) { $log.Add(""OMITIDO: no existe adaptador con nombre exacto '$alias'.""); continue }
    $index = [int]$adapter.ifIndex
    try {
        if ([string]$item.Dhcp -eq 'Enabled') {
            Set-NetIPInterface -InterfaceIndex $index -AddressFamily IPv4 -Dhcp Enabled -ErrorAction Stop
            Set-DnsClientServerAddress -InterfaceIndex $index -ResetServerAddresses -ErrorAction Stop
            $log.Add(""OK: '$alias' configurado para DHCP."")
            continue
        }
        Set-NetIPInterface -InterfaceIndex $index -AddressFamily IPv4 -Dhcp Disabled -ErrorAction Stop
        Get-NetIPAddress -InterfaceIndex $index -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -notlike '127.*' } |
            Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue
        $addresses = @($item.Addresses)
        $gateways = @($item.Gateways)
        $first = $true
        foreach ($address in $addresses) {
            $params = @{ InterfaceIndex=$index; IPAddress=[string]$address.Address; PrefixLength=[int]$address.PrefixLength; AddressFamily='IPv4'; ErrorAction='Stop' }
            if ($first -and $gateways.Count -gt 0 -and $gateways[0]) { $params.DefaultGateway=[string]$gateways[0] }
            New-NetIPAddress @params | Out-Null
            $first = $false
        }
        $dns = @($item.DnsServers | Where-Object { $_ })
        if ($dns.Count -gt 0) { Set-DnsClientServerAddress -InterfaceIndex $index -ServerAddresses $dns -ErrorAction Stop }
        else { $log.Add(""AVISO: '$alias' no tenía DNS guardados; no se modificó DNS."") }
        $log.Add(""OK: '$alias' restaurado. IPs=$($addresses.Count); Gateway=$($gateways -join ', '); DNS=$($dns -join ', ')"")
    } catch { $log.Add(""ERROR: '$alias' - $($_.Exception.Message)"") }
}
$log | Set-Content -LiteralPath $env:ONKOS_IPV4_REPORT -Encoding UTF8
";
                            string restoreScriptPath = Path.Combine(Path.GetTempPath(), "ONKOS_Restaurar_IPV4.ps1");
                            string networkRestoreReport = Path.Combine(backupRoot, "Red", "RESULTADO_RESTAURACION_IPV4.txt");
                            File.WriteAllText(restoreScriptPath, restoreScript, new UTF8Encoding(true));
                            try
                            {
                                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{restoreScriptPath}\"")
                                {
                                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
                                };
                                psi.Environment["ONKOS_IPV4_INPUT"] = structuredNetworkFile;
                                psi.Environment["ONKOS_IPV4_REPORT"] = networkRestoreReport;
                                using var process = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar PowerShell para restaurar IPv4.");
                                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                                if (!process.WaitForExit(90000)) { try { process.Kill(true); } catch { } throw new TimeoutException("La restauración IPv4 superó 90 segundos."); }
                                string stdOut = await outputTask;
                                string stdErr = await errorTask;
                                string restoreLog = File.Exists(networkRestoreReport) ? File.ReadAllText(networkRestoreReport, Encoding.UTF8) : stdOut + Environment.NewLine + stdErr;
                                report.Add("=== RESULTADO DE RESTAURACIÓN IPv4 ===");
                                report.Add(restoreLog);
                                if (process.ExitCode != 0) throw new IOException("PowerShell terminó con código " + process.ExitCode + ". " + stdErr);
                                WriteLog("Restauración IPv4 estructurada ejecutada. Revisa RESULTADO_RESTAURACION_IPV4.txt.");
                                ActivityLog.Write("RESTORE", "Restauración IPv4 estructurada", "REVISAR", restoreLog);
                            }
                            finally { try { if (File.Exists(restoreScriptPath)) File.Delete(restoreScriptPath); } catch { } }
                        }
                        else report.Add("Configuración IPv4: no aplicada; el técnico canceló la confirmación final.");
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        report.Add("ERROR RESTAURACIÓN IPv4: " + ex.Message);
                        WriteLog("ERROR RESTAURACIÓN IPv4: " + ex.Message);
                        ActivityLog.Write("RESTORE", "Restauración IPv4 falló", "ERROR", ex.Message);
                    }
                }
                else report.Add("Configuración IPv4: no aplicada por decisión del técnico.");
            }
            else if (File.Exists(legacyNetworkDump))
            {
                report.Add("CONFIGURACIÓN IPv4 ANTIGUA NO APLICADA: el respaldo solo contiene netsh dump, que no incluye DNS de forma fiable y puede no corresponder a los adaptadores destino. Genere un nuevo respaldo con la versión actual para obtener IPv4_config.json.");
                WriteLog("El backup es antiguo: se omitió aplicar netsh dump para evitar una configuración incompleta o asignada al adaptador equivocado.");
            }

            string reportPath = Path.Combine(backupRoot, "INFORME_RESTAURACION.txt");
            File.WriteAllText(reportPath, string.Join(Environment.NewLine, report), Encoding.UTF8);
            SetProgress(files.Count, files.Count, errors == 0 ? "¡Restauración finalizada!" : "Restauración finalizada con errores.", "Terminado");
            WriteLog($"Restauración finalizada. Restaurados: {restored}; omitidos: {skipped}; errores: {errors}.");
            ActivityLog.Write("RESTORE", "Restauración finalizada", errors == 0 ? "OK" : "CON ERRORES", $"Restaurados: {restored} | Omitidos: {skipped} | Errores: {errors}");
            OnkosMessageBox.Show(this, $"Restauración finalizada.\n\nRestaurados: {restored:N0}\nOmitidos (existentes): {skipped:N0}\nErrores: {errors:N0}\n\nInforme: {reportPath}",
                "ONKOS Restauración", MessageBoxButtons.OK, errors == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            WriteLog("ERROR RESTAURACIÓN: " + ex.Message);
            ActivityLog.Write("RESTORE", "Error general", "ERROR", ex.Message);
            OnkosMessageBox.Show(this, ex.Message, "Error restaurando", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _busy = false; _run.Enabled = true; }
    }

    private static string? GetWindowsProfileRoot(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            string usersRoot = Path.Combine(Path.GetPathRoot(full) ?? @"C:\", "Users");
            if (!full.StartsWith(usersRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
            string remainder = full.Substring(usersRoot.Length + 1);
            int separator = remainder.IndexOf(Path.DirectorySeparatorChar);
            if (separator <= 0) return null;
            return Path.Combine(usersRoot, remainder.Substring(0, separator));
        }
        catch { return null; }
    }

    private static string GetBackupFolderLabel(string backupFile, string backupRoot)
    {
        try
        {
            string filesRoot = Path.Combine(backupRoot, "Archivos");
            string relative = Path.GetRelativePath(filesRoot, backupFile);
            return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        }
        catch { return string.Empty; }
    }

    private static string RemapProfilePath(string originalPath, Dictionary<string, string> profileMap, string selectedFolderLabel)
    {
        string? knownFolder = GetKnownFolderName(selectedFolderLabel);
        foreach (var map in profileMap)
        {
            string oldRoot = map.Key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!originalPath.StartsWith(oldRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            string remainder = originalPath.Substring(oldRoot.Length + 1);
            if (knownFolder is not null)
            {
                string[] parts = remainder.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                int folderIndex = Array.FindIndex(parts, p => IsKnownFolderSegment(p, knownFolder));
                if (folderIndex >= 0)
                {
                    string insideFolder = string.Join(Path.DirectorySeparatorChar, parts.Skip(folderIndex + 1));
                    return string.IsNullOrEmpty(insideFolder) ? Path.Combine(map.Value, knownFolder) : Path.Combine(map.Value, knownFolder, insideFolder);
                }
                return Path.Combine(map.Value, knownFolder, remainder);
            }
            return Path.Combine(map.Value, remainder);
        }
        return originalPath;
    }

    private static string? GetKnownFolderName(string label)
    {
        string normalized = label.Replace("_", " ").Trim();
        if (normalized.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) || normalized.Contains("One Drive", StringComparison.OrdinalIgnoreCase)) return "OneDrive";
        if (normalized.Contains("Documentos", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Documents", StringComparison.OrdinalIgnoreCase)) return "Documents";
        if (normalized.Contains("Escritorio", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Desktop", StringComparison.OrdinalIgnoreCase)) return "Desktop";
        if (normalized.Contains("Descargas", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Downloads", StringComparison.OrdinalIgnoreCase)) return "Downloads";
        if (normalized.Contains("Imágenes", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Imagenes", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Pictures", StringComparison.OrdinalIgnoreCase)) return "Pictures";
        if (normalized.Contains("Música", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Musica", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Music", StringComparison.OrdinalIgnoreCase)) return "Music";
        if (normalized.Contains("Videos", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Vídeos", StringComparison.OrdinalIgnoreCase)) return "Videos";
        if (normalized.Contains("Favoritos", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Favorites", StringComparison.OrdinalIgnoreCase)) return "Favorites";
        if (normalized.Contains("Contactos", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Contacts", StringComparison.OrdinalIgnoreCase)) return "Contacts";
        if (normalized.Contains("Juegos guardados", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Saved Games", StringComparison.OrdinalIgnoreCase)) return "Saved Games";
        if (normalized.Contains("Búsquedas", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Busquedas", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Searches", StringComparison.OrdinalIgnoreCase)) return "Searches";
        if (normalized.Contains("Vínculos", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Vinculos", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Links", StringComparison.OrdinalIgnoreCase)) return "Links";
        if (normalized.Contains("Objetos 3D", StringComparison.OrdinalIgnoreCase) || normalized.Contains("3D Objects", StringComparison.OrdinalIgnoreCase)) return "3D Objects";
        return null;
    }

    private static bool IsKnownFolderSegment(string segment, string canonical)
    {
        string value = segment.Trim();
        if (canonical.Equals("Documents", StringComparison.OrdinalIgnoreCase)) return value.Equals("Documents", StringComparison.OrdinalIgnoreCase) || value.Equals("Documentos", StringComparison.OrdinalIgnoreCase);
        if (canonical.Equals("Desktop", StringComparison.OrdinalIgnoreCase)) return value.Equals("Desktop", StringComparison.OrdinalIgnoreCase) || value.Equals("Escritorio", StringComparison.OrdinalIgnoreCase);
        if (canonical.Equals("Downloads", StringComparison.OrdinalIgnoreCase)) return value.Equals("Downloads", StringComparison.OrdinalIgnoreCase) || value.Equals("Descargas", StringComparison.OrdinalIgnoreCase);
        if (canonical.Equals("Pictures", StringComparison.OrdinalIgnoreCase)) return value.Equals("Pictures", StringComparison.OrdinalIgnoreCase) || value.Equals("Imágenes", StringComparison.OrdinalIgnoreCase) || value.Equals("Imagenes", StringComparison.OrdinalIgnoreCase);
        if (canonical.Equals("Music", StringComparison.OrdinalIgnoreCase)) return value.Equals("Music", StringComparison.OrdinalIgnoreCase) || value.Equals("Música", StringComparison.OrdinalIgnoreCase) || value.Equals("Musica", StringComparison.OrdinalIgnoreCase);
        if (canonical.Equals("Videos", StringComparison.OrdinalIgnoreCase)) return value.Equals("Videos", StringComparison.OrdinalIgnoreCase) || value.Equals("Vídeos", StringComparison.OrdinalIgnoreCase);
        if (canonical.Equals("Favorites", StringComparison.OrdinalIgnoreCase)) return value.Equals("Favorites", StringComparison.OrdinalIgnoreCase) || value.Equals("Favoritos", StringComparison.OrdinalIgnoreCase);
        if (canonical.Equals("OneDrive", StringComparison.OrdinalIgnoreCase)) return value.Equals("OneDrive", StringComparison.OrdinalIgnoreCase);
        if (canonical.Equals("3D Objects", StringComparison.OrdinalIgnoreCase)) return value.Equals("3D Objects", StringComparison.OrdinalIgnoreCase) || value.Equals("Objetos 3D", StringComparison.OrdinalIgnoreCase);
        return value.Equals(canonical, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string[]> ParseCsv(string csv)
    {
        var result = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else
            {
                if (c == '"') quoted = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n') { row.Add(field.ToString()); field.Clear(); if (row.Any(v => v.Length > 0)) result.Add(row.ToArray()); row.Clear(); }
                else field.Append(c);
            }
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); result.Add(row.ToArray()); }
        return result;
    }

    private void CollectFilesSafe(string rootPath, string currentPath, string folderName, List<(string Source, string Relative, string FolderName)> files, List<(string Source, string Relative, string FolderName)> directories, List<string> errorDetails)
    {
        try
        {
            string relativeDirectory = Path.GetRelativePath(rootPath, currentPath);
            directories.Add((currentPath, relativeDirectory == "." ? "" : relativeDirectory, folderName));
        }
        catch (Exception ex)
        {
            string detail = $"ORIGEN: {currentPath} | DESTINO: No se pudo determinar | MOTIVO: Error registrando carpeta: {ex.Message}";
            errorDetails.Add(detail);
            WriteLog("ERROR: " + detail);
        }
        try
        {
            foreach (string file in Directory.EnumerateFiles(currentPath))
            {
                try { files.Add((file, Path.GetRelativePath(rootPath, file), folderName)); }
                catch (Exception ex)
                {
                    string detail = $"ORIGEN: {file} | DESTINO: No se pudo determinar | MOTIVO: Error registrando archivo: {ex.Message}";
                    errorDetails.Add(detail);
                    WriteLog("ERROR: " + detail);
                }
            }
        }
        catch (Exception ex)
        {
            string detail = $"ORIGEN: {currentPath} | DESTINO: Archivos/{folderName}/ | MOTIVO: No se pudieron listar archivos: {ex.Message}";
            errorDetails.Add(detail);
            WriteLog("ERROR: " + detail);
        }
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(currentPath))
            {
                try
                {
                    var attributes = File.GetAttributes(directory);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) { WriteLog($"Omitida carpeta de enlace (evita ciclos): {directory}"); continue; }
                    CollectFilesSafe(rootPath, directory, folderName, files, directories, errorDetails);
                }
                catch (Exception ex)
                {
                    string detail = $"ORIGEN: {directory} | DESTINO: Archivos/{folderName}/ | MOTIVO: No se pudo explorar subcarpeta: {ex.Message}";
                    errorDetails.Add(detail);
                    WriteLog("ERROR: " + detail);
                }
            }
        }
        catch (Exception ex)
        {
            string detail = $"ORIGEN: {currentPath} | DESTINO: Archivos/{folderName}/ | MOTIVO: No se pudieron listar subcarpetas: {ex.Message}";
            errorDetails.Add(detail);
            WriteLog("ERROR: " + detail);
        }
    }

    private static string CsvRow(params string[] fields) => string.Join(",", fields.Select(value => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\""));

    private void SetProgress(int done, int total, string status, string current)
    { int pct = total <= 0 ? 0 : Math.Clamp((int)(100.0 * done / total), 0, 100); _bar.Value = pct; _status.Text = status; _currentFile.Text = "Ahora: " + current; }
    private void ReportError(List<string> errorDetails, string source, string destination, string error)
    {
        string detail = $"ORIGEN: {source} | DESTINO: {destination} | MOTIVO: {error}";
        errorDetails.Add(detail);
        WriteLog("ERROR: " + detail);
    }
    private void WriteLog(string message) { if (InvokeRequired) { BeginInvoke(new Action(() => WriteLog(message))); return; } _log.AppendText(message + Environment.NewLine); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); try { SetWindowTheme(Handle, "DarkMode_Explorer", null); SetWindowTheme(_sources.Handle, "DarkMode_Explorer", null); } catch { } }
    [DllImport("uxtheme.dll", CharSet=CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd,string? appName,string? idList);

    private static bool IsSameOrChild(string parent, string child) => IsPathInside(parent, child);

    private static bool IsPathInside(string candidate, string parent)
    {
        try
        {
            string normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalizedCandidate.Equals(normalizedParent, StringComparison.OrdinalIgnoreCase)
                || normalizedCandidate.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
    private static string SafeName(string s) => string.Join("_", s.Split(Path.GetInvalidFileNameChars()));

    private static async Task<string> RunNetshWithTimeoutAsync(string args, int timeoutMs)
    {
        var psi = new ProcessStartInfo("netsh.exe", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar netsh.");
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(true); } catch { }
            throw new TimeoutException($"netsh tardó más de {timeoutMs / 1000} s. Operación cancelada.");
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"netsh terminó con código {p.ExitCode}.\n{stderr}\n{stdout}".Trim());

        return stdout;
    }

    private sealed class FolderEntry
    {
        public string Label { get; }
        public string Path { get; }
        public bool Custom { get; }
        public bool Selected { get; set; }
        public FolderEntry(string label, string path, bool custom, bool selected = false)
        { Label = label; Path = path; Custom = custom; Selected = selected; }
    }
    private static bool IsItemChecked(ListViewItem item) => item.Tag is FolderEntry entry && entry.Selected;
    private static void SetItemChecked(ListViewItem item, bool selected)
    {
        if (item.Tag is FolderEntry entry) entry.Selected = selected;
    }
    private sealed record ProfileChoice(string Name, string Path) { public override string ToString() => $"{Name}    —    {Path}"; }
    private sealed record DriveChoice(string Label, string Root, DriveType Type) { public override string ToString() => Label; }

    private static class ShellIcon
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint dwAttributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path, uint attrs, out SHFILEINFO info, uint size, uint flags);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
        public static Icon Get(string path) { const uint SHGFI_ICON = 0x100, SHGFI_SMALLICON = 0x1; SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_SMALLICON); if (info.hIcon == IntPtr.Zero) return SystemIcons.WinLogo; try { return (Icon)Icon.FromHandle(info.hIcon).Clone(); } finally { DestroyIcon(info.hIcon); } }
    }
}

internal sealed class PurpleCheckBox : CheckBox
{
    public PurpleCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(31, 27, 41);
        ForeColor = Color.FromArgb(244, 241, 250);
        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        AutoSize = false;
        Height = 32;
        Width = 260;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        int boxSize = 16;
        int x = 5;
        int y = (Height - boxSize) / 2;
        var rect = new Rectangle(x, y, boxSize, boxSize);
        Color purple = Color.FromArgb(105, 73, 164);
        Color border = Color.FromArgb(65, 57, 82);
        using var fill = new SolidBrush(Checked ? purple : Color.FromArgb(46, 40, 59));
        using var outline = new Pen(Checked ? Color.FromArgb(126, 91, 190) : border, 1.2F);
        using var shape = RoundedCheckRect(rect, 4);
        g.FillPath(fill, shape);
        g.DrawPath(outline, shape);
        if (Checked)
        {
            using var tick = new Pen(Color.White, 2.1F) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(tick, new[] { new Point(x + 3, y + 8), new Point(x + 6, y + 11), new Point(x + 13, y + 4) });
        }
                int textX = 29;
        if (Image != null)
        {
            int imageSize = Math.Min(24, Height - 6);
            g.DrawImage(Image, new Rectangle(textX, (Height - imageSize) / 2, imageSize, imageSize));
            textX += imageSize + 7;
        }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(textX, 0, Math.Max(0, Width - textX - 5), Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedCheckRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ManualDriveComboBox : ComboBox
{
    private const int WmMouseWheel = 0x020A;
    private const int WmMouseHWheel = 0x020E;

    protected override void WndProc(ref Message m)
    {
        // Evita que la rueda del mouse cambie accidentalmente el disco seleccionado.
        // El usuario debe abrir la lista y elegir el destino manualmente.
        if (m.Msg == WmMouseWheel || m.Msg == WmMouseHWheel) return;
        base.WndProc(ref m);
    }
}

internal sealed class ModernProgressBar : Control
{
    private int _value;
    public int Value { get => _value; set { _value = Math.Clamp(value, 0, 100); Invalidate(); } }

    public ModernProgressBar()
    {
        Height = 30;
        BackColor = Color.FromArgb(39, 34, 51);
        ForeColor = Color.White;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var track = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var trackBrush = new SolidBrush(Color.FromArgb(47, 41, 61));
        using var trackPath = Rounded(track, 7);
        g.FillPath(trackBrush, trackPath);
        int fillWidth = (int)((Width - 1) * (_value / 100.0));
        if (fillWidth > 0)
        {
            var fill = new Rectangle(0, 0, Math.Max(1, fillWidth), Height - 1);
            using var fillBrush = new LinearGradientBrush(fill, Color.FromArgb(85, 53, 138), Color.FromArgb(142, 104, 205), 0f);
            using var fillPath = Rounded(fill, 7);
            g.FillPath(fillBrush, fillPath);
        }
        string text = _value + "%";
        using var font = new Font("Segoe UI", 10F, FontStyle.Bold);
        var size = g.MeasureString(text, font);
        using var textBrush = new SolidBrush(Color.White);
        g.DrawString(text, font, textBrush, (Width - size.Width) / 2, (Height - size.Height) / 2);
    }

    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
