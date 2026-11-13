using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

/// <summary>
/// Ventanas emergentes unificadas de ONKOS IT Toolkit.
/// Azul = información, ámbar = advertencia, rojo = error, morado = confirmación.
/// </summary>
internal static class OnkosMessageBox
{
    private static readonly Color Purple = Color.FromArgb(105, 73, 164);
    private static readonly Color Text = Color.FromArgb(39, 35, 48);
    private static readonly Color Muted = Color.FromArgb(105, 99, 116);
    private static readonly Color Surface = Color.White;

    public static void ShowInfo(IWin32Window? owner, string title, string message, string? details = null)
        => Show(owner, DialogKind.Info, title, message, details);

    public static void ShowWarning(IWin32Window? owner, string title, string message, string? details = null)
        => Show(owner, DialogKind.Warning, title, message, details);

    public static void ShowError(IWin32Window? owner, string title, string message, string? details = null)
        => Show(owner, DialogKind.Error, title, message, details);

    public static bool Confirm(IWin32Window? owner, string title, string message, string confirmText = "Continuar", string cancelText = "Cancelar", bool dangerous = false)
        => Show(owner, dangerous ? DialogKind.Warning : DialogKind.Confirm, title, message, null, true, confirmText, cancelText) == DialogResult.Yes;

    public static DialogResult AskYesNoCancel(IWin32Window? owner, string title, string message,
        string yesText = "Sí", string noText = "No", string cancelText = "Cancelar")
        => Show(owner, DialogKind.Confirm, title, message, null, true, yesText, noText, cancelText);

    // Compatibilidad visual: reemplazo centralizado de MessageBox.Show para toda la aplicación.
    public static DialogResult Show(IWin32Window? owner, string message, string title,
        MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        DialogKind kind = icon switch
        {
            MessageBoxIcon.Warning or MessageBoxIcon.Exclamation => DialogKind.Warning,
            MessageBoxIcon.Error or MessageBoxIcon.Hand or MessageBoxIcon.Stop => DialogKind.Error,
            MessageBoxIcon.Question => DialogKind.Confirm,
            _ => DialogKind.Info
        };

        return buttons switch
        {
            MessageBoxButtons.YesNo => Show(owner, kind, title, message, null, true, "Sí", "No",
                null, DialogResult.Yes, DialogResult.No),
            MessageBoxButtons.YesNoCancel => Show(owner, kind, title, message, null, true, "Sí", "No",
                "Cancelar", DialogResult.Yes, DialogResult.No),
            MessageBoxButtons.OKCancel => Show(owner, kind, title, message, null, true, "Aceptar", "Cancelar",
                null, DialogResult.OK, DialogResult.Cancel),
            _ => Show(owner, kind, title, message)
        };
    }

    public static DialogResult Show(IWin32Window? owner, string message, string title)
        => Show(owner, DialogKind.Info, title, message);

    public static DialogResult Show(IWin32Window? owner, string message)
        => Show(owner, DialogKind.Info, "ONKOS", message);

    public static DialogResult Show(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        => Show(null, message, title, buttons, icon);

    public static DialogResult Show(string message, string title)
        => Show(null, DialogKind.Info, title, message);

    public static DialogResult Show(string message)
        => Show(null, DialogKind.Info, "ONKOS", message);

    private static DialogResult Show(IWin32Window? owner, DialogKind kind, string title, string message,
        string? details = null, bool confirm = false, string confirmText = "Aceptar", string cancelText = "Cancelar", string? thirdText = null, DialogResult primaryResult = DialogResult.Yes, DialogResult secondaryResult = DialogResult.No)
    {
        Color accent = kind switch
        {
            DialogKind.Info => Color.FromArgb(38, 133, 216),
            DialogKind.Warning => Color.FromArgb(205, 145, 25),
            DialogKind.Error => Color.FromArgb(205, 62, 62),
            _ => Purple
        };
        string label = kind switch
        {
            DialogKind.Info => "INFORMACIÓN",
            DialogKind.Warning => "ADVERTENCIA",
            DialogKind.Error => "ERROR",
            _ => "CONFIRMAR ACCIÓN"
        };
        string symbol = kind switch
        {
            DialogKind.Info => "i",
            DialogKind.Warning => "!",
            DialogKind.Error => "×",
            _ => "?"
        };

        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false,
            MinimizeBox = false,
            MaximizeBox = false,
            BackColor = Surface,
            Font = new Font("Segoe UI", 9.5F),
            ClientSize = new Size(510, details is null ? 232 : 300),
            AutoScaleMode = AutoScaleMode.Font
        };

        var top = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = accent };
        var icon = new Label
        {
            Text = symbol, ForeColor = accent, BackColor = Color.White,
            Font = new Font("Segoe UI", kind == DialogKind.Error ? 28F : 22F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter, Size = new Size(42, 42), Location = new Point(22, 12)
        };
        icon.Region = new Region(RoundedRect(new Rectangle(0, 0, icon.Width, icon.Height), 21));
        var category = new Label
        {
            Text = label, ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            AutoSize = true, Location = new Point(78, 22)
        };
        top.Controls.Add(icon);
        top.Controls.Add(category);

        var heading = new Label
        {
            Text = title, ForeColor = Text, Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            AutoSize = false, Location = new Point(26, 84), Size = new Size(458, 32)
        };
        var body = new Label
        {
            Text = message, ForeColor = Muted, Font = new Font("Segoe UI", 10F),
            AutoSize = false, Location = new Point(27, 122), Size = new Size(456, details is null ? 54 : 64)
        };
        form.Controls.Add(heading);
        form.Controls.Add(body);
        form.Controls.Add(top);

        if (!string.IsNullOrWhiteSpace(details))
        {
            var detailBox = new TextBox
            {
                Text = details, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(247, 246, 249),
                ForeColor = Muted, Font = new Font("Consolas", 8.5F),
                Location = new Point(27, 190), Size = new Size(456, 58), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            form.Controls.Add(detailBox);
        }

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = Color.FromArgb(248, 247, 250) };
        var primary = new Button
        {
            Text = confirmText, Size = new Size(125, 36), Location = new Point(354, 13),
            FlatStyle = FlatStyle.Flat, BackColor = accent, ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand,
            DialogResult = confirm ? primaryResult : DialogResult.OK
        };
        primary.FlatAppearance.BorderSize = 0;
        footer.Controls.Add(primary);

        if (confirm)
        {
            var secondary = new Button
            {
                Text = cancelText, Size = new Size(105, 36), Location = new Point(thirdText is null ? 240 : 250, 13),
                FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Text,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand,
                DialogResult = secondaryResult
            };
            secondary.FlatAppearance.BorderColor = Color.FromArgb(218, 214, 225);
            footer.Controls.Add(secondary);

            if (thirdText is not null)
            {
                primary.Location = new Point(374, 13);
                var cancel = new Button
                {
                    Text = thirdText, Size = new Size(105, 36), Location = new Point(126, 13),
                    FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Text,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand,
                    DialogResult = DialogResult.Cancel
                };
                cancel.FlatAppearance.BorderColor = Color.FromArgb(218, 214, 225);
                footer.Controls.Add(cancel);
                form.CancelButton = cancel;
            }
            else form.CancelButton = secondary;
        }
        else form.CancelButton = primary;

        form.AcceptButton = primary;
        form.Controls.Add(footer);

        return owner is null ? form.ShowDialog() : form.ShowDialog(owner);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private enum DialogKind { Info, Warning, Error, Confirm }
}
