using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

/// <summary>
/// Tema centralizado de ONKOS IT Toolkit.
/// Reemplaza a UiColors y UiHelpers. Todos los formularios deben usar esta clase.
/// </summary>
internal static class OnkosTheme
{
    // ── Colores base ──────────────────────────────────────────────
    public static readonly Color Bg        = Color.FromArgb(18, 16, 25);
    public static readonly Color Surface   = Color.FromArgb(31, 27, 41);
    public static readonly Color Surface2  = Color.FromArgb(39, 34, 51);
    public static readonly Color Surface3  = Color.FromArgb(46, 40, 59);

    public static readonly Color Purple      = Color.FromArgb(105, 73, 164);
    public static readonly Color PurpleHover = Color.FromArgb(126, 91, 190);
    public static readonly Color PurpleLight = Color.FromArgb(142, 104, 205);

    public static readonly Color Accent      = Color.FromArgb(16, 185, 129);
    public static readonly Color AccentHover = Color.FromArgb(52, 211, 153);
    public static readonly Color Danger      = Color.FromArgb(239, 68, 68);
    public static readonly Color DangerHover = Color.FromArgb(248, 113, 113);
    public static readonly Color Warning     = Color.FromArgb(245, 158, 11);
    public static readonly Color Info        = Color.FromArgb(59, 130, 246);

    public static readonly Color TextMain  = Color.FromArgb(244, 241, 250);
    public static readonly Color TextMuted = Color.FromArgb(183, 174, 199);
    public static readonly Color TextDim   = Color.FromArgb(130, 122, 148);
    public static readonly Color Border    = Color.FromArgb(65, 57, 82);
    public static readonly Color LogBg     = Color.FromArgb(13, 12, 18);
    public static readonly Color LogFg     = Color.FromArgb(203, 195, 218);

    // ── Fuentes ───────────────────────────────────────────────────
    public const string FontFamily = "Segoe UI";

    public static Font H1        => new(FontFamily, 19F, FontStyle.Bold);
    public static Font H2        => new(FontFamily, 15F, FontStyle.Bold);
    public static Font H3        => new(FontFamily, 11.5F, FontStyle.Bold);
    public static Font Body      => new(FontFamily, 9.5F);
    public static Font BodySmall => new(FontFamily, 7.5F);
    public static Font Mono      => new("Consolas", 9F);
    public static Font Button    => new(FontFamily, 10.5F, FontStyle.Bold);

    // ── Estilos de botón ──────────────────────────────────────────
    public static void StyleButton(Button b, ButtonKind kind = ButtonKind.Secondary)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.Cursor = Cursors.Hand;
        b.Font = Button;
        b.UseVisualStyleBackColor = false;
        b.ForeColor = TextMain;

        switch (kind)
        {
            case ButtonKind.Primary:
                b.BackColor = Purple;
                b.FlatAppearance.MouseOverBackColor = PurpleHover;
                break;
            case ButtonKind.Success:
                b.BackColor = Accent;
                b.FlatAppearance.MouseOverBackColor = AccentHover;
                break;
            case ButtonKind.Danger:
                b.BackColor = Danger;
                b.FlatAppearance.MouseOverBackColor = DangerHover;
                break;
            case ButtonKind.Secondary:
            default:
                b.BackColor = Surface2;
                b.ForeColor = TextMain;
                b.FlatAppearance.BorderColor = Border;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 45, 67);
                break;
        }
    }

    // ── Estilos auxiliares ────────────────────────────────────────
    public static void StyleTextBox(TextBox t)
    {
        t.BackColor = Surface2;
        t.ForeColor = TextMain;
        t.BorderStyle = BorderStyle.FixedSingle;
        t.Font = Body;
    }

    public static void StyleCombo(ComboBox c)
    {
        c.BackColor = Surface2;
        c.ForeColor = TextMain;
        c.FlatStyle = FlatStyle.Flat;
        c.Font = Body;
    }

    public static void StyleGroup(GroupBox g)
    {
        g.ForeColor = Color.FromArgb(210, 197, 235);
        g.BackColor = Surface;
        g.Font = new Font(FontFamily, 9.5F, FontStyle.Bold);
        g.FlatStyle = FlatStyle.Flat;
        g.Padding = new Padding(10);
    }

    public static GroupBox MakeGroup(string title) => new()
    {
        Text = title,
        ForeColor = Color.FromArgb(210, 197, 235),
        BackColor = Surface,
        Padding = new Padding(10),
        Font = new Font(FontFamily, 9.5F, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 10),
        FlatStyle = FlatStyle.Flat
    };

    // ── Helpers de dibujo ─────────────────────────────────────────
    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Color Blend(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    // ── Header común con degradado ────────────────────────────────
    public static Panel CreateHeader(string title, string subtitle, Action onBack)
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Purple };
        header.Paint += (_, e) =>
        {
            using var brush = new LinearGradientBrush(header.ClientRectangle, Purple, PurpleLight, 0f);
            e.Graphics.FillRectangle(brush, header.ClientRectangle);
        };

        var back = new Button { Text = "‹  VOLVER", Location = new Point(22, 8), Size = new Size(120, 28) };
        StyleButton(back, ButtonKind.Secondary);
        back.BackColor = Color.FromArgb(40, 255, 255, 255);
        back.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 255, 255, 255);
        back.Click += (_, _) => onBack();
        header.Controls.Add(back);

        header.Controls.Add(new Label
        {
            Text = title,
            ForeColor = Color.White,
            Font = new Font(FontFamily, 20F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 40)
        });
        header.Controls.Add(new Label
        {
            Text = subtitle,
            ForeColor = Color.FromArgb(220, 210, 240),
            Font = new Font(FontFamily, 9.5F),
            AutoSize = true,
            Location = new Point(24, 74)
        });

        return header;
    }

    // ── Barra de estado inferior común ────────────────────────────
    public static Panel CreateStatusBar(string module)
    {
        var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Surface2, Padding = new Padding(12, 0, 12, 0) };
        statusBar.Controls.Add(new Label
        {
            Text = $"ONKOS IT Toolkit   ·   {module}   ·   USB: {Path.GetPathRoot(AppContext.BaseDirectory)?.TrimEnd('\\')}   ·   {Environment.MachineName}",
            ForeColor = TextMuted,
            Font = new Font(FontFamily, 8F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        });
        return statusBar;
    }
}

internal enum ButtonKind { Primary, Secondary, Success, Danger }
