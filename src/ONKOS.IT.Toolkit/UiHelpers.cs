using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal static class UiColors
{
    public static readonly Color Bg = Color.FromArgb(18, 16, 25);
    public static readonly Color Surface = Color.FromArgb(31, 27, 41);
    public static readonly Color Surface2 = Color.FromArgb(39, 34, 51);
    public static readonly Color Purple = Color.FromArgb(105, 73, 164);
    public static readonly Color PurpleHover = Color.FromArgb(126, 91, 190);
    public static readonly Color TextMain = Color.FromArgb(244, 241, 250);
    public static readonly Color TextMuted = Color.FromArgb(183, 174, 199);
    public static readonly Color Border = Color.FromArgb(65, 57, 82);
    public static readonly Color Accent = Color.FromArgb(153, 117, 214);
}

// Header reutilizable para todos los módulos.
internal sealed class ModuleHeader : Panel
{
    public event Action? BackRequested;

    public ModuleHeader(string title, string? subtitle = null)
    {
        Dock = DockStyle.Top;
        Height = string.IsNullOrWhiteSpace(subtitle) ? 88 : 104;
        BackColor = UiColors.Purple;

        var back = new Button
        {
            Text = "‹  VOLVER AL INICIO",
            Location = new Point(22, 5),
            Size = new Size(170, 25)
        };
        UiHelpers.StyleButton(back, secondary: true);
        back.Click += (_, _) => BackRequested?.Invoke();
        Controls.Add(back);

        Controls.Add(new Label
        {
            Text = title,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 19F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 30)
        });

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            Controls.Add(new Label
            {
                Text = subtitle,
                ForeColor = Color.FromArgb(235, 225, 250),
                Font = new Font("Segoe UI", 9.5F),
                AutoSize = true,
                Location = new Point(24, 66)
            });
        }
    }
}

internal static class UiHelpers
{
    public static void StyleButton(Button b, bool secondary)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = UiColors.Border;
        b.FlatAppearance.BorderSize = secondary ? 1 : 0;
        b.BackColor = secondary ? UiColors.Surface2 : UiColors.Purple;
        b.ForeColor = UiColors.TextMain;
        b.Cursor = Cursors.Hand;
        b.FlatAppearance.MouseOverBackColor = secondary ? Color.FromArgb(52, 45, 67) : UiColors.PurpleHover;
    }

    public static void StyleTextBox(TextBox t)
    {
        t.BackColor = UiColors.Surface2;
        t.ForeColor = UiColors.TextMain;
        t.BorderStyle = BorderStyle.FixedSingle;
        t.Font = new Font("Segoe UI", 10F);
    }

    public static void StyleCombo(ComboBox c)
    {
        c.BackColor = UiColors.Surface2;
        c.ForeColor = UiColors.TextMain;
        c.FlatStyle = FlatStyle.Flat;
    }

    public static void StyleCheck(CheckBox c)
    {
        c.ForeColor = UiColors.TextMain;
        c.BackColor = UiColors.Surface;
        c.Padding = new Padding(4);
        c.Cursor = Cursors.Hand;
    }

    public static GroupBox MakeGroup(string title) => new()
    {
        Text = title,
        ForeColor = Color.FromArgb(210, 197, 235),
        BackColor = UiColors.Surface,
        Padding = new Padding(10),
        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 10),
        FlatStyle = FlatStyle.Flat
    };

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
}

// Tooltips globales.
internal static class TooltipHelper
{
    private static readonly ToolTip _tip = new()
    {
        AutoPopDelay = 6000,
        InitialDelay = 500,
        ReshowDelay = 100,
        ShowAlways = true,
        BackColor = UiColors.Surface2,
        ForeColor = UiColors.TextMain
    };

    public static void Set(Control control, string text)
    {
        if (control == null) return;
        try { _tip.SetToolTip(control, text); } catch { }
    }
}
