using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Linq;
namespace IMEInd;

public enum ToastStyle
{
    Default = 0,
    StrikeThrough = 1,
}

public sealed class ToastForm : Form
{
    readonly Label _label;
    readonly Label _icon;
    readonly System.Windows.Forms.Timer _timer;
    readonly FlowLayoutPanel _panel;


    public ToastForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        var height = 80;
        var opacity = 1.0;
        int alpha = (int)(opacity * 255);
        var baseColor = ColorTranslator.FromHtml("#122738");
        var backColor = Color.FromArgb(alpha, baseColor);
        _icon = new Label
        {
            Text = "\uF2B7",
            AutoSize = true,
            Font = new Font("Segoe Fluent Icons", 20),
            ForeColor = Color.White,
            BackColor = backColor,
            TextAlign = ContentAlignment.MiddleCenter,
            MaximumSize = new Size(int.MaxValue, height),
            MinimumSize = new Size(0, height),
        };
        _label = new Label
        {
            ForeColor = Color.White,
            BackColor = backColor,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            MaximumSize = new Size(int.MaxValue, height),
            MinimumSize = new Size(0, height),
        };
        // XXX: I don't know why Point(2, 2), and Rectangle(2,2,Width -4, Height -4) but it works
        _panel = new FlowLayoutPanel
        {
            BackColor = backColor,
            Location = new Point(2, 2),
            MaximumSize = new Size(int.MaxValue, height),
            MinimumSize = new Size(0, height),
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
        };
        _panel.Controls.Add(_icon);
        _panel.Controls.Add(_label);
        Controls.Add(_panel);
        Paint += (_, e) =>
        {
            e.Graphics.DrawRectangle(new Pen(ColorTranslator.FromHtml("#ff9d00"), 4), 2, 2, Width - 4, Height - 4);
        };
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        TopMost = true;
        _timer = new System.Windows.Forms.Timer { Interval = 5000 };
        _timer.Tick += (_, __) => Hide();
    }
    public void ShowToast(string text, Screen screen, ToastStyle style = ToastStyle.Default, AutomationElement? inputElement = null)
    {
        // Ensure UI updates happen on the UI thread
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowToast(text, screen, style, inputElement)));
            return;
        }

        _label.Text = text;
        // Update font style to allow future style extensions
        var fontStyle = style == ToastStyle.StrikeThrough ? FontStyle.Bold | FontStyle.Strikeout : FontStyle.Bold;
        _label.Font = new Font(_label.Font.FontFamily, _label.Font.Size, fontStyle);
        // Ensure layout reflects new text/icon sizes before positioning
        PerformLayout();
        // Reposition label in case icon width changed
        _label.Location = new Point(_icon.Width, 0);

        Opacity = 0.95;

        Point nearPoint;
        if (inputElement != null)
        {
            // Floating mode: position indicator around the input field
            try
            {
                var boundingRect = inputElement.Current.BoundingRectangle;
                // Position indicator above the input field, centered horizontally
                var x = (int)(boundingRect.X + boundingRect.Width / 2 - Width / 2);
                var y = (int)(boundingRect.Y - Height - 10); // 10px gap above the input field

                // Ensure the indicator stays within screen bounds
                var screenBounds = screen.Bounds;
                x = Math.Max(screenBounds.X, Math.Min(x, screenBounds.X + screenBounds.Width - Width));
                y = Math.Max(screenBounds.Y, Math.Min(y, screenBounds.Y + screenBounds.Height - Height));

                // If there's not enough space above, position below the input field
                if (y < screenBounds.Y + 20)
                {
                    y = (int)(boundingRect.Y + boundingRect.Height + 10);
                    y = Math.Min(y, screenBounds.Y + screenBounds.Height - Height);
                }

                nearPoint = new Point(x, y);
            }
            catch
            {
                // Fallback to default position if getting bounding rectangle fails
                var centerX = screen.Bounds.X + screen.Bounds.Width / 2;
                var y = screen.Bounds.Y + screen.Bounds.Height - 200 - Height;
                nearPoint = new Point(centerX, Math.Max(screen.Bounds.Y, y));
            }
        }
        else
        {
            // Default mode: position at fixed location near bottom of screen
            var centerX = screen.Bounds.X + screen.Bounds.Width / 2;
            var y = screen.Bounds.Y + screen.Bounds.Height - 200 - Height;
            nearPoint = new Point(centerX, Math.Max(screen.Bounds.Y, y));
        }

        Location = nearPoint;

        Show();
        // Restart timer on UI thread so Tick will fire correctly
        _timer.Stop();
        _timer.Start();
    }
}


