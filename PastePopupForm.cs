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
    private readonly AppSettings settings;
    private readonly bool favoritesOnly;

    private const int TextItemH = 44;
    private const int ImgItemH = 80;
    private const int MaxPreview = 60;
    private const int Thumb = 64;
    private const int StarSize = 18;
    private const int XSize = 16;
    private const int XRightMargin = 6;
    private const int XStarGap = 4;
    private const int TextRightMargin = 8;
    private const int ClearBtnH = 36;
    private int hoveredImgIndex = -1;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUNDSMALL = 4;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public PastePopupForm(List<ClipItem> allItems, Action<ClipItem> onSelect, AppSettings settings, bool favoritesOnly = false)
    {
        this.allItems = allItems;
        this.onSelect = onSelect;
        this.settings = settings;
        this.favoritesOnly = favoritesOnly;

        foreach (var item in allItems)
        {
            if (favoritesOnly && !item.IsFavorited)
                continue;
            if (item.IsImage)
                imgItems.Add(item);
            else
                textItems.Add(item);
        }

        int maxText = Math.Min(textItems.Count, settings.MaxTextItems);
        int maxImg = Math.Min(imgItems.Count, settings.MaxImageItems);
        int h = Math.Max(
            maxText > 0 ? maxText * TextItemH : TextItemH,
            maxImg > 0 ? maxImg * ImgItemH : ImgItemH
        ) + 28 + ClearBtnH;

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(660, h);
        MinimumSize = new Size(400, 160);
        BackColor = settings.BgColor;
        Icon = Program.LoadAppIcon();

        _ = Handle;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = settings.BgColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ClearBtnH));

        var textHeader = MakeHeader("Text");
        var imgHeader = MakeHeader("Images");
        layout.Controls.Add(textHeader, 0, 0);
        layout.Controls.Add(imgHeader, 1, 0);

        textList = MakeList();
        imgList = MakeList();

        FillList(textList, textItems, false);
        FillList(imgList, imgItems, true);

        textList.MeasureItem += (_, e) => e.ItemHeight = TextItemH;
        imgList.MeasureItem += (_, e) => e.ItemHeight = ImgItemH;
        textList.DrawItem += OnDrawTextItem;
        imgList.DrawItem += OnDrawImgItem;

        textList.MouseClick += (_, e) => HandleItemClick(e, textList, textItems);
        imgList.MouseClick += (_, e) => HandleItemClick(e, imgList, imgItems);
        imgList.MouseMove += (_, e) =>
        {
            int idx = imgList.IndexFromPoint(e.Location);
            if (idx != hoveredImgIndex)
            {
                hoveredImgIndex = idx;
                imgList.Invalidate();
            }
        };
        imgList.MouseLeave += (_, _) =>
        {
            hoveredImgIndex = -1;
            imgList.Invalidate();
        };
        textList.MouseLeave += (_, _) => hoveredImgIndex = -1;

        textList.DoubleClick += (_, _) => Pick(textList, textItems);
        imgList.DoubleClick += (_, _) => Pick(imgList, imgItems);

        textList.KeyDown += (_, e) => HandleKey(e, textList, textItems);
        imgList.KeyDown += (_, e) => HandleKey(e, imgList, imgItems);

        layout.Controls.Add(textList, 0, 1);
        layout.Controls.Add(imgList, 1, 1);

        var clearAllBtn = new Button
        {
            Text = "Clear All",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(200, 50, 50),
            ForeColor = Color.White,
            FlatAppearance = { BorderSize = 0 },
            Font = new Font("Segoe UI Semibold", 9f),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 2, 4, 4)
        };
        clearAllBtn.Click += (_, _) =>
        {
            HistoryStore.Clear(allItems);
            Close();
        };
        layout.Controls.Add(clearAllBtn, 0, 2);
        layout.SetColumnSpan(clearAllBtn, 2);

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

    private Label MakeHeader(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 9f),
            ForeColor = Shift(settings.FgColor, 0.6f),
            BackColor = Shift(settings.BgColor, 0.08f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
    }

    private ListBox MakeList()
    {
        return new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            BackColor = settings.BgColor,
            ForeColor = settings.FgColor,
            Font = new Font("Segoe UI", 9f),
            DrawMode = DrawMode.OwnerDrawVariable
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

    private void HandleItemClick(MouseEventArgs e, ListBox list, List<ClipItem> source)
    {
        int idx = list.IndexFromPoint(e.Location);
        if (idx < 0 || idx >= source.Count) return;

        var item = source[idx];

        int xLeft = list.ClientSize.Width - XRightMargin - XSize;
        int starLeft = xLeft - XStarGap - StarSize;

        if (e.X >= xLeft)
        {
            DeleteItem(item, list, source, idx);
        }
        else if (e.X >= starLeft)
        {
            ToggleFavorite(item);

            if (favoritesOnly)
            {
                source.RemoveAt(idx);
                list.Items.RemoveAt(idx);
                if (source.Count == 0)
                {
                    list.Items.Add("(no favorites)");
                    list.Enabled = false;
                }
                list.Invalidate();
            }
            else
            {
                list.Invalidate(list.GetItemRectangle(idx));
            }
        }
    }

    private void ToggleFavorite(ClipItem item)
    {
        var original = allItems.Find(i => ReferenceEquals(i, item));
        if (original != null)
            original.IsFavorited = !original.IsFavorited;
        HistoryStore.Save(allItems);
    }

    private void DeleteItem(ClipItem item, ListBox list, List<ClipItem> source, int idx)
    {
        var original = allItems.Find(i => ReferenceEquals(i, item));
        if (original != null)
            allItems.Remove(original);
        HistoryStore.Save(allItems);
        source.RemoveAt(idx);
        list.Items.RemoveAt(idx);
        if (source.Count == 0)
        {
            string placeholder = source == imgItems ? "(no images)" : "(no text)";
            list.Items.Add(placeholder);
            list.Enabled = false;
        }
        list.Invalidate();
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
        int x = Math.Max(screen.Left, Math.Min(Location.X, screen.Right - Width));
        int y = Math.Max(screen.Top, Math.Min(Location.Y, screen.Bottom - Height));
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

        using var bg = new SolidBrush(sel ? Shift(settings.BgColor, 0.15f) : settings.BgColor);
        g.FillRectangle(bg, r);

        if (textItems.Count == 0) return;

        using var sep = new Pen(Shift(settings.BgColor, 0.08f));
        g.DrawLine(sep, r.Left, r.Top, r.Right, r.Top);

        var item = textItems[e.Index];

        int xLeft = r.Right - XRightMargin - XSize;
        int starLeft = xLeft - XStarGap - StarSize;
        int centerY = r.Top + (r.Height - StarSize) / 2;

        using var sf = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var sb = new SolidBrush(item.IsFavorited ? Color.FromArgb(255, 200, 50) : Shift(settings.FgColor, 0.3f));
        g.DrawString(item.IsFavorited ? "â˜…" : "â˜†", sf, sb, starLeft, centerY);

        using var xf = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var xb = new SolidBrush(Shift(settings.FgColor, 0.4f));
        g.DrawString("Ã—", xf, xb, xLeft, centerY - 1);

        int textRight = starLeft - TextRightMargin;
        var textR = new Rectangle(r.Left + 10, r.Top + 6, textRight - r.Left - 10, r.Height - 24);
        string preview = TextPreview(item.Text);

        using var pf = new Font("Segoe UI", 9.5f);
        using var tb = new SolidBrush(sel ? settings.FgColor : settings.FgColor);
        g.DrawString(preview, pf, tb, textR);

        using var tf = new Font("Segoe UI", 8f);
        using var tim = new SolidBrush(Shift(settings.FgColor, 0.4f));
        g.DrawString(TimeAgo(item.Time), tf, tim, r.Left + 10, r.Bottom - 18);
    }

    private void OnDrawImgItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var r = e.Bounds;
        bool sel = (e.State & DrawItemState.Selected) != 0;

        using var bg = new SolidBrush(sel ? Shift(settings.BgColor, 0.15f) : settings.BgColor);
        g.FillRectangle(bg, r);

        if (imgItems.Count == 0) return;

        using var sep = new Pen(Shift(settings.BgColor, 0.08f));
        g.DrawLine(sep, r.Left, r.Top, r.Right, r.Top);

        var item = imgItems[e.Index];
        int tx = r.Left + 8;
        int ty = r.Top + (r.Height - Thumb) / 2;

        using var frame = new Pen(Shift(settings.BgColor, 0.12f));
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
            using var b = new SolidBrush(Shift(settings.FgColor, 0.5f));
            g.DrawString("err", f, b, tx + 20, ty + 24);
        }

        int xLeft = r.Right - XRightMargin - XSize;
        int starLeft = xLeft - XStarGap - StarSize;
        int centerY = r.Top + (r.Height - StarSize) / 2;

        using var sf = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var sb = new SolidBrush(item.IsFavorited ? Color.FromArgb(255, 200, 50) : Shift(settings.FgColor, 0.3f));
        g.DrawString(item.IsFavorited ? "â˜…" : "â˜†", sf, sb, starLeft, centerY);

        if (hoveredImgIndex == e.Index)
        {
            using var xf = new Font("Segoe UI", 10f, FontStyle.Bold);
            using var xb = new SolidBrush(Shift(settings.FgColor, 0.4f));
            g.DrawString("Ã—", xf, xb, xLeft, centerY - 1);
        }

        int lx = tx + Thumb + 12;
        using var nf = new Font("Segoe UI", 9f);
        using var nb = new SolidBrush(sel ? settings.FgColor : settings.FgColor);
        g.DrawString("Image", nf, nb, lx, r.Top + 12);

        using var tf = new Font("Segoe UI", 8f);
        using var db = new SolidBrush(Shift(settings.FgColor, 0.4f));
        string dims = "";
        try
        {
            using var ms = new MemoryStream(item.ImageData!);
            using var img = Image.FromStream(ms);
            dims = $"{img.Width}x{img.Height}";
        }
        catch { }
        g.DrawString(dims, tf, db, lx, r.Top + 32);

        using var tm = new SolidBrush(Shift(settings.FgColor, 0.4f));
        g.DrawString(TimeAgo(item.Time), tf, tm, lx, r.Bottom - 18);
    }

    private static Color Shift(Color c, float factor)
    {
        float lum = (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255f;
        if (lum > 0.5f)
        {
            int r = Math.Max(0, (int)(c.R * (1 - factor)));
            int g = Math.Max(0, (int)(c.G * (1 - factor)));
            int b = Math.Max(0, (int)(c.B * (1 - factor)));
            return Color.FromArgb(r, g, b);
        }
        else
        {
            int r = Math.Min(255, (int)(c.R + (255 - c.R) * factor));
            int g = Math.Min(255, (int)(c.G + (255 - c.G) * factor));
            int b = Math.Min(255, (int)(c.B + (255 - c.B) * factor));
            return Color.FromArgb(r, g, b);
        }
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
