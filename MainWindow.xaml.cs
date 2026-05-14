using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CrownDripAutoclicker;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr i);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int v);

    bool _running;
    CancellationTokenSource? _cts;
    readonly Random _rand = new();

    Key  _hotkey   = Key.NumPad0;
    int  _hotkeyVk = 0x60; // VK_NUMPAD0
    bool _binding;

    // ── Shared brushes ───────────────────────────────────────────────────────
    static readonly Color GoldColor   = Color.FromRgb(200, 168,  48);
    static readonly Color GreenColor  = Color.FromRgb( 52, 211, 153);
    static readonly Color RedColor    = Color.FromRgb(200,  60,  40);
    static readonly Color DimColor    = Color.FromRgb( 42,  32,   8);
    static readonly Color DimTxtColor = Color.FromRgb( 58,  48,  16);

    public MainWindow()
    {
        InitializeComponent();
        Task.Run(HotkeyLoop);
    }

    // ── Hotkey polling (background thread) ───────────────────────────────────
    async Task HotkeyLoop()
    {
        bool wasDown = false;
        while (true)
        {
            bool down = (GetAsyncKeyState(_hotkeyVk) & 0x8000) != 0;
            if (down && !wasDown && !_binding)
                Dispatcher.Invoke(() => { if (_running) Stop(); else _ = Start(); });
            wasDown = down;
            await Task.Delay(15).ConfigureAwait(false);
        }
    }

    // ── Toggle button handler ─────────────────────────────────────────────
    void Toggle_Click(object s, RoutedEventArgs e)
    {
        if (_running) Stop(); else _ = Start();
    }

    // ── Start clicking ────────────────────────────────────────────────────
    async Task Start()
    {
        _running = true;
        _cts = new CancellationTokenSource();

        // Capture jitter flag on UI thread before entering loop
        bool jitter = Randomize.IsChecked == true;

        // Update UI state → running
        ToggleBtn.Content    = "STOP";
        ToggleBtn.Foreground = new SolidColorBrush(Colors.White);
        SetToggleBackground(RedColor, Color.FromRgb(160, 40, 30), Color.FromRgb(120, 24, 16));
        StatusText.Text       = "ON";
        StatusText.Foreground = new SolidColorBrush(GreenColor);
        Dot.Fill              = new SolidColorBrush(GreenColor);

        var tok = _cts.Token;
        try
        {
            while (!tok.IsCancellationRequested)
            {
                int ms = ParseDelay();
                if (jitter) ms += _rand.Next(-10, 11);
                ms = Math.Max(1, ms);

                mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); // MOUSEEVENTF_LEFTDOWN
                mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); // MOUSEEVENTF_LEFTUP

                await Task.Delay(ms, tok).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    // ── Stop clicking ─────────────────────────────────────────────────────
    void Stop()
    {
        _running = false;
        _cts?.Cancel();
        _cts = null;

        // Restore gold gradient on button
        ToggleBtn.Content    = "START";
        ToggleBtn.Foreground = new SolidColorBrush(Color.FromRgb(10, 9, 0));
        SetToggleBackground(
            Color.FromRgb(240, 208, 96),
            Color.FromRgb(200, 168, 48),
            Color.FromRgb(160, 120, 32));

        StatusText.Text       = "IDLE";
        StatusText.Foreground = new SolidColorBrush(DimTxtColor);
        Dot.Fill              = new SolidColorBrush(DimColor);
    }

    // Helper: apply a 3-stop vertical gradient to the toggle button
    void SetToggleBackground(Color top, Color mid, Color bot)
    {
        var grad = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint   = new System.Windows.Point(0, 1)
        };
        grad.GradientStops.Add(new GradientStop(top, 0.0));
        grad.GradientStops.Add(new GradientStop(mid, 0.5));
        grad.GradientStops.Add(new GradientStop(bot, 1.0));
        // The XAML template uses Border.Background; we update it via the button's Tag
        // and re-trigger via the code-behind approach: set directly on the child border
        // Since WPF ControlTemplate owns the Border, we manipulate via Background property:
        ToggleBtn.Background = grad;
        // Note: because the ControlTemplate uses {TemplateBinding Background} on Bd,
        // setting ToggleBtn.Background propagates correctly into the template.
        // (The XAML template must use <Border Background="{TemplateBinding Background}">)
    }

    // ── Parse delay textbox safely ────────────────────────────────────────
    int ParseDelay()
    {
        if (int.TryParse(DelayBox?.Text?.Trim(), out int d) && d >= 1)
            return d;
        return 50;
    }

    // ── Hotkey binding ────────────────────────────────────────────────────
    void HotkeyBtn_Click(object s, RoutedEventArgs e)
    {
        if (_binding) return;
        if (_running) Stop();
        _binding = true;
        HotkeyBtn.Content = "…";
        HotkeyHint.Text   = "  · press a key";
        KeyDown += OnBind;
    }

    void OnBind(object s, KeyEventArgs e)
    {
        e.Handled = true;
        KeyDown  -= OnBind;
        _binding  = false;

        if (e.Key != Key.Escape)
        {
            _hotkey   = e.Key;
            _hotkeyVk = KeyInterop.VirtualKeyFromKey(e.Key);
        }

        HotkeyBtn.Content = KeyName(_hotkey);
        HotkeyHint.Text   = "  · rebind";
    }

    static string KeyName(Key k) => k switch
    {
        Key.NumPad0 => "Num 0",
        Key.NumPad1 => "Num 1",
        Key.NumPad2 => "Num 2",
        Key.NumPad3 => "Num 3",
        Key.NumPad4 => "Num 4",
        Key.NumPad5 => "Num 5",
        Key.NumPad6 => "Num 6",
        Key.NumPad7 => "Num 7",
        Key.NumPad8 => "Num 8",
        Key.NumPad9 => "Num 9",
        Key.F1      => "F1",  Key.F2  => "F2",  Key.F3  => "F3",
        Key.F4      => "F4",  Key.F5  => "F5",  Key.F6  => "F6",
        Key.F7      => "F7",  Key.F8  => "F8",  Key.F9  => "F9",
        Key.F10     => "F10", Key.F11 => "F11", Key.F12 => "F12",
        _           => k.ToString()
    };

    // ── Window chrome ─────────────────────────────────────────────────────
    void TitleBar_MouseDown(object s, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    void Minimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void Close_Click(object s, RoutedEventArgs e)
    {
        Stop();
        Application.Current.Shutdown();
    }
}
