
using System.Diagnostics;
using System.Drawing;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GI.Screenshot;
using WindowsInput;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

public class Program
{
    private static int swidth;
    private static int sheight;
    
    [STAThread]
    public static void Main(string[] args)
    {
        Console.ReadLine();
        var wx = GetMonitorSize();
        swidth = wx.Width;
        sheight = wx.Height;
        var region = CaptureRegion();
        bool? color = false;
        
        var size = region.Size;
        DateTime lastTime = DateTime.MinValue;
        bool isGameRunning = false;
        using (Bitmap bitmap = new Bitmap((int) region.Width, (int) region.Height, PixelFormat.Format32bppArgb))
        {
            var graphics = Graphics.FromImage(bitmap);
            int x = (int) region.X;
            int y = (int) region.Y;
            int width = (int) size.Width;
            size = region.Size;
            int height = (int) size.Height;
            var blockRegionSize = new System.Drawing.Size(width, height);
            var ips = new InputSimulator();
            while (true)
            {
                var start = Stopwatch.GetTimestamp();
                graphics.CopyFromScreen(x, y, 0, 0, blockRegionSize, CopyPixelOperation.SourceCopy);
                if (HasGameStarted(bitmap))
                {
                    if (isGameRunning == false)
                    {
                        Console.WriteLine("Game start detected.");
                        color = GetBallColor(bitmap);
                    }
                    isGameRunning = true;
                    lastTime = DateTime.UtcNow;
                }

                if (DateTime.UtcNow - lastTime >= TimeSpan.FromSeconds(1))
                {
                    if (isGameRunning)
                    {
                        Console.WriteLine("Game end detected.");
                    }

                    isGameRunning = false;
                }
                else
                {
                    var nc = GetBallColor(bitmap);
                    if (nc is not null)
                    {
                        if (nc != color)
                        {
                            Task.Run(() =>
                            {
                                ips.Mouse.LeftButtonDown();
                                Thread.Sleep(20);
                                ips.Mouse.LeftButtonUp();
                            });
                            Console.WriteLine($"Changed to {nc}!");
                        }

                        color = nc;
                    }
                }
                var elapsed = Stopwatch.GetElapsedTime(start);
                if (elapsed < TimeSpan.FromMilliseconds(30))
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(30) - elapsed);
                }
                else
                {
                    // Console.WriteLine("Couldn't keep up!");
                }
            }
            
        }
    }
    
    public static System.Drawing.Size GetMonitorSize()
    {
        Window wnd = new Window();
        wnd.Visibility = Visibility.Hidden;
        wnd.Hide();
        wnd.Opacity = 0;
        wnd.WindowStyle = WindowStyle.None;
        wnd.Show();
        var hwnd = new WindowInteropHelper(wnd).EnsureHandle();
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        wnd.Close();
        NativeMethods.MONITORINFO info = new NativeMethods.MONITORINFO();
        NativeMethods.GetMonitorInfo(new HandleRef(null, monitor), info);
        return info.rcMonitor.Size;
    }

    internal static class NativeMethods
    {
        public const Int32 MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr handle, Int32 flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(HandleRef hmonitor, MONITORINFO info);
    
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
        public class MONITORINFO
        {
            internal int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            internal RECT rcMonitor = new RECT();
            internal RECT rcWork = new RECT();
            internal int dwFlags = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }

            public RECT(System.Drawing.Rectangle r)
            {
                left = r.Left;
                top = r.Top;
                right = r.Right;
                bottom = r.Bottom;
            }

            public static RECT FromXYWH(int x, int y, int width, int height) => new RECT(x, y, x + width, y + height);

            public System.Drawing.Size Size => new System.Drawing.Size(right - left, bottom - top);
        }
    }
    

    /// <summary>
    /// Returns true for white, and false for black
    /// </summary>
    public static bool? GetBallColor(Bitmap bitmap)
    {
        var start = 0.407;
        var end = 1 - 0.407;
        var height = bitmap.Height;
        var width = bitmap.Width;
        int x = width / 2;
        for (int i = (int)(start * height); i <= end * height; i+=1)
        {
            if (bitmap.GetPixel(x, i).GetBrightness() <= 0.05)
            {
                return false;
            }
            if (bitmap.GetPixel(x, i).GetBrightness() >= 0.95)
            {
                return true;
            }
        }
        return null;
    }

    public static bool HasGameStarted(Bitmap bitmap)
    {
        var pt = 0.294;
        var height = bitmap.Height;
        var width = bitmap.Width;
        int x = width / 2;
        int i = (int)(height * pt);
        if (bitmap.GetPixel(x, i).GetBrightness() <= 0.05)
        {
            return true;
        }
        if (bitmap.GetPixel(x, i).GetBrightness() >= 0.95)
        {
            return true;
        }

        return false;
    }

    public static T GetFieldValue<T>(object obj, string name) {
        // Set the flags so that private and public fields from instances will be found
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var field = obj.GetType().GetField(name, bindingFlags);
        return (T)field?.GetValue(obj);
    }
    
    public static Rect CaptureRegion()
    {
        BitmapSource bitmap = Screenshot.CaptureRegion(new Rect(0, 0, swidth
            , sheight));
        RegionSelectionWindow regionSelectionWindow1 = new RegionSelectionWindow();
        regionSelectionWindow1.WindowStyle = WindowStyle.None;
        regionSelectionWindow1.ResizeMode = ResizeMode.NoResize;
        regionSelectionWindow1.Topmost = true;
        regionSelectionWindow1.ShowInTaskbar = false;
        GetFieldValue<System.Windows.Controls.Image>(regionSelectionWindow1, "BackgroundImage").Source = bitmap;
        GetFieldValue<System.Windows.Controls.Image>(regionSelectionWindow1, "BackgroundImage").Opacity = 0.2;
        regionSelectionWindow1.BorderThickness = new Thickness(0.0);
        regionSelectionWindow1.Left = 0;
        regionSelectionWindow1.Top = 0;
        regionSelectionWindow1.Width = swidth;
        regionSelectionWindow1.Height = sheight;
        regionSelectionWindow1.ShowDialog();
        var dpiScale = VisualTreeHelper.GetDpi(regionSelectionWindow1);
        var val = regionSelectionWindow1.SelectedRegion.Value;
        val.Scale(dpiScale.DpiScaleX, dpiScale.DpiScaleY);
        return val;
    }
}