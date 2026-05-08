using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CrownDripAutoclicker;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr i);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int v);

    const uint DOWN = 0x0002;
    const uint UP = 0x0004;

    bool running;
    int clicks;
    CancellationTokenSource? token;
    Random rand = new();

    bool fullscreen = false;

    public MainWindow()
    {
        InitializeComponent();

        CpsSlider.ValueChanged += (_, _) =>
            CpsText.Text = $"{(int)CpsSlider.Value} CPS";

        ToggleButton.Click += async (_, _) =>
        {
            if (running) Stop();
            else await Start();
        };

        Task.Run(HotkeyLoop);
    }

    async Task HotkeyLoop()
    {
        while (true)
        {
            if ((GetAsyncKeyState(0x75) & 1) != 0)
            {
                Dispatcher.Invoke(async () =>
                {
                    if (running) Stop();
                    else await Start();
                });
            }

            await Task.Delay(50);
        }
    }

    async Task Start()
    {
        running = true;
        token = new CancellationTokenSource();

        ToggleButton.Content = "STOP";
        StatusText.Text = "Running";

        StartRainbow();

        try
        {
            while (!token.IsCancellationRequested)
            {
                int delay;

                if (UseDelayMode.IsChecked == true)
                {
                    if (!int.TryParse(DelayBox.Text, out delay))
                        delay = 50;
                }
                else
                {
                    int cps = (int)CpsSlider.Value;

                    if (Randomize.IsChecked == true)
                        cps += rand.Next(-3, 4);

                    cps = Math.Max(1, cps);

                    delay = 1000 / cps;
                }

                mouse_event(DOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(UP, 0, 0, 0, UIntPtr.Zero);

                clicks++;
                ClickCount.Text = $"{clicks} clicks";

                await Task.Delay(Math.Max(1, delay), token.Token);
            }
        }
        catch { }
    }

    void Stop()
    {
        running = false;
        token?.Cancel();

        ToggleButton.Content = "START";
        StatusText.Text = "Idle";

        StopRainbow();
    }

    void StartRainbow()
    {
        var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(2)))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };

        var rt = new RotateTransform();
        GlowBorder.RenderTransform = rt;
        GlowBorder.RenderTransformOrigin = new Point(0.5, 0.5);

        rt.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    void StopRainbow()
    {
        GlowBorder.RenderTransform = null;
    }

    // WINDOW CONTROLS
    private void DragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (!fullscreen)
        {
            WindowState = WindowState.Maximized;
            fullscreen = true;
        }
        else
        {
            WindowState = WindowState.Normal;
            fullscreen = false;
        }
    }
}