public class App
{
    // 0: None, 1: Error, 2: Info, 3: Debug
    public static int LogLevel = 2;
    private static readonly object LogLock = new();
    private static string? LogFilePath;
    private static Config? _config;

    static App()
    {
#if DEBUG
        try
        {
            AllocConsole();
            Console.WriteLine("[DEBUG] Console allocated. IMEInd starting...");
        }
        catch { /* If console allocation fails, continue silently. */ }
#endif

        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logDir = Path.Combine(appDataPath, "IMEInd");
            Directory.CreateDirectory(logDir);
            LogFilePath = Path.Combine(logDir, "IMEInd.log");

            // Allow overriding log level via environment variable IMEIND_LOGLEVEL (0-3)
            var env = Environment.GetEnvironmentVariable("IMEIND_LOGLEVEL");
            if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out var lvl))
            {
                LogLevel = Math.Min(3, Math.Max(0, lvl));
            }
        }
        catch { /* Ignore logging init errors */ }
    }

    public static void log(string msg)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}";
#if DEBUG
        // In Debug we have a console allocated; write there
        try { Console.Error.WriteLine($"[LOGGING] {line}"); } catch { }
#else
        // In Release there is typically no console; emit to debug listeners
        try { System.Diagnostics.Debug.WriteLine(line); } catch { }
