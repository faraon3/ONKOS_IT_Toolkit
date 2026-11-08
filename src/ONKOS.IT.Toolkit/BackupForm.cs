using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal sealed class BackupForm : UserControl
{
    public event Action? BackRequested;
    private readonly ListView _sources = new();
    private readonly ImageList _folderIcons = new();
    private readonly ComboBox _drivePicker = new();
    private readonly TextBox _destination = new();
    private readonly TextBox _backupLabel = new();
    private readonly PurpleCheckBox _ipv4 = new() { Text = "Guardar configuración de red", Checked = true, AutoSize = false };
    private readonly PurpleCheckBox _drivers = new() { Text = "Guardar controladores (drivers)", Checked = true, AutoSize = false };
    private readonly ModernProgressBar _bar = new();
    private readonly Label _status = new();
    private readonly Label _currentFile = new();
    private readonly TextBox _log = new();
    private readonly Button _run = new();
    private readonly Button _addFolder = new();
    private bool _busy;

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
        header.Controls.Add(new Label { Text = "Asistente de respaldo seguro · Clínica ONKOS", ForeColor = Color.FromArgb(235, 225, 250), AutoSize = true, Location = new Point(25, 66) });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12), AutoScroll = true, BackColor = Bg };
        var sourcesGroup = MakeGroup("01  ·  Selecciona la información");
        sourcesGroup.Dock = DockStyle.Top; sourcesGroup.Height = 285;
        _folderIcons.ColorDepth = ColorDepth.Depth32Bit; _folderIcons.ImageSize = new Size(24, 24);
        _sources.Dock = DockStyle.Fill; _sources.View = View.Tile; _sources.TileSize = new Size(300, 42); _sources.CheckBoxes = false;
        _sources.FullRowSelect = true; _sources.HideSelection = false; _sources.BorderStyle = BorderStyle.None; _sources.CheckBoxes = false; _sources.OwnerDraw = true;
        _sources.DrawColumnHeader += (_, e) => { if (e.Header is null) return; using var brush = new SolidBrush(Purple); using var font = new Font("Segoe UI", 9.5F, FontStyle.Bold); e.Graphics.FillRectangle(brush, e.Bounds); TextRenderer.DrawText(e.Graphics, e.Header.Text ?? string.Empty, font, e.Bounds, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter); };
        _sources.DrawItem += DrawSourceTile;
        _sources.DrawSubItem += DrawSourceSubItem;
        _sources.MouseDown += SourceMouseDown;
        _sources.KeyDown += (_, e) => { if (e.KeyCode == Keys.Space && _sources.SelectedItems.Count > 0) { var item = _sources.SelectedItems[0]; item.Checked = !item.Checked; _sources.Invalidate(item.Bounds); e.Handled = true; } };
        _sources.BackColor = Surface; _sources.ForeColor = TextMain; _sources.Font = new Font("Segoe UI", 9.5F);
        _sources.SmallImageList = _folderIcons;
        _sources.Columns.Add("Carpeta / ubicación", 220); _sources.Columns.Add("Ruta", 500); 
        _sources.Resize += (_, _) => { if (_sources.Columns.Count > 1) { _sources.Columns[0].Width = Math.Min(230, Math.Max(170, _sources.ClientSize.Width / 3)); _sources.Columns[1].Width = Math.Max(120, _sources.ClientSize.Width - _sources.Columns[0].Width - 4); } };
        LoadKnownFolders();
        var sourceButtons = new Panel { Dock = DockStyle.Bottom, Height = 42, BackColor = Surface };
        _addFolder.Text = "+  Agregar carpeta desde cualquier disco"; StyleButton(_addFolder, true);
        _addFolder.SetBounds(8, 6, 270, 30); _addFolder.Click += (_, _) => AddCustomFolder();
        sourceButtons.Controls.Add(_addFolder); sourcesGroup.Controls.Add(_sources); sourcesGroup.Controls.Add(sourceButtons);

        var destinationGroup = MakeGroup("03  ·  Elige dónde guardar el respaldo");
        destinationGroup.Dock = DockStyle.Top; destinationGroup.Height = 118;
        var destinationRow = new Panel { Dock = DockStyle.Fill, BackColor = Surface };
        _drivePicker.DropDownStyle = ComboBoxStyle.DropDownList; StyleCombo(_drivePicker); _drivePicker.DrawMode = DrawMode.OwnerDrawFixed; _drivePicker.ItemHeight = 25; _drivePicker.DrawItem += DrawDriveItem;
        _drivePicker.SetBounds(10, 8, 330, 34);
        _drivePicker.SelectedIndexChanged += (_, _) => { if (_drivePicker.SelectedItem is DriveChoice d) _destination.Text = d.Root; };
        var choose = new Button { Text = "Examinar carpeta / red…", Width = 220, Height = 34, Location = new Point(352, 8) };
        StyleButton(choose, true); choose.Click += (_, _) => ChooseDestination();
        _destination.ReadOnly = true; StyleTextBox(_destination);
        _destination.SetBounds(10, 52, 850, 32); _destination.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        destinationRow.Controls.Add(_drivePicker); destinationRow.Controls.Add(choose); destinationRow.Controls.Add(_destination);
        destinationGroup.Controls.Add(destinationRow); RefreshDrives();

        // Opciones avanzadas plegables para mantener una pantalla simple.
        var optionsGroup = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Surface, Margin = new Padding(0, 0, 0, 8) };
        var optionsToggle = new Button { Text = "⚙  Opciones adicionales  ▸", Location = new Point(8, 5), Size = new Size(280, 32), TextAlign = ContentAlignment.MiddleLeft };
        StyleButton(optionsToggle, true);
        var options = new Panel { Location = new Point(8, 42), Height = 94, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Surface };
        StyleCheck(_ipv4); StyleCheck(_drivers);
        _ipv4.SetBounds(8, 4, 270, 32);
        _drivers.SetBounds(290, 4, 340, 32);
        var nameLabel = new Label { Text = "Nombre de la copia (opcional)", ForeColor = TextMain, Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(8, 43) };
        _backupLabel.SetBounds(8, 65, 850, 30); _backupLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _backupLabel.PlaceholderText = "Ejemplo: Recepcion_Principal";
        StyleTextBox(_backupLabel);
        options.Controls.Add(_ipv4); options.Controls.Add(_drivers); options.Controls.Add(nameLabel); options.Controls.Add(_backupLabel);
        options.Visible = false;
        optionsToggle.Click += (_, _) =>
        {
            options.Visible = !options.Visible;
            optionsGroup.Height = options.Visible ? 145 : 42;
            optionsToggle.Text = options.Visible ? "⚙  Opciones adicionales  ▾" : "⚙  Opciones adicionales  ▸";
        };
        optionsGroup.Controls.Add(options); optionsGroup.Controls.Add(optionsToggle);

        var progressGroup = MakeGroup("04  ·  Progreso de la copia");
        progressGroup.Dock = DockStyle.Top; progressGroup.Height = 112;
        var progressPanel = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(3), BackColor = Surface2 };
        _bar.Dock = DockStyle.Fill; _bar.Value = 0;
        progressPanel.Controls.Add(_bar);
        _status.Dock = DockStyle.Top; _status.Height = 24; _status.Text = "Listo para comenzar"; _status.ForeColor = TextMain;
        _currentFile.Dock = DockStyle.Top; _currentFile.Height = 25; _currentFile.Text = "Ahora: —"; _currentFile.ForeColor = TextMuted; _currentFile.AutoEllipsis = true;
        progressGroup.Controls.Add(_currentFile); progressGroup.Controls.Add(_status); progressGroup.Controls.Add(progressPanel);

        // Detalles técnicos plegables, ocultos por defecto.
        var logGroup = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Surface, Margin = new Padding(0, 0, 0, 8) };
        var logToggle = new Button { Text = "☰  Ver detalles técnicos  ▸", Location = new Point(8, 5), Size = new Size(280, 32), TextAlign = ContentAlignment.MiddleLeft };
        StyleButton(logToggle, true);
        _log.SetBounds(8, 42, 850, 75); _log.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _log.Multiline = true; _log.ReadOnly = true; _log.ScrollBars = ScrollBars.Vertical;
        _log.Font = new Font("Consolas", 9F); _log.BackColor = Color.FromArgb(13, 12, 18); _log.ForeColor = Color.FromArgb(203, 195, 218); _log.BorderStyle = BorderStyle.None;
        _log.Visible = false;
        logToggle.Click += (_, _) =>
        {
            _log.Visible = !_log.Visible;
            logGroup.Height = _log.Visible ? 125 : 42;
            logToggle.Text = _log.Visible ? "☰  Ocultar detalles técnicos  ▾" : "☰  Ver detalles técnicos  ▸";
        };
        logGroup.Controls.Add(_log); logGroup.Controls.Add(logToggle);

        _run.Text = "INICIAR COPIA DE SEGURIDAD"; _run.Dock = DockStyle.Bottom; _run.Height = 50; StyleButton(_run, false);
        _run.Font = new Font("Segoe UI", 11F, FontStyle.Bold); _run.Click += async (_, _) => await StartBackupAsync();
        content.Controls.Add(logGroup); content.Controls.Add(progressGroup); content.Controls.Add(destinationGroup); content.Controls.Add(optionsGroup); content.Controls.Add(sourcesGroup);
        Controls.Add(content); Controls.Add(_run); Controls.Add(header);
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
    }

    private void AddKnown(string label, Environment.SpecialFolder? special, bool selected, string iconKey, string? customPath = null)
    {
        string path = customPath ?? Environment.GetFolderPath(special!.Value);
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) AddListItem(label, path, selected, iconKey);
    }
    private void AddListItem(string label, string path, bool selected, string iconKey)
    {
        int icon = GetIconIndex(iconKey, path);
        var item = new ListViewItem(label, icon) { Checked = selected, Tag = new FolderEntry(label, path, !string.IsNullOrWhiteSpace(iconKey)) };
        item.SubItems.Add(path); _sources.Items.Add(item);
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
        using var cb=new SolidBrush(item.Checked ? Purple : Color.FromArgb(46,40,59)); using var pen=new Pen(item.Checked ? PurpleHover : Border,1.2f); using var path=RoundedRect(box,4); e.Graphics.FillPath(cb,path); e.Graphics.DrawPath(pen,path);
        if(item.Checked){using var check=new Pen(Color.White,2.2f); e.Graphics.DrawLines(check,new[]{new Point(x+3,y+8),new Point(x+6,y+11),new Point(x+13,y+4)});}
        if(item.ImageIndex>=0 && _folderIcons.Images.Count>item.ImageIndex){var img=_folderIcons.Images[item.ImageIndex]; if(img!=null)e.Graphics.DrawImage(img,x+23,bounds.Y+(bounds.Height-24)/2,24,24);}
        TextRenderer.DrawText(e.Graphics,item.Text,_sources.Font,new Rectangle(x+52,bounds.Y+2, bounds.Width-58,18),TextMain,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        string pathText=item.SubItems.Count>1?item.SubItems[1].Text:string.Empty; TextRenderer.DrawText(e.Graphics,pathText,new Font("Segoe UI",7.5F),new Rectangle(x+52,bounds.Y+19,bounds.Width-58,17),TextMuted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }

    private void DrawSourceSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item is null || e.SubItem is null) return; 
        using var bg = new SolidBrush(e.Item.Selected ? Color.FromArgb(58, 46, 78) : Surface);
        e.Graphics.FillRectangle(bg, e.Bounds);
        if (e.ColumnIndex == 0)
        {
            int x=e.Bounds.X+8, y=e.Bounds.Y+(e.Bounds.Height-16)/2;
            var box=new Rectangle(x,y,16,16);
            using var cb=new SolidBrush(e.Item.Checked ? Purple : Color.FromArgb(46,40,59));
            using var pen=new Pen(e.Item.Checked ? PurpleHover : Border,1.2f);
            using var path=RoundedRect(box,4);
            e.Graphics.FillPath(cb,path); e.Graphics.DrawPath(pen,path);
            if(e.Item.Checked){using var check=new Pen(Color.White,2.2f); e.Graphics.DrawLines(check,new[]{new Point(x+3,y+8),new Point(x+6,y+11),new Point(x+13,y+4)});}
            if (e.Item.ImageIndex >= 0 && _sources.SmallImageList?.Images is not null && e.Item.ImageIndex < _sources.SmallImageList.Images.Count)
            {
                var icon = _sources.SmallImageList.Images[e.Item.ImageIndex];
                if (icon is not null) e.Graphics.DrawImage(icon, x+23, e.Bounds.Y+(e.Bounds.Height-22)/2, 22, 22);
            }
            TextRenderer.DrawText(e.Graphics,e.SubItem.Text ?? string.Empty,_sources.Font,new Rectangle(x+50,e.Bounds.Y,e.Bounds.Width-52,e.Bounds.Height),TextMain,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
        else TextRenderer.DrawText(e.Graphics,e.SubItem.Text,_sources.Font,e.Bounds,TextMuted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
    private void SourceMouseDown(object? sender, MouseEventArgs e)
    {
        var hit=_sources.HitTest(e.Location);
        if(hit.Item!=null && e.X < hit.Item.Bounds.X+42)
        { hit.Item.Checked=!hit.Item.Checked; _sources.Invalidate(hit.Item.Bounds); }
    }
    private void DrawDriveItem(object? sender, DrawItemEventArgs e)
    {
        if(e.Index<0)return;
        var drive=(DriveChoice)_drivePicker.Items[e.Index]!;
        e.DrawBackground();
        string symbol=drive.Type==DriveType.Removable ? "USB / EXTERNO" : drive.Type==DriveType.Network ? "RED" : "DISCO";
        var iconPath=Path.Combine(AppContext.BaseDirectory,"Assets","Unidad_USB.png");
        if(File.Exists(iconPath))
        {
            try { using var img=Image.FromFile(iconPath); e.Graphics.DrawImage(img,e.Bounds.X+4,e.Bounds.Y+2,20,20); }
            catch { }
        }
        Color fore=(e.State&DrawItemState.Selected)!=0?Color.White:TextMain;
        TextRenderer.DrawText(e.Graphics,symbol+"  "+drive.Label,_drivePicker.Font,new Rectangle(e.Bounds.X+28,e.Bounds.Y,e.Bounds.Width-30,e.Bounds.Height),fore,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
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
        { MessageBox.Show(this, "Esta carpeta ya está en la lista.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        AddListItem(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), path, true, "Unidad");
    }
    private void RefreshDrives()
    {
        _drivePicker.Items.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        { try { _drivePicker.Items.Add(new DriveChoice($"{drive.Name} · {drive.VolumeLabel} · {drive.DriveType} · {drive.AvailableFreeSpace / 1024 / 1024 / 1024} GB libres", drive.RootDirectory.FullName, drive.DriveType)); } catch { } }
        if (_drivePicker.Items.Count > 0) _drivePicker.SelectedIndex = 0;
    }
    private void ChooseDestination()
    {
        using var dialog = new FolderBrowserDialog { Description = "Selecciona el destino del backup (USB, disco externo o red)." };
        if (dialog.ShowDialog(this) == DialogResult.OK) _destination.Text = dialog.SelectedPath;
    }

    private async Task StartBackupAsync()
    {
        if (_busy) return;
        var selected = _sources.Items.Cast<ListViewItem>().Where(x => x.Checked).Select(x => (FolderEntry)x.Tag!).ToList();
        if (selected.Count == 0 && !_ipv4.Checked && !_drivers.Checked) { MessageBox.Show(this, "Selecciona carpetas o una opción adicional.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        string dest = _destination.Text.Trim();
        if (string.IsNullOrWhiteSpace(dest) || !Directory.Exists(dest)) { MessageBox.Show(this, "Selecciona primero el destino del respaldo.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        foreach (var f in selected)
            if (IsSameOrChild(dest, f.Path)) { MessageBox.Show(this, $"El destino está dentro de la carpeta origen ({f.Label}). Elige otra ubicación.", "Destino no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        string customPrefix = SafeName(_backupLabel.Text.Trim());
        string folderPrefix = string.IsNullOrWhiteSpace(customPrefix) ? "" : customPrefix + "_";
        string root = Path.Combine(dest, $"{folderPrefix}{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}");
        _busy = true; _run.Enabled = false; _log.Clear(); SetProgress(0, 0, "Preparando respaldo…", "Revisando lo seleccionado");
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
            string filesRoot = Path.Combine(root, "Archivos");
            Directory.CreateDirectory(filesRoot);

            int errors = errorDetails.Count;

            // Crear primero toda la estructura de carpetas, incluso las vacías.
            foreach (var dir in directoryList)
            {
                string targetDir = Path.Combine(filesRoot, dir.FolderName, dir.Relative);
                try
                {
                    Directory.CreateDirectory(targetDir);
                }
                catch (Exception ex)
                {
                    errors++;
                    ReportError(errorDetails, dir.Source, targetDir, "No se pudo crear la carpeta en el respaldo: " + ex.Message);
                }
            }

            int total = Math.Max(fileList.Count + (_ipv4.Checked ? 1 : 0) + (_drivers.Checked ? 1 : 0), 1);
            int done = 0, copied = 0;
            IProgress<(int done, int total, string message, string current)> progress = new Progress<(int done, int total, string message, string current)>(p => { SetProgress(p.done, p.total, $"Copiando… {p.done} de {p.total}", p.current); if (!string.IsNullOrWhiteSpace(p.message)) WriteLog(p.message); });
            await Task.Run(() =>
            {
                foreach (var item in fileList)
                {
                    string targetDir = Path.Combine(root, "Archivos", item.FolderName, Path.GetDirectoryName(item.Relative) ?? "");
                    string targetFile = Path.Combine(targetDir, Path.GetFileName(item.Source));
                    try { Directory.CreateDirectory(targetDir); File.Copy(item.Source, targetFile, true); copied++; }
                    catch (Exception ex) { errors++; ReportError(errorDetails, item.Source, targetFile, ex.Message); }
                    done++; progress.Report((done, total, "", Path.GetFileName(item.Source)));
                }
            });
            if (_ipv4.Checked)
            {
                try { string networkFile = Path.Combine(root, "Red", "IPv4_netsh_dump.txt"); Directory.CreateDirectory(Path.GetDirectoryName(networkFile)!); await RunProcessAsync("cmd.exe", $"/c netsh interface dump > \"{networkFile}\"", true); File.WriteAllText(Path.Combine(root, "Red", "LEEME.txt"), "Backup netsh. Antes de restaurar, verificar adaptador, IP y permisos administrativos."); done++; progress.Report((done, total, "IPv4 exportado (netsh).", "Configuración IPv4")); }
                catch (Exception ex) { errors++; WriteLog("ERROR respaldando IPv4: " + ex.Message); }
            }
            if (_drivers.Checked)
            {
                try { string driverDir = Path.Combine(root, "Drivers"); Directory.CreateDirectory(driverDir); var result = await RunProcessAsync("pnputil", $"/export-driver * \"{driverDir}\"", false); File.WriteAllText(Path.Combine(driverDir, "EXPORT_LOG.txt"), result); done++; progress.Report((done, total, "Exportación de drivers ejecutada.", "Controladores instalados")); }
                catch (Exception ex) { errors++; WriteLog("ERROR exportando drivers: " + ex.Message); }
            }
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
                $"Errores: {errors}{Environment.NewLine}" +
                $"Informe detallado: INFORME_ERRORES_BACKUP.txt{Environment.NewLine}");

            SetProgress(total, total, errors == 0 ? "¡Respaldo completado!" : "Respaldo completado con algunos problemas.", "Terminado");
            WriteLog($"Finalizado. Copiados: {copied}. Errores: {errors}.{Environment.NewLine}Ruta: {root}");
            MessageBox.Show(this,
                $"Backup finalizado.{Environment.NewLine}Copiados: {copied}{Environment.NewLine}Errores: {errors}{Environment.NewLine}{Environment.NewLine}{root}{Environment.NewLine}{Environment.NewLine}Informe: INFORME_ERRORES_BACKUP.txt",
                "ONKOS Backup", MessageBoxButtons.OK, errors == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex) { _status.Text = "El backup falló."; WriteLog("ERROR GENERAL: " + ex.Message); MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { _busy = false; _run.Enabled = true; }
    }
    private void CollectFilesSafe(string rootPath, string currentPath, string folderName, List<(string Source, string Relative, string FolderName)> files, List<(string Source, string Relative, string FolderName)> directories, List<string> errorDetails)
    {
        // Registrar esta carpeta antes de recorrer su contenido. Esto permite conservar
        // tanto las carpetas vacías como las carpetas que contienen otras carpetas vacías.
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
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        WriteLog($"Omitida carpeta de enlace (evita ciclos): {directory}");
                        continue;
                    }
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

    private static bool IsSameOrChild(string parent, string child) { try { return Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar).StartsWith(Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase); } catch { return false; } }
    private static string SafeName(string s) => string.Join("_", s.Split(Path.GetInvalidFileNameChars()));
    private static async Task<string> RunProcessAsync(string exe, string args, bool shellRedirect)
    { var psi = new ProcessStartInfo(exe, args) { UseShellExecute = shellRedirect, CreateNoWindow = true, RedirectStandardOutput = !shellRedirect, RedirectStandardError = !shellRedirect }; using var p = Process.Start(psi) ?? throw new InvalidOperationException($"No se pudo iniciar {exe}"); if (shellRedirect) { await p.WaitForExitAsync(); return $"Proceso terminado con código {p.ExitCode}"; } string stdout = await p.StandardOutput.ReadToEndAsync(); string stderr = await p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync(); return stdout + Environment.NewLine + stderr + Environment.NewLine + "ExitCode=" + p.ExitCode; }
    private sealed record FolderEntry(string Label, string Path, bool Custom);
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

        TextRenderer.DrawText(g, Text, Font, new Rectangle(29, 0, Math.Max(0, Width - 34), Height), ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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

internal sealed class ModernProgressBar : Control
{
    private int _value;
    public int Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, 100); Invalidate(); }
    }

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
