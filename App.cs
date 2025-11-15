using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Reflection;
using System.Diagnostics;
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
        // Ensure UI updates happen on the UI thread
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowToast(text, near)));
            return;
        }

        _label.Text = text;
        // Ensure layout reflects new text/icon sizes before positioning
        PerformLayout();
        // Reposition label in case icon width changed
        _label.Location = new Point(_icon.Width, 0);

        Opacity = 0.95;
        Location = near.HasValue
            ? new Point(Math.Max(0, near.Value.X - Width / 2), Math.Max(0, near.Value.Y))
            : new Point(Screen.PrimaryScreen!.Bounds.Width / 2 - Width / 2, Screen.PrimaryScreen!.Bounds.Height - Height - 100);

        Show();
        // Restart timer on UI thread so Tick will fire correctly
        _timer.Stop();
        _timer.Start();
    }
}


class App
{
    public static void log(string msg)
    {
        Console.Error.WriteLine($"[LOGGING] {msg}");
    }
    public static int LogLevel = 2; // 0: None, 1: Error, 2: Info, 3: Debug

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
#if DEBUG
    [DllImport("kernel32.dll")] static extern bool AllocConsole();
#endif

    public record IME(int LangID)
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

    private Listener listener = new Listener();

    public static Screen GetScreenFromWindowHandle(nint windowHandler)
    {
        try
        {
            return Screen.FromHandle(windowHandler);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to get screen for window handle {windowHandler}: {ex}");
            // Fallback to primary screen if anything goes wrong
            return Screen.PrimaryScreen!;
        }
    }

    private record struct DebugRecord(
        nint? hWnd,
        string? controlType,
        string? uiElement,
        bool? isValuePattern,
        bool? isTextPattern,
        bool? hasLegacyPatternType,
        string? className,
        bool? hasEditableChild
    )
    {
        public DebugRecord() : this(null, null, null, null, null, null, null, null) { }
    }
#if DEBUG
    private static DebugRecord? lastDebugRecord = null;
#endif

    public static bool IsEditable(AutomationElement element)
    {
        var (r, debugRecord) = _IsEditable(element);
#if DEBUG
        if (lastDebugRecord != debugRecord)
        {
            lastDebugRecord = debugRecord;
            log($"DebugRecord: {debugRecord}");
        }
#endif
        return r;
    }
    private static (bool, DebugRecord) _IsEditable(AutomationElement element)
    {
        DebugRecord debugRecord = new DebugRecord();
        if (element == null)
            return (false, debugRecord);

        var ct = element.Current.ControlType;
#if DEBUG
        debugRecord.controlType = ct.LocalizedControlType;
#endif
        if (ct == ControlType.Edit || ct == ControlType.Document)
            return (true, debugRecord);

        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj))
        {
#if DEBUG
            debugRecord.isValuePattern = true;
#endif
            var valuePattern = (ValuePattern)valuePatternObj;
            if (!valuePattern.Current.IsReadOnly)
                return (true, debugRecord);
        }
#if DEBUG
        debugRecord.isValuePattern = false;
#endif

        if (element.TryGetCurrentPattern(TextPattern.Pattern, out _))
        {
#if DEBUG
            debugRecord.isTextPattern = true;
#endif
            return (true, debugRecord);
        }

#if DEBUG
        debugRecord.isTextPattern = false;
#endif
        var className = element.Current.ClassName;
#if DEBUG
        debugRecord.className = className;
#endif
        if (!string.IsNullOrEmpty(className) &&
            (className.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
             className.Contains("TextArea", StringComparison.OrdinalIgnoreCase)))
            return (true, debugRecord);

        var child = element.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
#if DEBUG
        debugRecord.hasEditableChild = child != null;
