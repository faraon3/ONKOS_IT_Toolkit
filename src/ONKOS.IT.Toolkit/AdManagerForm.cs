using System;
using System.Drawing;
using System.Windows.Forms;

namespace ONKOS.IT.Toolkit;

internal sealed class AdManagerForm : UserControl
{
    public event Action? BackRequested;

    private readonly Label _connectionStatus = new()
    {
        Text = "Conexión pendiente",
        ForeColor = UiColors.TextMuted,
        AutoSize = true,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold)
    };

    public AdManagerForm()
    {
        Dock = DockStyle.Fill;
        BackColor = UiColors.Bg;
        ForeColor = UiColors.TextMain;
        Font = new Font("Segoe UI", 9.5F);

        var header = new ModuleHeader("ONKOS AD MANAGER", "Administración de usuarios, equipos y áreas de Clínica ONKOS");
        header.BackRequested += () => BackRequested?.Invoke();

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiColors.Bg,
            Padding = new Padding(18),
            AutoScroll = true
        };

        var connection = UiHelpers.MakeGroup("01  ·  CONEXIÓN CON ACTIVE DIRECTORY");
        connection.Dock = DockStyle.Top;
        connection.Height = 126;

        var statusLabel = new Label
        {
            Text = "Estado:",
            ForeColor = UiColors.TextMuted,
            AutoSize = true,
            Location = new Point(16, 31)
        };
        _connectionStatus.Location = new Point(72, 30);

        var info = new Label
        {
            Text = "El módulo consultará el dominio y las unidades organizativas existentes antes de permitir cambios.",
            ForeColor = UiColors.TextMuted,
            AutoSize = false,
            Location = new Point(16, 62),
            Size = new Size(850, 38)
        };
        connection.Controls.Add(statusLabel);
        connection.Controls.Add(_connectionStatus);
        connection.Controls.Add(info);

        var areas = UiHelpers.MakeGroup("02  ·  ÁREAS / UNIDADES ORGANIZATIVAS");
        areas.Dock = DockStyle.Top;
        areas.Height = 190;
        areas.Controls.Add(new Label
        {
            Text = "Aquí aparecerán las unidades organizativas reales del dominio.",
            ForeColor = UiColors.TextMuted,
            AutoSize = true,
            Location = new Point(16, 34)
        });

        var actions = UiHelpers.MakeGroup("03  ·  ADMINISTRACIÓN");
        actions.Dock = DockStyle.Top;
        actions.Height = 150;

        var users = new Label
        {
            Text = "USUARIOS",
            ForeColor = UiColors.TextMain,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(18, 34)
        };
        var computers = new Label
        {
            Text = "EQUIPOS",
            ForeColor = UiColors.TextMain,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(430, 34)
        };
        actions.Controls.Add(users);
        actions.Controls.Add(computers);

        content.Controls.Add(actions);
        content.Controls.Add(areas);
        content.Controls.Add(connection);

        Controls.Add(content);
        Controls.Add(header);
    }
}
