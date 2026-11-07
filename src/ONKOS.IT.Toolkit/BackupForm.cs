using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal sealed class BackupForm : Form
{
    private readonly List<(CheckBox Box, string Category, string Path)> _folders = new();
    private readonly TextBox _destination = new();
    private readonly ProgressBar _bar = new();
    private readonly Label _status = new();
    private readonly TextBox _log = new();
    private readonly Button _run = new();

    public BackupForm()
    {
        Text = "Backup y migración - ONKOS";
        StartPosition = FormStartPosition.CenterParent; Size = new Size(760, 600);
        MinimumSize = new Size(680, 520); Font = new Font("Segoe UI", 9F);
        var title = new Label { Text = "COPIA DE SEGURIDAD", Dock = DockStyle.Top, Height = 48, Padding = new Padding(16, 10, 0, 0), Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = Color.FromArgb(85, 53, 138) };
        var source = new GroupBox { Text = "Selecciona las carpetas que deseas respaldar", Dock = DockStyle.Top, Height = 130 };
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        AddFolder(source, "Escritorio", "Desktop", Path.Combine(user, "Desktop"), 20, 28);
        AddFolder(source, "Documentos", "Documents", Path.Combine(user, "Documents"), 20, 58);
        AddFolder(source, "Descargas", "Downloads", Path.Combine(user, "Downloads"), 340, 28);
        AddFolder(source, "Imágenes", "Pictures", Path.Combine(user, "Pictures"), 340, 58);

        var destBox = new GroupBox { Text = "Destino (USB, disco externo o carpeta de red)", Dock = DockStyle.Top, Height = 82 };
        _destination.ReadOnly = true; _destination.SetBounds(12, 27, 520, 26); _destination.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var browse = new Button { Text = "Examinar...", Width = 110, Location = new Point(545, 25), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        browse.Click += (_, _) => { using var d = new FolderBrowserDialog { Description = "Selecciona dónde guardar el respaldo" }; if (d.ShowDialog(this) == DialogResult.OK) _destination.Text = d.SelectedPath; };
        destBox.Controls.Add(_destination); destBox.Controls.Add(browse);

        _bar.Dock = DockStyle.Top; _bar.Height = 22;
        _status.Dock = DockStyle.Top; _status.Height = 28; _status.Padding = new Padding(8, 5, 0, 0); _status.Text = "Listo para iniciar.";
        _log.Dock = DockStyle.Fill; _log.Multiline = true; _log.ReadOnly = true; _log.ScrollBars = ScrollBars.Vertical; _log.Font = new Font("Consolas", 9F);
        _run.Text = "INICIAR BACKUP"; _run.Dock = DockStyle.Bottom; _run.Height = 42; _run.BackColor = Color.FromArgb(85, 53, 138); _run.ForeColor = Color.White; _run.FlatStyle = FlatStyle.Flat;
        _run.Click += async (_, _) => await BackupAsync();
        Controls.Add(_log); Controls.Add(_run); Controls.Add(_status); Controls.Add(_bar); Controls.Add(destBox); Controls.Add(source); Controls.Add(title);
    }

    private void AddFolder(Control parent, string label, string category, string path, int x, int y)
    {
        var box = new CheckBox { Text = label, AutoSize = true, Checked = Directory.Exists(path), Location = new Point(x, y) };
        _folders.Add((box, category, path)); parent.Controls.Add(box);
    }

    private async Task BackupAsync()
    {
        var selected = _folders.Where(x => x.Box.Checked && Directory.Exists(x.Path)).ToList();
        if (selected.Count == 0) { MessageBox.Show(this, "Selecciona al menos una carpeta existente."); return; }
        if (string.IsNullOrWhiteSpace(_destination.Text) || !Directory.Exists(_destination.Text)) { MessageBox.Show(this, "Selecciona primero el destino."); return; }
        string output = Path.Combine(_destination.Text, $"ONKOS_BACKUP_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}");
        _run.Enabled = false; _log.Clear(); _bar.Value = 0;
        try
        {
            Directory.CreateDirectory(output);
            var files = new List<(string File, string Relative, string Category)>();
            foreach (var f in selected)
            {
                try { foreach (var file in Directory.EnumerateFiles(f.Path, "*", SearchOption.AllDirectories)) files.Add((file, Path.GetRelativePath(f.Path, file), f.Category)); }
                catch (Exception e) { Log($"Aviso explorando {f.Category}: {e.Message}"); }
            }
            if (files.Count == 0) { Log("No se encontraron archivos."); _status.Text = "No hay archivos para copiar."; return; }
            _bar.Maximum = files.Count;
            int copied = 0, errors = 0;
            var progress = new Progress<(int done, string msg)>(p => { _bar.Value = Math.Min(p.done, _bar.Maximum); _status.Text = $"Progreso: {(int)(100.0*p.done/files.Count)}% ({p.done}/{files.Count})"; Log(p.msg); });
            await Task.Run(() =>
            {
                foreach (var f in files)
                {
                    try
                    {
                        string folder = Path.Combine(output, f.Category, Path.GetDirectoryName(f.Relative) ?? "");
                        Directory.CreateDirectory(folder);
                        File.Copy(f.File, Path.Combine(folder, Path.GetFileName(f.File)), true);
                        copied++;
                        ((IProgress<(int done, string msg)>)progress).Report((copied + errors, $"OK: {f.Category}\\{f.Relative}"));
                    }
                    catch (Exception ex) { errors++; ((IProgress<(int done, string msg)>)progress).Report((copied + errors, $"ERROR: {f.File} - {ex.Message}")); }
                }
            });
            Log($"\r\nFinalizado. Copiados: {copied}. Errores: {errors}.\r\nDestino: {output}");
            _status.Text = $"Terminado: {copied} copiados / {errors} errores.";
            MessageBox.Show(this, $"Backup finalizado.\r\nCopiados: {copied}\r\nErrores: {errors}\r\n\r\n{output}", "ONKOS Backup", MessageBoxButtons.OK, errors == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex) { Log("ERROR: " + ex.Message); MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { _run.Enabled = true; }
    }

    private void Log(string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => Log(text))); return; }
        _log.AppendText(text + Environment.NewLine);
    }
}