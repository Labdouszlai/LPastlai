using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace lpastlai;

public class PastePopupForm : Form
{
    private readonly ListBox listBox;
    private readonly List<ClipItem> items;
    private readonly Action<ClipItem> onSelect;
    private const int TextItemHeight = 44;
    private const int ImageItemHeight = 80;
    private const int MaxPreviewLen = 60;
    private const int ThumbSize = 64;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUNDSMALL = 4;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public PastePopupForm(List<ClipItem> items, Action<ClipItem> onSelect)
    {
        this.items = items;
        this.onSelect = onSelect;

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(420, CalcHeight(items));
        MinimumSize = new Size(280, TextItemHeight);
        BackColor = Color.FromArgb(30, 30, 30);

        _ = Handle;

        listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9f),
            DrawMode = DrawMode.OwnerDrawVariable
        };

        if (items.Count == 0)
        {
            listBox.Items.Add("(empty)");
            listBox.Enabled = false;
        }
        else
        {
            foreach (var item in items)
                listBox.Items.Add(item);
        }

        listBox.MeasureItem += OnMeasureItem;
        listBox.DrawItem += OnDrawItem;
        listBox.DoubleClick += (_, _) => SelectCurrent();
        listBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { SelectCurrent(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) Close();
        };

        Controls.Add(listBox);

        Deactivate += (_, _) => Close();
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    private static int CalcHeight(List<ClipItem> items)
    {
        if (items.Count == 0) return TextItemHeight;
        int visible = Math.Min(items.Count, 10);
        int h = 0;
        for (int i = 0; i < visible; i++)
            h += items[i].IsImage ? ImageItemHeight : TextItemHeight;
        return h;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (Environment.OSVersion.Version.Build >= 22000)
        {
            int useDark = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            int corner = DWMWCP_ROUNDSMALL;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (listBox.Items.Count > 0)
            listBox.SelectedIndex = 0;
        listBox.Focus();

        var screen = Screen.FromPoint(Location).WorkingArea;
        int x = Math.Clamp(Location.X, screen.Left, screen.Right - Width);
        int y = Math.Clamp(Location.Y, screen.Top, screen.Bottom - Height);
        Location = new Point(x, y);

        FadeIn();
    }

    private async void FadeIn()
    {
        try
        {
            for (int i = 0; i < 8; i++)
            {
                Opacity = (i + 1) / 8.0;
                await Task.Delay(12);
            }
        }
        catch
        {
            Opacity = 1;
        }
    }

    private void OnMeasureItem(object? sender, MeasureItemEventArgs e)
    {
        if (items.Count == 0 || e.Index < 0 || e.Index >= items.Count)
        {
            e.ItemHeight = TextItemHeight;
            return;
        }

        e.ItemHeight = items[e.Index].IsImage ? ImageItemHeight : TextItemHeight;
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rect = e.Bounds;
        bool selected = (e.State & DrawItemState.Selected) != 0;

        using var bg = new SolidBrush(selected ? Color.FromArgb(70, 70, 75) : BackColor);
        g.FillRectangle(bg, rect);

        if (items.Count == 0)
            return;

        using var sepPen = new Pen(Color.FromArgb(42, 42, 42));
        g.DrawLine(sepPen, rect.Left, rect.Top, rect.Right, rect.Top);

        var item = items[e.Index];
        if (item.IsImage)
            DrawImageItem(g, rect, item, selected);
        else
            DrawTextItem(g, rect, item, selected);
    }

    private void DrawTextItem(Graphics g, Rectangle rect, ClipItem item, bool selected)
    {
        using var previewFont = new Font("Segoe UI", 9.5f);
        using var textBrush = new SolidBrush(selected ? Color.White : Color.FromArgb(220, 220, 220));
        string preview = BuildPreview(item.Text);
        var textRect = new Rectangle(rect.Left + 12, rect.Top + 6, rect.Width - 24, 20);
        g.DrawString(preview, previewFont, textBrush, textRect);

        using var timeFont = new Font("Segoe UI", 8f);
        using var timeBrush = new SolidBrush(Color.FromArgb(130, 130, 130));
        var timeRect = new Rectangle(rect.Left + 12, rect.Bottom - 18, rect.Width - 24, 14);
        g.DrawString(FormatTime(item.Time), timeFont, timeBrush, timeRect);
    }

    private void DrawImageItem(Graphics g, Rectangle rect, ClipItem item, bool selected)
    {
        int thumbX = rect.Left + 10;
        int thumbY = rect.Top + (rect.Height - ThumbSize) / 2;

        using var framePen = new Pen(Color.FromArgb(60, 60, 60));
        g.DrawRectangle(framePen, thumbX - 1, thumbY - 1, ThumbSize + 1, ThumbSize + 1);

        try
        {
            using var ms = new MemoryStream(item.ImageData!);
            using var img = Image.FromStream(ms);
            using var thumb = new Bitmap(img, ThumbSize, ThumbSize);
            g.DrawImage(thumb, thumbX, thumbY);
        }
        catch
        {
            using var font = new Font("Segoe UI", 7f);
            using var brush = new SolidBrush(Color.FromArgb(100, 100, 100));
            g.DrawString("preview", font, brush, thumbX + 8, thumbY + 24);
        }

        using var infoFont = new Font("Segoe UI", 9f);
        using var infoBrush = new SolidBrush(selected ? Color.White : Color.FromArgb(220, 220, 220));
        int labelX = thumbX + ThumbSize + 14;
        g.DrawString("Image", infoFont, infoBrush, new Rectangle(labelX, rect.Top + 10, rect.Right - labelX - 10, 18));

        using var dimFont = new Font("Segoe UI", 8f);
        using var dimBrush = new SolidBrush(Color.FromArgb(130, 130, 130));
        string dims = "";
        try
        {
            using var ms2 = new MemoryStream(item.ImageData!);
            using var img2 = Image.FromStream(ms2);
            dims = $"{img2.Width} × {img2.Height} px";
        }
        catch
        {
            dims = "";
        }
        g.DrawString(dims, dimFont, dimBrush, new Rectangle(labelX, rect.Top + 30, rect.Right - labelX - 10, 16));

        using var timeFont = new Font("Segoe UI", 8f);
        using var timeBrush = new SolidBrush(Color.FromArgb(130, 130, 130));
        g.DrawString(FormatTime(item.Time), timeFont, timeBrush,
                     new Rectangle(labelX, rect.Bottom - 18, rect.Right - labelX - 10, 14));
    }

    private static string FormatTime(DateTime dt)
    {
        var diff = DateTime.Now - dt;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dt.ToString("MMM dd");
    }

    private static string BuildPreview(string text)
    {
        string preview = text.Replace("\r", " ").Replace("\n", " ¶ ").Trim();
        return preview.Length > MaxPreviewLen ? preview[..MaxPreviewLen] + "…" : preview;
    }

    private void SelectCurrent()
    {
        int idx = listBox.SelectedIndex;
        if (idx >= 0 && idx < items.Count)
        {
            var item = items[idx];
            Close();
            onSelect(item);
        }
    }
}