#endif
        try
        {
            if (!string.IsNullOrEmpty(LogFilePath))
            {
                lock (LogLock)
                {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
        }
        catch { /* Swallow logging failures */ }
    }

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
            _ => $"0x{LangID:X4}",
        };

        // Whether the language is served by a true IME (e.g., Chinese/Japanese/Korean).
        // English and most other layouts are not IMEs.
        public bool IsSupportedIME => LangID switch
        {
            0x0409 => true, // English
            0x0804 => true, // Simplified Chinese
            0x0404 => true, // Traditional Chinese
            0x0411 => true, // Japanese
            0x0412 => true, // Korean
            _ => false,
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
        try
        {
            ControlType ct = element.Current.ControlType;
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

            // FindFirst can throw ElementNotAvailableException if the element becomes stale
            var child = element.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
#if DEBUG
            debugRecord.hasEditableChild = child != null;
#endif
            if (child != null)
                return (true, debugRecord);

            return (false, debugRecord);
        }
        catch (ElementNotAvailableException)
        {
            // The automation element is no longer available (window closed or control destroyed).
            // Treat as non-editable and continue.
            return (false, debugRecord);
        }
        catch (Exception)
        {
            // Any other automation errors should fail gracefully and be considered non-editable.
            return (false, debugRecord);
        }
    }

    public void Show(ToastForm indicator, IME ime, Screen screen)
    {
        if (LogLevel >= 2)
        {
            log($"Showing IME toast: {ime.LangID} on screen {screen.DeviceName}");
        }
        // When the lookup indicates the current layout is not a supported IME, display
        // explicit "Unavailable" with strike-through to inform the user.
        var text = ime.IsSupportedIME ? ime.Name : "Unavailable";
        var toastStyle = ime.IsSupportedIME ? ToastStyle.Default : ToastStyle.StrikeThrough;

        AutomationElement? inputElement = null;
        if (_config?.FloatingMode == true)
        {
            try
            {
                var focusedElement = AutomationElement.FocusedElement;
                // Only show floating mode when an input element has actual keyboard focus
                if (focusedElement != null && IsEditable(focusedElement))
                {
                    inputElement = focusedElement;
                }
                else
                {
                    // If no editable element has focus, don't show the toast in floating mode
                    if (LogLevel >= 2)
                    {
                        log($"Floating mode: no editable element with focus, hiding toast");
                    }
                    indicator.Hide();
                    return;
                }
            }
            catch
            {
                // If getting focused element fails, don't show the toast in floating mode
                if (LogLevel >= 2)
                {
                    log($"Floating mode: failed to get focused element, hiding toast");
                }
                indicator.Hide();
                return;
            }
        }

        indicator.ShowToast(text, screen, toastStyle, inputElement);
    }

    [STAThread]
    public void Run()
    {
        if (LogLevel >= 2 && LogFilePath is not null)
        {
            log($"Log file path: {LogFilePath}");
        }

        // Create default configuration file if it doesn't exist
        Config.CreateDefault();

        // Load configuration
        _config = Config.Load();

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

        listener.OnInputLangChange += (ime, screen) =>
        {
            Show(indicator, ime, screen);
        };

        listener.OnFocusChanged += (ime, screen) =>
        {
            Show(indicator, ime, screen);
        };
        listener.FirstRun += (ime, screen) =>
        {
            Show(indicator, ime, screen);
        };
        listener.OnLongTimeElapsed += (ime, screen) =>
        {
            Show(indicator, ime, screen);
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
        private readonly Config config;
        private readonly HashSet<string> excludeSet = new HashSet<string>();
        // For ancestor retrieval
        private const uint GA_ROOT = 2;

        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        IME GetCurrentIME()
        {
            // Prefer the raw focused element handle (if available) because
            // the focused control may be hosted on a different thread than
            // the top-level window (observed with Notepad / edit controls).
            var (_, w, raw) = GetCurrentElementAndForegroundWindow();
            IntPtr hwndForThread = raw != 0 ? (IntPtr)raw : (IntPtr)w;
            var tid = GetWindowThreadProcessId(hwndForThread, out _);
            var hkl = GetKeyboardLayout(tid);
            ushort LangID = (ushort)((ulong)hkl & 0xFFFF);
            return new IME(LangID);
        }

        private static bool IsWebView2HostClass(string? className) =>
            !string.IsNullOrEmpty(className) && (
                className.Contains("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("WebView", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("CefBrowserWindow", StringComparison.OrdinalIgnoreCase));

        private static IntPtr GetTopLevel(IntPtr h)
        {
            if (h == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                var top = GetAncestor(h, GA_ROOT);
                return top != IntPtr.Zero ? top : h;
            }
            catch
            {
                return h;
            }
        }

        // Normalizes a raw UIA handle to a real top-level window; falls back to process MainWindow or foreground window.
        private nint GetRealWindow(nint rawHandle, AutomationElement? el)
        {
            IntPtr candidate = rawHandle;
            string? className = null;
            int? processId = null;
            if (el != null)
            {
                try
                {
                    className = el.Current.ClassName;
                    processId = el.Current.ProcessId;
                }
                catch { }
            }

            // Normalize to top-level ancestor if possible.
            candidate = GetTopLevel(candidate);

            // If the class name looks like an embedded browser surface, prefer foreground top-level window.
            if (candidate == IntPtr.Zero || IsWebView2HostClass(className))
            {
                var fg = GetForegroundWindow();
                if (fg != IntPtr.Zero) candidate = fg;
            }

            // Process-based fallback: sometimes MainWindowHandle differs from the raw content handle.
            if (candidate == IntPtr.Zero && processId.HasValue)
            {
                try
                {
                    var proc = Process.GetProcessById(processId.Value);
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        candidate = proc.MainWindowHandle;
                    }
                }
                catch { }
            }

            // Final fallback to desktop window.
            if (candidate == IntPtr.Zero)
            {
                candidate = GetDesktopWindow();
            }
            return candidate;
        }

        private (AutomationElement?, nint, nint) GetCurrentElementAndForegroundWindow()
        {
            AutomationElement? el;
            try
            {
                el = AutomationElement.FocusedElement;
            }
            catch
            {
                el = null;
            }
            IntPtr rawHandle = IntPtr.Zero;
            if (el != null)
            {
                try
                {
                    rawHandle = new IntPtr(el.Current.NativeWindowHandle);
                }
                catch { rawHandle = IntPtr.Zero; }
            }

            var candidate = GetRealWindow(rawHandle, el);

            return (el, candidate, rawHandle);
        }

        private nint GetForegroundInputWindow()
        {
            var (el, candidate, rawHandle) = GetCurrentElementAndForegroundWindow();
            if (lastWindow == IntPtr.Zero)
            {
                if (LogLevel >= 2)
                {
                    log($"Initial resolved window handle: {candidate}");
                }
                if (IsExcludedWindow(candidate))
                {
                    if (LogLevel >= 2)
                    {
                        log($"Initial window suppressed (blacklisted). Handle: {candidate}");
                    }
                    return IntPtr.Zero;
                }
                return candidate;
            }

            bool editable = el != null && IsEditable(el);
            if (LogLevel >= 3)
            {
                log($"Raw handle: {rawHandle}, Resolved handle: {candidate}, Editable: {editable}");
            }
            if (editable && candidate != IntPtr.Zero)
            {
                if (LogLevel >= 3)
                {
                    log($"Focused element editable; using resolved handle: {candidate}");
                }
                if (IsExcludedWindow(candidate))
                {
                    if (LogLevel >= 2)
                    {
                        log($"Editable window suppressed (blacklisted). Handle: {candidate}");
                    }
                    return lastWindow; // keep previous
                }
                return candidate;
            }

            // If candidate differs from lastWindow, prefer the fresh candidate even if not editable to avoid staleness.
            if (candidate != IntPtr.Zero && candidate != lastWindow)
            {
                if (LogLevel >= 3)
                {
                    log($"Non-editable focus; updating window to new handle: {candidate}");
                }
                if (IsExcludedWindow(candidate))
                {
                    if (LogLevel >= 2)
                    {
                        log($"Non-editable window change suppressed (blacklisted). Handle: {candidate}");
                    }
                    return lastWindow;
                }
                return candidate;
            }

            if (IsExcludedWindow(lastWindow))
            {
                if (LogLevel >= 2)
                {
                    log($"Existing window is now excluded; returning zero handle.");
                }
                return IntPtr.Zero;
            }
            return lastWindow;
        }


        private System.Windows.Forms.Timer timer;
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
            if (!IsExcludedWindow(lastWindow))
            {
                FirstRun?.Invoke(lastIME, lastScreen);
            }
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
            config = Config.Load();
            // Build normalized blacklist set (both with and without .exe extensions)
            foreach (var name in config.ExcludeExecutables)
            {
                var trimmed = name.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var lower = trimmed.ToLowerInvariant();
                if (lower.EndsWith(".exe"))
                {
                    excludeSet.Add(lower);
                    excludeSet.Add(lower[..^4]);
                }
                else
                {
                    excludeSet.Add(lower);
                    excludeSet.Add(lower + ".exe");
                }
            }
            if (excludeSet.Count > 0 && LogLevel >= 2)
            {
                log($"Process blacklist loaded: {string.Join(", ", excludeSet)}");
            }
            timer = new System.Windows.Forms.Timer { Interval = config.TimerIntervalMs };
            timer.Tick += (_, __) =>
            {
                var currentIME = GetCurrentIME();
                if (LogLevel >= 3)
                {
                    log($"Checked IME: {currentIME.LangID}, Last IME: {lastIME.LangID}");
                }
                if (currentIME != lastIME)
                {
                    lastIME = currentIME;
                    forceUpdateTime();
                    forceUpdateWindow();
                    if (LogLevel >= 2)
                    {
                        log($"IME changed to: {lastIME.LangID}, Window: {lastWindow}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
                    }
                    // Use the actual current window for exclusion, not the possibly retained lastWindow
                    var currentFocusWindow = GetForegroundInputWindow();
                    if (!IsExcludedWindow(currentFocusWindow))
                    {
                        OnInputLangChange?.Invoke(lastIME, lastScreen);
                    }
                }
            };
            timer.Tick += (_, __) =>
            {
                var currentWindow = GetForegroundInputWindow();
                var now = DateTime.Now;
                if (LogLevel >= 3)
                {
                    log($"Checked Window: {currentWindow}, Last Window: {lastWindow}");
                    log($"Time since last change: {(now - lastTime).TotalSeconds} seconds");
                    log($"Current Screen: {GetScreenFromWindowHandle(currentWindow).DeviceName}, Last Screen: {lastScreen.DeviceName}");
                }
                if (LogLevel >= 2)
                {
                    if (currentWindow != lastWindow)
                    {
                        log($"Last screen: {lastScreen.DeviceName}, New screen: {GetScreenFromWindowHandle(currentWindow).DeviceName}, Time since last change: {(now - lastTime).TotalSeconds} seconds");
                    }
                }
                if (currentWindow != lastWindow && ((now - lastTime).TotalSeconds >= config.WindowChangeThresholdSeconds || GetScreenFromWindowHandle(currentWindow).DeviceName != lastScreen.DeviceName))
                {
                    if (IsExcludedWindow(currentWindow))
                    {
                        if (LogLevel >= 2)
                        {
                            log($"Window change suppressed (blacklisted). Handle: {currentWindow}");
                        }
                        return; // do not update state
                    }
                    lastWindow = currentWindow;
                    forceUpdateTime();
                    forceUpdateIME();
                    forceUpdateScreen();
                    if (LogLevel >= 2)
                    {
                        log($"Window changed to: {lastWindow}, IME: {lastIME.LangID}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
                    }
                    if (!IsExcludedWindow(lastWindow))
                    {
                        OnFocusChanged?.Invoke(lastIME, lastScreen);
                    }
                }
            };
            timer.Tick += (_, __) =>
            {
                var now = DateTime.Now;
                if ((now - lastTime).TotalMinutes >= config.LongTimeElapsedMinutes)
                {
                    lastTime = now;
                    forceUpdateIME();
                    forceUpdateWindow();
                    if (LogLevel >= 2)
                    {
                        log($"Long time elapsed with no changes. IME: {lastIME.LangID}, Window: {lastWindow}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
                    }
                    var currentFocusWindow = GetForegroundInputWindow();
                    if (!IsExcludedWindow(currentFocusWindow))
                    {
                        OnLongTimeElapsed?.Invoke(lastIME, lastScreen);
                    }
                }
            };
        }

        public delegate void Action(IME ime, Screen screen);

        public event Action? OnInputLangChange;
        public event Action? OnFocusChanged;
        public event Action? FirstRun;
        public event Action? OnLongTimeElapsed;

        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

        private bool IsExcludedWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || excludeSet.Count == 0) return false;
            try
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0) return false;
                Process? proc = null;
                try { proc = Process.GetProcessById((int)pid); } catch { return false; }
                if (proc == null) return false;
                string exeName;
                try
                {
                    exeName = Path.GetFileName(proc.MainModule?.FileName) ?? proc.ProcessName + ".exe";
                }
                catch
                {
                    exeName = proc.ProcessName + ".exe";
                }
                var lower = exeName.ToLowerInvariant();
                var baseName = lower.EndsWith(".exe") ? lower[..^4] : lower;
                return excludeSet.Contains(lower) || excludeSet.Contains(baseName);
            }
            catch
            {
                return false;
            }
        }
    }
}
