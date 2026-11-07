using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal sealed class BackupForm : Form
{
    private readonly CheckedListBox _sources = new();
    private readonly ComboBox _drivePicker = new();
    private readonly TextBox _destination = new();
    private readonly CheckBox _ipv4 = new() { Text = "Respaldar configuración de red IPv4", Checked = true, AutoSize = true };
    private readonly CheckBox _drivers = new() { Text = "Exportar controladores instalados (requiere administrador)", Checked = true, AutoSize = true };
    private readonly ProgressBar _bar = new();
    private readonly Label _percent = new();
    private readonly Label _status = new();
    private readonly Label _fileStatus = new();
    private readonly TextBox _log = new();
    private readonly Button _run = new();
    private readonly Button _addFolder = new();
    private readonly Button _removeFolder = new();
    private bool _busy;

    public BackupForm()
    {
        Text = "Backup y migración | ONKOS IT Toolkit";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 760);
        MinimumSize = new Size(820, 650);
        BackColor = Color.FromArgb(245, 243, 249);
        Font = new Font("Segoe UI", 9F);

        var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.FromArgb(85, 53, 138) };
        header.Controls.Add(new Label { Text = "ASISTENTE DE BACKUP Y MIGRACIÓN", ForeColor = Color.White, Font = new Font("Segoe UI", 17F, FontStyle.Bold), AutoSize = true, Location = new Point(20, 13) });
        header.Controls.Add(new Label { Text = "Selecciona los datos, guarda el respaldo y restáuralos cuando lo necesites.", ForeColor = Color.WhiteSmoke, AutoSize = true, Location = new Point(22, 48) });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
        var sourcesGroup = new GroupBox { Text = "1. Información que deseas respaldar", Dock = DockStyle.Top, Height = 255, Padding = new Padding(10) };
        _sources.Dock = DockStyle.Fill;
        _sources.CheckOnClick = true;
        _sources.HorizontalScrollbar = true;
        LoadKnownFolders();
        var sourceButtons = new Panel { Dock = DockStyle.Bottom, Height = 38 };
        _addFolder.Text = "+ Agregar carpeta de cualquier unidad";
        _addFolder.SetBounds(4, 4, 235, 28);
        _addFolder.Click += (_, _) => AddCustomFolder();
        _removeFolder.Text = "Quitar seleccionada";
        _removeFolder.SetBounds(245, 4, 155, 28);
        _removeFolder.Click += (_, _) => RemoveSelectedFolder();
        sourceButtons.Controls.Add(_addFolder);
        sourceButtons.Controls.Add(_removeFolder);
        sourcesGroup.Controls.Add(_sources);
        sourcesGroup.Controls.Add(sourceButtons);

        var destinationGroup = new GroupBox { Text = "2. ¿Dónde guardar la copia?", Dock = DockStyle.Top, Height = 105, Padding = new Padding(10) };
        var destinationRow = new Panel { Dock = DockStyle.Fill };
        _drivePicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _drivePicker.SetBounds(8, 8, 300, 28);
        _drivePicker.SelectedIndexChanged += (_, _) => { if (_drivePicker.SelectedItem is DriveChoice d) _destination.Text = d.Root; };
        var choose = new Button { Text = "Elegir carpeta / ruta de red...", Width = 220, Height = 28, Location = new Point(320, 8) };
        choose.Click += (_, _) => ChooseDestination();
        _destination.ReadOnly = true;
        _destination.SetBounds(8, 43, 700, 26);
        _destination.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        destinationRow.Controls.Add(_drivePicker); destinationRow.Controls.Add(choose); destinationRow.Controls.Add(_destination);
        destinationGroup.Controls.Add(destinationRow);
        RefreshDrives();

        var optionsGroup = new GroupBox { Text = "3. Configuración adicional", Dock = DockStyle.Top, Height = 75 };
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), FlowDirection = FlowDirection.LeftToRight };
        options.Controls.Add(_ipv4); options.Controls.Add(_drivers);
        optionsGroup.Controls.Add(options);

        var progressGroup = new GroupBox { Text = "4. Progreso del respaldo", Dock = DockStyle.Top, Height = 92 };
        _bar.SetBounds(12, 25, 780, 24);
        _bar.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        _percent.Text = "0%"; _percent.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _percent.SetBounds(800, 25, 55, 24); _percent.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _status.Text = "Listo para comenzar."; _status.SetBounds(12, 53, 500, 22);
        _fileStatus.Text = "Aún no se han procesado archivos."; _fileStatus.SetBounds(515, 53, 340, 22); _fileStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        progressGroup.Controls.Add(_bar); progressGroup.Controls.Add(_percent); progressGroup.Controls.Add(_status); progressGroup.Controls.Add(_fileStatus);

        var logGroup = new GroupBox { Text = "Registro de actividad", Dock = DockStyle.Fill, Padding = new Padding(8) };
        _log.Dock = DockStyle.Fill; _log.Multiline = true; _log.ReadOnly = true; _log.ScrollBars = ScrollBars.Vertical;
        _log.Font = new Font("Consolas", 9F); _log.BackColor = Color.White;
        logGroup.Controls.Add(_log);

        _run.Text = "INICIAR COPIA DE SEGURIDAD";
        _run.Dock = DockStyle.Bottom; _run.Height = 48;
        _run.BackColor = Color.FromArgb(85, 53, 138); _run.ForeColor = Color.White; _run.FlatStyle = FlatStyle.Flat;
        _run.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _run.Click += async (_, _) => await StartBackupAsync();

        content.Controls.Add(logGroup);
        content.Controls.Add(progressGroup);
        content.Controls.Add(optionsGroup);
        content.Controls.Add(destinationGroup);
        content.Controls.Add(sourcesGroup);
        Controls.Add(content); Controls.Add(_run); Controls.Add(header);
    }

    private void LoadKnownFolders()
    {
        AddKnown("Escritorio", Environment.SpecialFolder.Desktop, true);
        AddKnown("Documentos", Environment.SpecialFolder.MyDocuments, true);
        AddKnown("Descargas", null, true, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        AddKnown("Imágenes", Environment.SpecialFolder.MyPictures, true);
        AddKnown("Música", Environment.SpecialFolder.MyMusic, true);
        AddKnown("Videos", Environment.SpecialFolder.MyVideos, true);
        AddKnown("Favoritos", Environment.SpecialFolder.Favorites, true);
        AddKnown("Contactos", Environment.SpecialFolder.Contacts, true);
        AddKnown("Búsquedas", Environment.SpecialFolder.Search, true);
        AddKnown("Juegos guardados", Environment.SpecialFolder.SavedGames, true);
        AddKnown("OneDrive", null, false, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive"));
    }

    private void AddKnown(string label, Environment.SpecialFolder? special, bool selected, string? customPath = null)
    {
        string path = customPath ?? Environment.GetFolderPath(special!.Value);
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            _sources.Items.Add(new FolderEntry(label, path, false), selected);
    }

    private void AddCustomFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Selecciona una carpeta específica, incluso en D:, E: u otra unidad." };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string path = dialog.SelectedPath;
        if (_sources.Items.Cast<FolderEntry>().Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "Esta carpeta ya está en la lista.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        int index = _sources.Items.Add(new FolderEntry(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), path, true), true);
        _sources.SelectedIndex = index;
    }

    private void RemoveSelectedFolder()
    {
        if (_sources.SelectedItem is FolderEntry entry && entry.Custom)
            _sources.Items.Remove(entry);
        else
            MessageBox.Show(this, "Selecciona una carpeta personalizada para quitarla. Las carpetas estándar no se eliminan de la lista.", "ONKOS");
    }

    private void RefreshDrives()
    {
        _drivePicker.Items.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            try { _drivePicker.Items.Add(new DriveChoice($"{drive.Name}  |  {drive.VolumeLabel}  |  {drive.DriveType}  |  {drive.AvailableFreeSpace / 1024 / 1024 / 1024} GB libres", drive.RootDirectory.FullName)); }
            catch { }
        }
        if (_drivePicker.Items.Count > 0) _drivePicker.SelectedIndex = 0;
    }

    private void ChooseDestination()
    {
        using var dialog = new FolderBrowserDialog { Description = "Selecciona la carpeta destino del backup. Puedes usar una unidad USB o ruta de red." };
        if (dialog.ShowDialog(this) == DialogResult.OK) _destination.Text = dialog.SelectedPath;
    }

    private async Task StartBackupAsync()
    {
        if (_busy) return;
        var selected = _sources.CheckedItems.Cast<FolderEntry>().ToList();
        if (selected.Count == 0 && !_ipv4.Checked && !_drivers.Checked)
        {
            MessageBox.Show(this, "Selecciona carpetas o una opción de configuración para respaldar.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        string dest = _destination.Text.Trim();
        if (string.IsNullOrWhiteSpace(dest) || !Directory.Exists(dest))
        {
            MessageBox.Show(this, "Selecciona primero una unidad o carpeta de destino.", "ONKOS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        foreach (var f in selected)
        {
            if (IsSameOrChild(dest, f.Path))
            {
                MessageBox.Show(this, $"El destino está dentro de la carpeta origen ({f.Label}). Elige otro destino para evitar copiar el backup dentro de sí mismo.", "Destino no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        string root = Path.Combine(dest, $"ONKOS_BACKUP_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}");
        _busy = true; _run.Enabled = false; _log.Clear(); _bar.Value = 0; _percent.Text = "0%";
        try
        {
            Directory.CreateDirectory(root);
            var fileList = new List<(string Source, string Relative, string FolderName)>();
            foreach (var entry in selected)
            {
                try
                {
                    foreach (string file in Directory.EnumerateFiles(entry.Path, "*", SearchOption.AllDirectories))
                    {
                        string rel = Path.GetRelativePath(entry.Path, file);
                        fileList.Add((file, rel, SafeName(entry.Label)));
                    }
                }
                catch (Exception ex) { WriteLog($"Aviso explorando {entry.Path}: {ex.Message}"); }
            }

            int total = fileList.Count;
            _bar.Maximum = Math.Max(1, total + (_ipv4.Checked ? 1 : 0) + (_drivers.Checked ? 1 : 0));
            int done = 0, copied = 0, errors = 0;\n            int maxWork = _bar.Maximum;
            var progress = new Progress<(int done, int total, string message)>(p =>
            {
                _bar.Value = Math.Min(p.done, _bar.Maximum);
                int percent = (int)(100.0 * p.done / Math.Max(1, p.total));
                _percent.Text = percent + "%";
                _status.Text = "Procesando copia de seguridad...";
                _fileStatus.Text = $"{p.done} / {p.total} elementos";
                WriteLog(p.message);
            });

            await Task.Run(() =>
            {
                foreach (var item in fileList)
                {
                    try
                    {
                        string targetDir = Path.Combine(root, "Archivos", item.FolderName, Path.GetDirectoryName(item.Relative) ?? "");
                        Directory.CreateDirectory(targetDir);
                        File.Copy(item.Source, Path.Combine(targetDir, Path.GetFileName(item.Source)), true);
                        copied++;
                    }
                    catch (Exception ex) { errors++; ReportError(item.Source, ex.Message); }
                    done++;
                    progress.Report((done, maxWork, $"[{done}/{_bar.Maximum}] {(errors > 0 && copied + errors == done ? "ERROR" : "OK")} {item.Source}"));
                }
            });

            if (_ipv4.Checked)
            {
                try
                {
                    string networkFile = Path.Combine(root, "Red", "IPv4_netsh_dump.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(networkFile)!);
                    await RunProcessAsync("netsh", $"interface dump > \"{networkFile}\"", true);
                    File.WriteAllText(Path.Combine(root, "Red", "LEEME.txt"), "Archivo netsh interface dump. La restauración requiere revisar el adaptador y permisos de administrador. No ejecutar en otro equipo sin verificar la configuración.");
                    done++; progress.Report((done, _bar.Maximum, "Configuración de red guardada (netsh)."));
                }
                catch (Exception ex) { errors++; WriteLog("ERROR respaldando IPv4: " + ex.Message); }
            }

            if (_drivers.Checked)
            {
                try
                {
                    string driverDir = Path.Combine(root, "Drivers");
                    Directory.CreateDirectory(driverDir);
                    var result = await RunProcessAsync("pnputil", $"/export-driver * \"{driverDir}\"", false);
                    File.WriteAllText(Path.Combine(driverDir, "EXPORT_LOG.txt"), result);
                    if (result.Contains("error", StringComparison.OrdinalIgnoreCase)) WriteLog("Aviso: pnputil reportó errores. Revisa EXPORT_LOG.txt.");
                    done++; progress.Report((done, _bar.Maximum, "Exportación de controladores solicitada."));
                }
                catch (Exception ex) { errors++; WriteLog("ERROR exportando drivers: " + ex.Message + " (puede requerir Windows 10/11 y administrador)."); }
            }

            File.WriteAllText(Path.Combine(root, "BACKUP_INFO.txt"),
                $"ONKOS IT Toolkit backup\r\nEquipo origen: {Environment.MachineName}\r\nUsuario: {Environment.UserName}\r\nSistema: {Environment.OSVersion}\r\nFecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\nArchivos copiados: {copied}\r\nErrores: {errors}\r\n");
            _status.Text = errors == 0 ? "Backup terminado correctamente." : "Backup terminado con advertencias.";
            _percent.Text = "100%";
            _bar.Value = _bar.Maximum;
            WriteLog($"\r\nTerminado. Archivos copiados: {copied}. Errores: {errors}.\r\nRuta: {root}");
            MessageBox.Show(this, $"Backup finalizado.\r\nArchivos copiados: {copied}\r\nErrores: {errors}\r\n\r\n{root}", "ONKOS Backup", MessageBoxButtons.OK, errors == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _status.Text = "El backup falló.";
            WriteLog("ERROR GENERAL: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Error de backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _busy = false; _run.Enabled = true; }
    }

    private void ReportError(string path, string error) => WriteLog($"ERROR: {path} | {error}");
    private void WriteLog(string message)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => WriteLog(message))); return; }
        _log.AppendText(message + Environment.NewLine);
    }
    private static bool IsSameOrChild(string parent, string child)
    {
        try { return Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar).StartsWith(Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
    private static string SafeName(string s) => string.Join("_", s.Split(Path.GetInvalidFileNameChars()));
    private static async Task<string> RunProcessAsync(string exe, string args, bool shellRedirect)
    {
        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = shellRedirect, CreateNoWindow = true, RedirectStandardOutput = !shellRedirect, RedirectStandardError = !shellRedirect };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"No se pudo iniciar {exe}");
        if (shellRedirect) { await p.WaitForExitAsync(); return $"Proceso terminado con código {p.ExitCode}"; }
        string stdout = await p.StandardOutput.ReadToEndAsync();
        string stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return stdout + Environment.NewLine + stderr + Environment.NewLine + "ExitCode=" + p.ExitCode;
    }

    private sealed record FolderEntry(string Label, string Path, bool Custom)
    {
        public override string ToString() => $"{Label}  —  {Path}";
    }
    private sealed record DriveChoice(string Label, string Root)
    {
        public override string ToString() => Label;
    }
}