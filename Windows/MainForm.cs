using System.Drawing.Drawing2D;
using System.Media;
using System.Runtime.InteropServices;

namespace MyWhoosh2Garmin;

internal sealed class MainForm : Form
{
    private static readonly Color WindowBackground = Color.FromArgb(36, 36, 38);
    private static readonly Color Secondary = Color.FromArgb(183, 183, 190);
    private static readonly Color Accent = Color.FromArgb(100, 170, 255);
    private readonly Panel dropArea = new()
    {
        Location = new Point(20, 20),
        Size = new Size(340, 140),
    };
    private readonly Label prompt = new()
    {
        Text = "Drop a MyWhoosh .fit file",
        Bounds = new Rectangle(2, 63, 336, 22),
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Secondary,
    };
    private readonly LinkLabel choose = new()
    {
        Text = "or choose…",
        Bounds = new Rectangle(123, 87, 94, 23),
        TextAlign = ContentAlignment.MiddleCenter,
        LinkColor = Accent,
        ActiveLinkColor = Color.FromArgb(156, 202, 255),
    };
    private bool hovering;
    private bool busy;

    public MainForm(string[] initialFiles)
    {
        Text = "Whoosh → Garmin";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(380, 180);
        BackColor = WindowBackground;
        Font = new Font("Segoe UI", 9.75f);
        prompt.Font = new Font(Font, FontStyle.Bold);

        Controls.Add(dropArea);
        dropArea.Controls.Add(prompt);
        dropArea.Controls.Add(choose);
        dropArea.Paint += PaintDropArea;

        choose.LinkClicked += async (_, _) => await ChooseFilesAsync();
        WireDropTarget(this);
        Shown += async (_, _) =>
        {
            ActiveControl = null;
            await ConvertFilesAsync(initialFiles);
        };
        FormClosing += (_, e) => e.Cancel = busy && e.CloseReason == CloseReason.UserClosing;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        const int immersiveDarkMode = 20;
        const int captionColor = 35;
        const int textColor = 36;
        int enabled = 1;
        int caption = ColorTranslator.ToWin32(WindowBackground);
        int text = ColorTranslator.ToWin32(Color.Gainsboro);
        DwmSetWindowAttribute(Handle, immersiveDarkMode, in enabled, sizeof(int));
        DwmSetWindowAttribute(Handle, captionColor, in caption, sizeof(int));
        DwmSetWindowAttribute(Handle, textColor, in text, sizeof(int));
    }

    private void PaintDropArea(object? sender, PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = dropArea.DeviceDpi / 96f;
        graphics.ScaleTransform(scale, scale);
        float width = dropArea.ClientSize.Width / scale;
        float height = dropArea.ClientSize.Height / scale;

        using var border = new Pen(hovering ? Accent : Color.FromArgb(103, 103, 112), 1.5f)
        {
            DashPattern = [4f, 4f],
        };
        using var frame = new GraphicsPath();
        frame.AddRoundedRectangle(new RectangleF(0.75f, 0.75f, width - 1.5f, height - 1.5f), new SizeF(12, 12));
        graphics.DrawPath(border, frame);

        graphics.TranslateTransform(width / 2 - 12, 28);
        using var icon = new Pen(hovering ? Accent : Secondary, 1.45f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        using var page = new GraphicsPath();
        page.AddPolygon([new PointF(3, 1), new PointF(15, 1), new PointF(21, 7), new PointF(21, 28), new PointF(3, 28)]);
        graphics.DrawPath(icon, page);
        graphics.DrawLines(icon, [new Point(15, 1), new Point(15, 7), new Point(21, 7)]);
        graphics.DrawLine(icon, 12, 10, 12, 22);
        graphics.DrawLines(icon, [new Point(8, 18), new Point(12, 22), new Point(16, 18)]);
    }

    private void WireDropTarget(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += (_, e) =>
        {
            bool canDrop = !busy && e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
            e.Effect = canDrop ? e.AllowedEffect & DragDropEffects.Copy : DragDropEffects.None;
            SetHovering(e.Effect == DragDropEffects.Copy);
        };
        control.DragLeave += (_, _) => SetHovering(false);
        control.DragDrop += async (_, e) =>
        {
            SetHovering(false);
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
                await ConvertFilesAsync(paths);
        };
        foreach (Control child in control.Controls)
            WireDropTarget(child);
    }

    private void SetHovering(bool value)
    {
        hovering = value;
        prompt.ForeColor = value ? Accent : Secondary;
        dropArea.Invalidate();
    }

    private async Task ChooseFilesAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose MyWhoosh FIT files",
            Filter = "FIT files (*.fit)|*.fit",
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ConvertFilesAsync(dialog.FileNames);
    }

    private async Task ConvertFilesAsync(string[] paths)
    {
        if (busy || paths.Length == 0)
            return;

        busy = true;
        choose.Enabled = false;
        try
        {
            bool succeeded = await Task.Run(() =>
            {
                bool allSucceeded = true;
                foreach (string path in paths)
                {
                    if (!string.Equals(Path.GetExtension(path), ".fit", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        ActivityConverter.Convert(path);
                    }
                    catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                    {
                        allSucceeded = false;
                    }
                }
                return allSucceeded;
            });
            if (!succeeded)
                SystemSounds.Beep.Play();
        }
        finally
        {
            busy = false;
            choose.Enabled = true;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, in int value, int size);
}
