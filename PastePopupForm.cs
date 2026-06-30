using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace lpastlai;

public class PastePopupForm : Form
{
    private readonly List<ClipItem> allItems;
    private readonly Action<ClipItem> onSelect;
    private readonly ListBox textList;
    private readonly ListBox imgList;
    private readonly List<ClipItem> textItems = new();
    private readonly List<ClipItem> imgItems = new();

    private const int TextItemH = 44;
    private const int ImgItemH = 80;
    private const int MaxPreview = 60;
    private const int Thumb = 64;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUNDSMALL = 4;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public PastePopupForm(List<ClipItem> allItems, Action<ClipItem> onSelect)
    {
        this.allItems = allItems;
        this.onSelect = onSelect;

        foreach (var item in allItems)
        {
            if (item.IsImage)
                imgItems.Add(item);
            else
                textItems.Add(item);
        }

        int maxText = Math.Min(textItems.Count, 8);
        int maxImg = Math.Min(imgItems.Count, 8);
        int h = Math.Max(
            maxText > 0 ? maxText * TextItemH : TextItemH,
            maxImg > 0 ? maxImg * ImgItemH : ImgItemH
        ) + 28;

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(660, h);
        MinimumSize = new Size(400, 160);
        BackColor = Color.FromArgb(30, 30, 30);
        Icon = Program.LoadAppIcon();

        _ = Handle;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.FromArgb(30, 30, 30)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var textHeader = MakeHeader("Text");
        var imgHeader = MakeHeader("Images");
        layout.Controls.Add(textHeader, 0, 0);
        layout.Controls.Add(imgHeader, 1, 0);

        textList = MakeList(DrawMode.OwnerDrawVariable);
        imgList = MakeList(DrawMode.OwnerDrawVariable);

        FillList(textList, textItems, false);
        FillList(imgList, imgItems, true);

        textList.MeasureItem += (_, e) => e.ItemHeight = TextItemH;
        imgList.MeasureItem += (_, e) => e.ItemHeight = ImgItemH;
        textList.DrawItem += OnDrawTextItem;
        imgList.DrawItem += OnDrawImgItem;

        textList.DoubleClick += (_, _) => Pick(textList, textItems);
        imgList.DoubleClick += (_, _) => Pick(imgList, imgItems);

        textList.KeyDown += (_, e) => HandleKey(e, textList, textItems);
        imgList.KeyDown += (_, e) => HandleKey(e, imgList, imgItems);

        layout.Controls.Add(textList, 0, 1);
        layout.Controls.Add(imgList, 1, 1);

        Controls.Add(layout);

        Deactivate += (_, _) => Close();
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
            if (e.KeyCode == Keys.Tab)
            {
                if (textList.Focused)
                    imgList.Focus();
                else
                    textList.Focus();
                e.Handled = true;
            }
        };
    }

    private static Label MakeHeader(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 9f),
            ForeColor = Color.FromArgb(160, 160, 160),
            BackColor = Color.FromArgb(38, 38, 38),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
    }

    private static ListBox MakeList(DrawMode mode)
    {
        return new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9f),
            DrawMode = mode
        };
    }

    private static void FillList(ListBox list, List<ClipItem> source, bool isImage)
    {
        if (source.Count == 0)
        {
            list.Items.Add(isImage ? "(no images)" : "(no text)");
            list.Enabled = false;
        }
        else
        {
            foreach (var item in source)
                list.Items.Add(item);
        }
    }

    private void HandleKey(KeyEventArgs e, ListBox list, List<ClipItem> source)
    {
        if (e.KeyCode == Keys.Enter)
        {
            Pick(list, source);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    private void Pick(ListBox list, List<ClipItem> source)
    {
        int idx = list.SelectedIndex;
        if (idx >= 0 && idx < source.Count)
        {
            Close();
            onSelect(source[idx]);
        }
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
        if (textList.Items.Count > 0)
            textList.SelectedIndex = 0;
        textList.Focus();

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

    private void OnDrawTextItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var r = e.Bounds;
        bool sel = (e.State & DrawItemState.Selected) != 0;

        using var bg = new SolidBrush(sel ? Color.FromArgb(70, 70, 75) : BackColor);
        g.FillRectangle(bg, r);

        if (textItems.Count == 0) return;

        using var sep = new Pen(Color.FromArgb(42, 42, 42));
        g.DrawLine(sep, r.Left, r.Top, r.Right, r.Top);

        var item = textItems[e.Index];
        string preview = TextPreview(item.Text);

        using var pf = new Font("Segoe UI", 9.5f);
        using var tb = new SolidBrush(sel ? Color.White : Color.FromArgb(220, 220, 220));
        g.DrawString(preview, pf, tb, r.Left + 10, r.Top + 6);

        using var tf = new Font("Segoe UI", 8f);
        using var tim = new SolidBrush(Color.FromArgb(130, 130, 130));
        g.DrawString(TimeAgo(item.Time), tf, tim, r.Left + 10, r.Bottom - 18);
    }

    private void OnDrawImgItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var r = e.Bounds;
        bool sel = (e.State & DrawItemState.Selected) != 0;

        using var bg = new SolidBrush(sel ? Color.FromArgb(70, 70, 75) : BackColor);
        g.FillRectangle(bg, r);

        if (imgItems.Count == 0) return;

        using var sep = new Pen(Color.FromArgb(42, 42, 42));
        g.DrawLine(sep, r.Left, r.Top, r.Right, r.Top);

        var item = imgItems[e.Index];
        int tx = r.Left + 8;
        int ty = r.Top + (r.Height - Thumb) / 2;

        using var frame = new Pen(Color.FromArgb(60, 60, 60));
        g.DrawRectangle(frame, tx, ty, Thumb, Thumb);

        try
        {
            using var ms = new MemoryStream(item.ImageData!);
            using var img = Image.FromStream(ms);
            using var thumb = new Bitmap(img, Thumb, Thumb);
            g.DrawImage(thumb, tx, ty);
        }
        catch
        {
            using var f = new Font("Segoe UI", 7f);
            using var b = new SolidBrush(Color.FromArgb(100, 100, 100));
            g.DrawString("err", f, b, tx + 20, ty + 24);
        }

        int lx = tx + Thumb + 12;
        using var nf = new Font("Segoe UI", 9f);
        using var nb = new SolidBrush(sel ? Color.White : Color.FromArgb(220, 220, 220));
        g.DrawString("Image", nf, nb, lx, r.Top + 12);

        using var tf = new Font("Segoe UI", 8f);
        using var db = new SolidBrush(Color.FromArgb(130, 130, 130));
        string dims = "";
        try
        {
            using var ms = new MemoryStream(item.ImageData!);
            using var img = Image.FromStream(ms);
            dims = $"{img.Width}x{img.Height}";
        }
        catch { }
        g.DrawString(dims, tf, db, lx, r.Top + 32);

        using var tm = new SolidBrush(Color.FromArgb(130, 130, 130));
        g.DrawString(TimeAgo(item.Time), tf, tm, lx, r.Bottom - 18);
    }

    private static string TimeAgo(DateTime dt)
    {
        var d = DateTime.Now - dt;
        if (d.TotalMinutes < 1) return "now";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes}m";
        if (d.TotalDays < 1) return $"{(int)d.TotalHours}h";
        if (d.TotalDays < 7) return $"{(int)d.TotalDays}d";
        return dt.ToString("MMM dd");
    }

    private static string TextPreview(string text)
    {
        string s = text.Replace("\r", " ").Replace("\n", " \u00b6 ").Trim();
        return s.Length > MaxPreview ? s[..MaxPreview] + "\u2026" : s;
    }
}
