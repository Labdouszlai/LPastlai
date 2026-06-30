using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace lpastlai;

public class SettingsForm : Form
{
    private readonly CheckBox chkCtrl, chkAlt, chkShift, chkWin;
    private readonly Label keyDisplay;
    private readonly HotkeySettings settings;
    private bool capturing;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUNDSMALL = 4;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public HotkeySettings Result { get; private set; }

    public SettingsForm(HotkeySettings current)
    {
        settings = new HotkeySettings
        {
            Modifiers = current.Modifiers,
            Key = current.Key
        };
        Result = current;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(320, 240);
        BackColor = Color.FromArgb(30, 30, 30);
        Icon = Program.LoadAppIcon();
        _ = Handle;

        var title = new Label
        {
            Text = "Hotkey Settings",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = Color.FromArgb(180, 180, 180),
            BackColor = Color.FromArgb(38, 38, 38),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0)
        };

        var wrap = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(14, 12, 14, 12),
            BackColor = Color.FromArgb(30, 30, 30)
        };

        var modLabel = new Label
        {
            Text = "Modifiers:",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(180, 180, 180),
            Size = new Size(280, 18)
        };

        chkCtrl = MakeCheck("Ctrl");
        chkAlt = MakeCheck("Alt");
        chkShift = MakeCheck("Shift");
        chkWin = MakeCheck("Win");

        var mods = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Size = new Size(280, 24),
            BackColor = Color.Transparent
        };
        mods.Controls.AddRange(new Control[] { chkCtrl, chkAlt, chkShift, chkWin });

        var keyLabel = new Label
        {
            Text = "Key:",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(180, 180, 180),
            Size = new Size(280, 18),
            Margin = new Padding(0, 10, 0, 0)
        };

        keyDisplay = new Label
        {
            Text = NativeMethods.KeyName(settings.Key),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 50, 55),
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(120, 32),
            Cursor = Cursors.Hand
        };
        keyDisplay.Click += (_, _) => StartCapture();

        var btnRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Size = new Size(280, 32),
            BackColor = Color.Transparent,
            Margin = new Padding(0, 14, 0, 0)
        };

        var saveBtn = new Button
        {
            Text = "Save",
            Font = new Font("Segoe UI", 9f),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 103, 192),
            ForeColor = Color.White,
            FlatAppearance = { BorderSize = 0 },
            Size = new Size(80, 30)
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
            Size = new Size(80, 30),
            Margin = new Padding(0, 0, 8, 0)
        };
        cancelBtn.Click += (_, _) => Close();

        btnRow.Controls.Add(saveBtn);
        btnRow.Controls.Add(cancelBtn);

        wrap.Controls.Add(modLabel);
        wrap.Controls.Add(mods);
        wrap.Controls.Add(keyLabel);
        wrap.Controls.Add(keyDisplay);
        wrap.Controls.Add(btnRow);

        Controls.Add(wrap);
        Controls.Add(title);

        KeyPreview = true;
        KeyDown += OnKeyDown;

        SyncChecks();
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
            keyDisplay.Text = NativeMethods.KeyName(settings.Key);
            keyDisplay.ForeColor = Color.White;
            return;
        }

        uint vk = (uint)e.KeyCode;
        settings.Key = vk;
        keyDisplay.Text = NativeMethods.KeyName(vk);
        keyDisplay.ForeColor = Color.White;
        capturing = false;
    }

    private void SyncChecks()
    {
        chkCtrl.Checked = (settings.Modifiers & NativeMethods.MOD_CONTROL) != 0;
        chkAlt.Checked = (settings.Modifiers & NativeMethods.MOD_ALT) != 0;
        chkShift.Checked = (settings.Modifiers & NativeMethods.MOD_SHIFT) != 0;
        chkWin.Checked = (settings.Modifiers & NativeMethods.MOD_WIN) != 0;
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

        settings.Modifiers = mods | NativeMethods.MOD_NOREPEAT;
        settings.Save();
        Result = settings;
        Close();
    }

    private CheckBox MakeCheck(string text)
    {
        return new CheckBox
        {
            Text = text,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.Transparent,
            AutoSize = true
        };
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
