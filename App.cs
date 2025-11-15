using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Reflection;
namespace IMEInd;

sealed class ToastForm : Form
{
    readonly Label _label;
    readonly Label _icon;
    readonly System.Windows.Forms.Timer _timer;


    public ToastForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        var height = 80;
        _icon = new Label
        {
            Text = "\uF2B7",
            AutoSize = true,
            Font = new Font("Segoe Fluent Icons", 20),
            ForeColor = Color.White,
            BackColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, 0),
            MaximumSize = new Size(int.MaxValue, height),
            MinimumSize = new Size(0, height),
        };
        Controls.Add(_icon);
        _label = new Label
        {
            ForeColor = Color.White,
            BackColor = Color.Black,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            Location = new Point(_icon.Width, 0),
            MaximumSize = new Size(int.MaxValue, height),
            MinimumSize = new Size(0, height),
        };
        Controls.Add(_label);

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.Black;
        Opacity = 0.5;
        _timer = new System.Windows.Forms.Timer { Interval = 5000 };
        _timer.Tick += (_, __) => Hide();
    }
    public void ShowToast(string text, Point? near = null)
    {
        _label.Text = text;
        Opacity = 0.95;
        Location = near.HasValue
            ? new Point(Math.Max(0, near.Value.X - Width / 2), Math.Max(0, near.Value.Y))
            : new Point(Screen.PrimaryScreen!.Bounds.Width / 2 - Width / 2, Screen.PrimaryScreen!.Bounds.Height - Height - 100);
        Show();
        _timer.Stop();
        _timer.Start();
    }
}


class App
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);

    record IME(int LangID)
    {
        public string Name => LangID switch
        {
            0x0409 => "English",
            0x0804 => "简体中文",
            0x0404 => "繁體中文",
            0x0411 => "日本語",
            0x0412 => "한국어",
            _ => $"0x{LangID:X4}"
        };
    }

    private DateTime lastToastTime = DateTime.MinValue;
    private IME? lastIME = null;

    IME GetCurrent()
    {
        var h = GetForegroundWindow();
        var tid = GetWindowThreadProcessId(h, out _);
        var hkl = GetKeyboardLayout(tid);
        ushort langID = (ushort)((ulong)hkl & 0xFFFF);
        lastIME = new IME(langID);
        lastToastTime = DateTime.Now;
        return lastIME;
    }

    public bool Show(ToastForm indicator)
    {
        var previousIME = lastIME;
        var previousTime = lastToastTime;
        var currentIME = GetCurrent();
        if (DateTime.Now.Subtract(previousTime).TotalMinutes > 5 || previousIME != currentIME)
        {
            indicator.ShowToast($"{currentIME.Name}");
            return true;
        }
        return false;
    }

    [STAThread]
    public void Run()
    {
        ApplicationConfiguration.Initialize();
        var indicator = new ToastForm();

        // Load icon from embedded resource
        Icon? trayIconImage = null;
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "IMEInd.resources.icon.ico";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                trayIconImage = new Icon(stream);
            }
        }
        catch
        {
            // Fallback to system icon if loading fails
        }

        // Create tray icon
        var trayIcon = new NotifyIcon
        {
            Icon = trayIconImage ?? SystemIcons.Information,
            Text = "IME Indicator",
            Visible = true
        };

        // Create context menu
        var contextMenu = new ContextMenuStrip();
        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (s, e) =>
        {
            trayIcon.Visible = false;
            Application.Exit();
        };
        contextMenu.Items.Add(exitMenuItem);
        trayIcon.ContextMenuStrip = contextMenu;

        Automation.AddAutomationFocusChangedEventHandler(OnFocusChanged);
        SystemEventsWrapper.OnInputLangChange += () =>
        {
            Show(indicator);
        };

        Application.Run(new ApplicationContext());

        // Cleanup
        Automation.RemoveAllEventHandlers();
        trayIcon.Dispose();

        void OnFocusChanged(object? sender, AutomationFocusChangedEventArgs e)
        {
            try
            {
                var el = AutomationElement.FocusedElement;
                if (el is null) return;

                var ct = el.Current.ControlType;
                bool editable = ct == ControlType.Edit || ct == ControlType.Document
                                || el.TryGetCurrentPattern(ValuePattern.Pattern, out _)
                                || el.TryGetCurrentPattern(TextPattern.Pattern, out _);

                if (!editable) return;

                Show(indicator);
            }
            catch { /* swallow */ }
        }
    }
}

public static class SystemEventsWrapper
{
    // Simple: poll HKL changes (120 ms); trigger callback on change (responsive enough)
    static SystemEventsWrapper()
    {
        var t = new System.Windows.Forms.Timer { Interval = 120 };
        IntPtr last = IntPtr.Zero;
        t.Tick += (_, __) =>
        {
            var h = GetKeyboardLayout(GetCurrentThreadId());
            if (h != last)
            {
                last = h;
                OnInputLangChange?.Invoke();
            }
        };
        t.Start();
    }

    public static event Action? OnInputLangChange;

    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
}