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
    int  _clicks;
    CancellationTokenSource? _cts;
    readonly Random _rand = new();

    Key  _hotkey   = Key.NumPad0;
    int  _hotkeyVk = 0x60; // VK_NUMPAD0
    bool _binding;

    public MainWindow()
    {
        InitializeComponent();
        CpsSlider.ValueChanged += (_, _) => CpsText.Text = ((int)CpsSlider.Value).ToString();
        UseDelay.Checked       += (_, _) => DelayBox.IsEnabled = true;
        UseDelay.Unchecked     += (_, _) => DelayBox.IsEnabled = false;
        Task.Run(HotkeyLoop);
    }

    async Task HotkeyLoop()
    {
        bool wasDown = false;
        while (true)
        {
            bool down = (GetAsyncKeyState(_hotkeyVk) & 0x8000) != 0;
            if (down && !wasDown && !_binding)
                Dispatcher.Invoke(() => { if (_running) Stop(); else _ = Start(); });
            wasDown = down;
            await Task.Delay(15);
        }
    }

    void Toggle_Click(object s, RoutedEventArgs e) { if (_running) Stop(); else _ = Start(); }

    async Task Start()
    {
        _running = true;
        _cts = new();
        ToggleBtn.Content = "STOP";
        ToggleBtn.Background = new SolidColorBrush(Color.FromRgb(190, 18, 60));
        StatusText.Text = "Running";
        StatusText.Foreground = Dot.Fill = new SolidColorBrush(Color.FromRgb(52, 211, 153));
        var tok = _cts.Token;
        try
        {
            while (!tok.IsCancellationRequested)
            {
                int ms = UseDelay.IsChecked == true
                    ? (int.TryParse(DelayBox.Text, out int d) ? Math.Max(1, d) : 50)
                    : 1000 / Math.Max(1, (int)CpsSlider.Value + (Randomize.IsChecked == true ? _rand.Next(-3, 4) : 0));
                mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
                mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                ClickCount.Text = $"{++_clicks:N0} clicks";
                await Task.Delay(Math.Max(1, ms), tok);
            }
        }
        catch (OperationCanceledException) { }
    }

    void Stop()
    {
        _running = false;
        _cts?.Cancel(); _cts = null;
        ToggleBtn.Content = "START";
        ToggleBtn.Background = new SolidColorBrush(Color.FromRgb(109, 40, 217));
        StatusText.Text = "Idle";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        Dot.Fill = new SolidColorBrush(Color.FromRgb(55, 65, 81));
    }

    void HotkeyBtn_Click(object s, RoutedEventArgs e)
    {
        if (_binding) return;
        if (_running) Stop();
        _binding = true;
        HotkeyBtn.Content = "…";
        HotkeyHint.Text = " · press any key";
        KeyDown += OnBind;
    }

    void OnBind(object s, KeyEventArgs e)
    {
        e.Handled = true;
        KeyDown -= OnBind;
        _binding = false;
        if (e.Key != Key.Escape)
        {
            _hotkey   = e.Key;
            _hotkeyVk = KeyInterop.VirtualKeyFromKey(e.Key);
            HotkeyBtn.Content = KeyName(e.Key);
        }
        else HotkeyBtn.Content = KeyName(_hotkey);
        HotkeyHint.Text = " · click to rebind";
    }

    static string KeyName(Key k) => k switch
    {
        Key.NumPad0 => "Num 0", Key.NumPad1 => "Num 1", Key.NumPad2 => "Num 2",
        Key.NumPad3 => "Num 3", Key.NumPad4 => "Num 4", Key.NumPad5 => "Num 5",
        Key.NumPad6 => "Num 6", Key.NumPad7 => "Num 7", Key.NumPad8 => "Num 8",
        Key.NumPad9 => "Num 9", _ => k.ToString()
    };

    void TitleBar_MouseDown(object s, MouseButtonEventArgs e)
        { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    void Minimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object s, RoutedEventArgs e) { Stop(); Application.Current.Shutdown(); }
}
