using System.Runtime.InteropServices;

namespace lpastlai;

public class SettingsForm : Form
{
    private readonly CheckBox chkCtrl, chkAlt, chkShift, chkWin;
    private readonly Label keyDisplay;
    private readonly AppSettings settings;
    private readonly NumericUpDown numText, numImg;
    private readonly Panel bgPanel, fgPanel;
    private bool capturing;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUNDSMALL = 4;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings current)
    {
        settings = new AppSettings
        {
            HotkeyModifiers = current.HotkeyModifiers,
            HotkeyKey = current.HotkeyKey,
            MaxTextItems = current.MaxTextItems,
            MaxImageItems = current.MaxImageItems,
            BgColorArgb = current.BgColorArgb,
            FgColorArgb = current.FgColorArgb,
        };
        Result = current;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(500, 300);
        BackColor = Color.FromArgb(30, 30, 30);
        Icon = Program.LoadAppIcon();
        _ = Handle;

        var title = new Label
        {
            Text = "Settings",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = Color.FromArgb(180, 180, 180),
            BackColor = Color.FromArgb(38, 38, 38),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0)
        };

        var btnPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            BackColor = Color.FromArgb(30, 30, 30),
            Padding = new Padding(0, 0, 12, 6)
        };

        var saveBtn = new Button
        {
            Text = "Save Settings",
            Font = new Font("Segoe UI", 9f),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 103, 192),
            ForeColor = Color.White,
            FlatAppearance = { BorderSize = 0 },
            Size = new Size(100, 28)
        };
        saveBtn.Click += (_, _) => SaveAndClose();

        var cancelBtn = new Button
        {
            Text = "Cancel",
            Font = new Font("Segoe UI", 9f),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 55),
            ForeColor = Color.FromArgb(180, 180, 180),
            FlatAppearance = { BorderSize = 0 },
            Size = new Size(80, 28)
        };
        cancelBtn.Click += (_, _) => Close();

        saveBtn.Location = new Point(btnPanel.Width - saveBtn.Width - cancelBtn.Width - 6, 2);
        cancelBtn.Location = new Point(btnPanel.Width - cancelBtn.Width, 2);
        btnPanel.Controls.Add(saveBtn);
        btnPanel.Controls.Add(cancelBtn);
        btnPanel.Resize += (_, _) =>
        {
            saveBtn.Location = new Point(btnPanel.Width - saveBtn.Width - cancelBtn.Width - 6, 2);
            cancelBtn.Location = new Point(btnPanel.Width - cancelBtn.Width, 2);
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            BackColor = Color.FromArgb(30, 30, 30),
            Padding = new Padding(14, 6, 14, 0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        AddHeader(table, "HOTKEY", 0);
        AddHeader(table, "DISPLAY", 3);
        AddHeader(table, "THEME", 6);

        chkCtrl = MakeCheck("Ctrl");
        chkAlt = MakeCheck("Alt");
        chkShift = MakeCheck("Shift");
        chkWin = MakeCheck("Win");
        var mods = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        mods.Controls.AddRange(new Control[] { chkCtrl, chkAlt, chkShift, chkWin });
        table.Controls.Add(mods, 0, 1);
        table.SetColumnSpan(mods, 2);

        table.Controls.Add(MakeLabel("Key:"), 0, 2);
        keyDisplay = new Label
        {
            Text = NativeMethods.KeyName(settings.HotkeyKey),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 50, 55),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Cursor = Cursors.Hand
        };
        keyDisplay.Click += (_, _) => StartCapture();
        table.Controls.Add(keyDisplay, 1, 2);

        numText = AddNudRow(table, "Max text items:", settings.MaxTextItems, 4);
        numImg = AddNudRow(table, "Max image items:", settings.MaxImageItems, 5);

        table.Controls.Add(MakeLabel("Background:"), 0, 7);
        bgPanel = MakeColorPanel(settings.BgColor, OnBgClick);
        table.Controls.Add(bgPanel, 1, 7);

        table.Controls.Add(MakeLabel("Font color:"), 0, 8);
        fgPanel = MakeColorPanel(settings.FgColor, OnFgClick);
        table.Controls.Add(fgPanel, 1, 8);

        Controls.Add(table);
        Controls.Add(btnPanel);
        Controls.Add(title);

        KeyPreview = true;
        KeyDown += OnKeyDown;
        SyncChecks();
    }

    private static void AddHeader(TableLayoutPanel table, string text, int row)
    {
        table.Controls.Add(new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 9f),
            ForeColor = Color.FromArgb(0, 130, 220),
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
        table.SetColumnSpan(table.GetControlFromPosition(0, row), 2);
    }

    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(180, 180, 180),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static NumericUpDown AddNudRow(TableLayoutPanel table, string text, int value, int row)
    {
        table.Controls.Add(MakeLabel(text), 0, row);
        var nud = new NumericUpDown
        {
            Value = value,
            Minimum = 1,
            Maximum = 50,
            Width = 70,
            BackColor = Color.FromArgb(50, 50, 55),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 2, 0, 2)
        };
        table.Controls.Add(nud, 1, row);
        return nud;
    }

    private static Panel MakeColorPanel(Color c, EventHandler onClick)
    {
        var p = new Panel
        {
            Width = 80,
            Height = 20,
            BackColor = c,
            Cursor = Cursors.Hand,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 3, 0, 3)
        };
        p.Click += onClick;
        return p;
    }

    private void OnBgClick(object? sender, EventArgs e)
    {
        using var dlg = new ColorDialog { Color = settings.BgColor, AnyColor = true, FullOpen = true };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            settings.BgColor = dlg.Color;
            bgPanel.BackColor = dlg.Color;
        }
    }

    private void OnFgClick(object? sender, EventArgs e)
    {
        using var dlg = new ColorDialog { Color = settings.FgColor, AnyColor = true, FullOpen = true };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            settings.FgColor = dlg.Color;
            fgPanel.BackColor = dlg.Color;
        }
    }

    private CheckBox MakeCheck(string text)
    {
        return new CheckBox
        {
            Text = text,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 8, 0),
            AutoSize = true
        };
    }

    private void StartCapture()
    {
        capturing = true;
        keyDisplay.Text = "... press a key";
        keyDisplay.ForeColor = Color.FromArgb(0, 180, 255);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!capturing) return;
        if (e.KeyCode == Keys.Escape)
        {
            capturing = false;
            keyDisplay.Text = NativeMethods.KeyName(settings.HotkeyKey);
            keyDisplay.ForeColor = Color.White;
            return;
        }
        uint vk = (uint)e.KeyCode;
        settings.HotkeyKey = vk;
        keyDisplay.Text = NativeMethods.KeyName(vk);
        keyDisplay.ForeColor = Color.White;
        capturing = false;
    }

    private void SyncChecks()
    {
        chkCtrl.Checked = (settings.HotkeyModifiers & NativeMethods.MOD_CONTROL) != 0;
        chkAlt.Checked = (settings.HotkeyModifiers & NativeMethods.MOD_ALT) != 0;
        chkShift.Checked = (settings.HotkeyModifiers & NativeMethods.MOD_SHIFT) != 0;
        chkWin.Checked = (settings.HotkeyModifiers & NativeMethods.MOD_WIN) != 0;
    }

    private void SaveAndClose()
    {
        uint mods = 0;
        if (chkCtrl.Checked) mods |= NativeMethods.MOD_CONTROL;
        if (chkAlt.Checked) mods |= NativeMethods.MOD_ALT;
        if (chkShift.Checked) mods |= NativeMethods.MOD_SHIFT;
        if (chkWin.Checked) mods |= NativeMethods.MOD_WIN;
        if (mods == 0)
        {
            keyDisplay.Text = "pick at least one modifier";
            return;
        }
        settings.HotkeyModifiers = mods | NativeMethods.MOD_NOREPEAT;
        settings.MaxTextItems = (int)numText.Value;
        settings.MaxImageItems = (int)numImg.Value;
        settings.Save();
        Result = settings;
        Close();
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
}
