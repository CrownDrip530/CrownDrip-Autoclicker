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

    // Gold colours
    static readonly SolidColorBrush GoldBrush   = new(Color.FromRgb(200, 168,  48));
    static readonly SolidColorBrush GreenBrush  = new(Color.FromRgb( 52, 211, 153));
    static readonly SolidColorBrush DimBrush    = new(Color.FromRgb( 42,  32,   8));
    static readonly SolidColorBrush DimTextBrush= new(Color.FromRgb( 58,  48,  16));

    public MainWindow()
    {
        InitializeComponent();
        Task.Run(HotkeyLoop);
    }

    // ── Hotkey polling loop ──────────────────────────────────────────────────
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

    // ── Toggle button ────────────────────────────────────────────────────────
    void Toggle_Click(object s, RoutedEventArgs e)
    {
        if (_running) Stop(); else _ = Start();
    }

    // ── Start clicking ───────────────────────────────────────────────────────
    async Task Start()
    {
        _running = true;
        _cts = new CancellationTokenSource();

        // Read delay value once on UI thread before entering loop
        int baseDelay = ParseDelay();
        bool jitter   = Randomize.IsChecked == true;

        // Update UI
        ToggleBtn.Content    = "STOP";
        ToggleBtn.Foreground = new SolidColorBrush(Color.FromRgb(10, 9, 0));
        // Switch button to red-ish dark gold when running
        var stopGrad = new LinearGradientBrush();
        stopGrad.GradientStops.Add(new GradientStop(Color.FromRgb(200, 60, 40), 0));
        stopGrad.GradientStops.Add(new GradientStop(Color.FromRgb(150, 30, 20), 0.5));
        stopGrad.GradientStops.Add(new GradientStop(Color.FromRgb(110, 20, 10), 1));
        stopGrad.StartPoint = new System.Windows.Point(0, 0);
        stopGrad.EndPoint   = new System.Windows.Point(0, 1);
        // We can't set LinearGradientBrush on Background directly via code easily in WPF
        // so just set a solid colour:
        ToggleBtn.Background = new SolidColorBrush(Color.FromRgb(180, 40, 30));

        StatusText.Text       = "ON";
        StatusText.Foreground = GreenBrush;
        Dot.Fill              = GreenBrush;

        var tok = _cts.Token;
        try
        {
            while (!tok.IsCancellationRequested)
            {
                // Re-read delay each iteration so live edits take effect
                int ms = ParseDelay();
                if (jitter) ms += _rand.Next(-10, 11);
                ms = Math.Max(1, ms);

                mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); // left down
                mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); // left up

                await Task.Delay(ms, tok).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    // ── Stop clicking ────────────────────────────────────────────────────────
    void Stop()
    {
        _running = false;
        _cts?.Cancel();
        _cts = null;

        ToggleBtn.Content    = "START";
        ToggleBtn.Foreground = new SolidColorBrush(Color.FromRgb(10, 9, 0));
        ToggleBtn.Background = null; // let XAML gradient take over again — re-apply via style reset
        // Force re-apply the gold gradient by clearing local value
        ToggleBtn.ClearValue(BackgroundProperty);

        StatusText.Text       = "IDLE";
        StatusText.Foreground = DimTextBrush;
        Dot.Fill              = DimBrush;
    }

    // ── Parse delay textbox safely ───────────────────────────────────────────
    int ParseDelay()
    {
        if (int.TryParse(DelayBox?.Text, out int d) && d > 0)
            return d;
        return 50; // fallback default
    }

    // ── Hotkey binding ───────────────────────────────────────────────────────
    void HotkeyBtn_Click(object s, RoutedEventArgs e)
    {
        if (_binding) return;
        if (_running) Stop();
        _binding = true;
        HotkeyBtn.Content = "…";
        HotkeyHint.Text   = "  · press any key";
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
            HotkeyBtn.Content = KeyName(e.Key);
        }
        else
        {
            HotkeyBtn.Content = KeyName(_hotkey);
        }

        HotkeyHint.Text = "  · rebind";
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
        _           => k.ToString()
    };

    // ── Window chrome ────────────────────────────────────────────────────────
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
