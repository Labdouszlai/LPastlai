using System.Drawing.Text;

namespace lpastlai;

public class HiddenHostForm : Form
{
    private const int HOTKEY_ID = 0xCAFE;

    private readonly List<ClipItem> history;
    private NotifyIcon trayIcon = null!;
    private IntPtr lastForegroundWindow = IntPtr.Zero;
    private System.Windows.Forms.Timer? pasteTimer;
    private Icon? appIcon;
    private Bitmap? iconBitmap;

    public HiddenHostForm()
    {
        history = HistoryStore.Load();

        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-2000, -2000);
        Size = new Size(1, 1);
        Opacity = 0;

        SetupTrayIcon();

        _ = Handle;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        NativeMethods.AddClipboardFormatListener(Handle);

        NativeMethods.RegisterHotKey(
            Handle,
            HOTKEY_ID,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_V);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_CLIPBOARDUPDATE:
                OnClipboardChanged();
                break;

            case NativeMethods.WM_HOTKEY:
                if (m.WParam.ToInt32() == HOTKEY_ID)
                    ShowPastePopup();
                break;
        }

        base.WndProc(ref m);
    }

    private void OnClipboardChanged()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                    HistoryStore.Add(history, text);
            }
            else if (Clipboard.ContainsImage())
            {
                using var img = Clipboard.GetImage();
                if (img != null)
                    HistoryStore.AddImage(history, img);
            }
        }
        catch
        {
        }
    }

    private void ShowPastePopup()
    {
        lastForegroundWindow = NativeMethods.GetForegroundWindow();

        NativeMethods.GetCursorPos(out var pos);

        var popup = new PastePopupForm(history, OnItemChosen)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(pos.X, pos.Y)
        };
        popup.Show();
        popup.Activate();
    }

    private void OnItemChosen(ClipItem item)
    {
        try
        {
            if (item.IsImage && item.ImageData != null)
            {
                using var ms = new MemoryStream(item.ImageData);
                using var img = Image.FromStream(ms);
                Clipboard.SetImage(img);
            }
            else
            {
                Clipboard.SetText(item.Text);
            }
        }
        catch
        {
            return;
        }

        if (lastForegroundWindow != IntPtr.Zero)
            NativeMethods.SetForegroundWindow(lastForegroundWindow);

        pasteTimer?.Stop();
        pasteTimer = new System.Windows.Forms.Timer { Interval = 80 };
        pasteTimer.Tick += (s, e) =>
        {
            pasteTimer!.Stop();
            pasteTimer.Dispose();
            pasteTimer = null;
            SendCtrlV();
        };
        pasteTimer.Start();
    }

    private static void SendCtrlV()
    {
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_V, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_V, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private void SetupTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Renderer = new DarkMenuRenderer();

        var showItem = new ToolStripMenuItem("Paste by...  (Ctrl+Shift+V)");
        showItem.Click += (s, e) => ShowPastePopup();
        menu.Items.Add(showItem);

        menu.Items.Add(new ToolStripSeparator());

        var startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = StartupManager.IsEnabled(),
            CheckOnClick = true
        };
        startupItem.Click += (s, e) => StartupManager.SetEnabled(startupItem.Checked);
        menu.Items.Add(startupItem);

        var clearItem = new ToolStripMenuItem("Clear clipboard history");
        clearItem.Click += (s, e) => HistoryStore.Clear(history);
        menu.Items.Add(clearItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitApp();
        menu.Items.Add(exitItem);

        appIcon = CreateTrayIcon();

        trayIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = "lpastlai — Paste by (Ctrl+Shift+V)",
            Visible = true,
            ContextMenuStrip = menu
        };

        trayIcon.DoubleClick += (s, e) => ShowPastePopup();
    }

    private Icon CreateTrayIcon()
    {
        iconBitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(iconBitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.Transparent);

        using var bg = new SolidBrush(Color.FromArgb(0, 103, 192));
        g.FillEllipse(bg, 1, 1, 14, 14);

        using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var tb = new SolidBrush(Color.White);
        g.DrawString("p", font, tb, 3.5f, 3);

        return Icon.FromHandle(iconBitmap.GetHicon());
    }

    private void ExitApp()
    {
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID);
        NativeMethods.RemoveClipboardFormatListener(Handle);
        trayIcon.Visible = false;
        trayIcon.Icon = null;
        trayIcon.Dispose();
        appIcon?.Dispose();
        iconBitmap?.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID);
            NativeMethods.RemoveClipboardFormatListener(Handle);
            trayIcon?.Dispose();
            appIcon?.Dispose();
            iconBitmap?.Dispose();
            pasteTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    private class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(55, 55, 60)), e.Item.ContentRectangle);
            else
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 30)), e.Item.ContentRectangle);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(50, 50, 50));
            e.Graphics.DrawLine(pen, e.Item.ContentRectangle.Left + 4, e.Item.ContentRectangle.Height / 2,
                                      e.Item.ContentRectangle.Right - 4, e.Item.ContentRectangle.Height / 2);
        }
    }

    private class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(55, 55, 60);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(55, 55, 60);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(55, 55, 60);
        public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 30);
        public override Color MenuBorder => Color.FromArgb(55, 55, 55);
        public override Color MenuItemBorder => Color.FromArgb(55, 55, 55);
        public override Color SeparatorDark => Color.FromArgb(50, 50, 50);
        public override Color SeparatorLight => Color.FromArgb(50, 50, 50);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(40, 40, 45);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(40, 40, 45);
    }
}