#endif
        if (child != null)
            return (true, debugRecord);

        return (false, debugRecord);
    }

    public void Show(ToastForm indicator, IME ime, nint windowHandler)
    {
        Screen screen = GetScreenFromWindowHandle(windowHandler);
        var centerX = screen.Bounds.X + screen.Bounds.Width / 2;
        var y = screen.Bounds.Y + screen.Bounds.Height - 100 - indicator.Height;
        var nearPoint = new Point(centerX, Math.Max(screen.Bounds.Y, y));
        indicator.ShowToast($"{ime.Name}", nearPoint);
    }

    [STAThread]
    public void Run()
    {
#if DEBUG
        try
        {
            AllocConsole();
            Console.WriteLine("[DEBUG] Console allocated. IMEInd starting...");
        }
        catch { /* If console allocation fails, continue silently. */ }
#endif

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

        listener.OnInputLangChange += (ime, windowHandler) =>
        {
            Show(indicator, ime, windowHandler);
        };

        listener.OnFocusChanged += (ime, windowHandler) =>
        {
            Show(indicator, ime, windowHandler);
        };
        listener.FirstRun += (ime, windowHandler) =>
        {
            Show(indicator, ime, windowHandler);
        };

        listener.Start();

        Application.Run(new ApplicationContext());

        // Cleanup
        Automation.RemoveAllEventHandlers();
        trayIcon.Dispose();

    }
    public class Listener
    {
        private DateTime lastTime = DateTime.MinValue;
        private IME lastIME = new IME(0);
        private nint lastWindow = IntPtr.Zero;
        private Screen lastScreen = Screen.PrimaryScreen!;

        IME GetCurrentIME()
        {
            var h = GetForegroundWindow();
            var tid = GetWindowThreadProcessId(h, out _);
            var hkl = GetKeyboardLayout(tid);
            ushort LangID = (ushort)((ulong)hkl & 0xFFFF);
            return new IME(LangID);
        }

        private nint GetForegroundInputWindow()
        {
            var el = AutomationElement.FocusedElement;
            var hWnd = new nint(el.Current.NativeWindowHandle);
            if (hWnd == IntPtr.Zero)
            {
                hWnd = GetDesktopWindow();
                if (LogLevel >= 3)
                {
                    log($"No focused element window, falling back to desktop window: {hWnd}");
                }
            }
            if (lastWindow == IntPtr.Zero)
            {
                if (LogLevel >= 2)
                {
                    log($"Initial focused element window handle: {hWnd}");
                }
                return hWnd;
            }
            if (el != null)
            {
                bool isEditable = IsEditable(el);
                if (LogLevel >= 3)
                {
                    log($"Foreground window handle: {hWnd}, Editable: {isEditable}");
                }
                if (isEditable)
                {
                    if (LogLevel >= 2)
                    {
                        log($"Focused element is editable, using window handle: {hWnd}");
                    }
                    return hWnd;
                }
            }
            return lastWindow;
        }


        private System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 120 };
        private void forceUpdateIME()
        {
            lastIME = GetCurrentIME();
        }

        private void forceUpdateWindow()
        {
            var h = GetForegroundInputWindow();
            lastWindow = h;
            forceUpdateScreen();
        }

        private void forceUpdateTime()
        {
            lastTime = DateTime.Now;
        }

        public void Start()
        {
            forceUpdateIME();
            forceUpdateWindow();
            forceUpdateTime();
            if (LogLevel >= 3)
            {
                log($"Initial IME: {lastIME.LangID}, Window: {lastWindow}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
            }
            FirstRun?.Invoke(lastIME, lastWindow);
            timer.Start();
        }

        private void forceUpdateScreen()
        {
            try
            {
                lastScreen = GetScreenFromWindowHandle(lastWindow);
            }
            catch (Exception ex)
            {
                if (LogLevel >= 1)
                {
                    log($"ERROR: Failed to get screen from window handle {lastWindow}: {ex}");
                }
                lastScreen = Screen.PrimaryScreen!;
            }
        }

        public Listener()
        {
            timer.Tick += (_, __) =>
            {
                var h = GetCurrentIME();
                if (LogLevel >= 3)
                {
                    log($"Checked IME: {h.LangID}, Last IME: {lastIME.LangID}");
                }
                if (h != lastIME)
                {
                    lastIME = h;
                    forceUpdateTime();
                    forceUpdateWindow();
                    if (LogLevel >= 2)
                    {
                        log($"IME changed to: {lastIME.LangID}, Window: {lastWindow}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
                    }
                    OnInputLangChange?.Invoke(lastIME, lastWindow);
                }
            };
            timer.Tick += (_, __) =>
            {
                var h = GetForegroundInputWindow();
                var now = DateTime.Now;
                if (LogLevel >= 3)
                {
                    log($"Checked Window: {h}, Last Window: {lastWindow}");
                    log($"Time since last change: {(now - lastTime).TotalSeconds} seconds");
                    log($"Current Screen: {GetScreenFromWindowHandle(h).DeviceName}, Last Screen: {lastScreen.DeviceName}");
                }
                if (LogLevel >= 2)
                {
                    if (h != lastWindow)
                    {
                        log($"Last screen: {lastScreen.DeviceName}, New screen: {GetScreenFromWindowHandle(h).DeviceName}");
                    }
                }
                if (h != lastWindow && ((now - lastTime).TotalSeconds >= 300 || GetScreenFromWindowHandle(h) != lastScreen))
                {
                    lastWindow = h;
                    forceUpdateScreen();
                    forceUpdateTime();
                    forceUpdateIME();
                    if (LogLevel >= 2)
                    {
                        log($"Window changed to: {lastWindow}, IME: {lastIME.LangID}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
                    }
                    OnFocusChanged?.Invoke(lastIME, lastWindow);
                }
            };
        }

        public delegate void Action(IME ime, nint hwnd);

        public event Action? OnInputLangChange;
        public event Action? OnFocusChanged;
        public event Action? FirstRun;

        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    }
}
