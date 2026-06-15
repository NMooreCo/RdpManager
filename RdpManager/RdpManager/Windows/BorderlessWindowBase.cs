using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using MaterialDesignThemes.Wpf;
using Application = System.Windows.Application;

namespace RdpManager.Windows
{
    #region Windows API Declarations

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MONITORINFO
    {
        public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    #endregion

    public static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, [In, Out] MONITORINFO lpmi);

        public const int MONITOR_DEFAULTTONEAREST = 2;
        public const int WM_GETMINMAXINFO = 0x0024;
    }

    /// <summary>
    /// Base class for borderless windows with custom window chrome
    /// Provides window control buttons, dragging, maximize/minimize, and taskbar-aware maximizing
    /// </summary>
    public abstract class BorderlessWindowBase : Window
    {
        protected readonly PaletteHelper _paletteHelper = new PaletteHelper();
        protected bool _isDarkMode = true; // Start in dark mode by default

        // These should be defined in derived class XAML with x:Name
        protected System.Windows.Controls.Border? WindowBorder => FindName("WindowBorder") as System.Windows.Controls.Border;
        protected Path? MaximizeIcon => FindName("MaximizeIcon") as Path;
        protected System.Windows.Controls.Button? MaximizeButton => FindName("MaximizeButton") as System.Windows.Controls.Button;
        protected Path? ThemeIcon => FindName("ThemeIcon") as Path;

        protected BorderlessWindowBase()
        {
            // Hook WndProc for WM_GETMINMAXINFO
            SourceInitialized += (s, e) =>
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                HwndSource.FromHwnd(handle)?.AddHook(WndProc);
            };

            // Update window corners on state changed
            StateChanged += (s, e) => UpdateWindowCorners();

            // Initialize window on load
            Loaded += (s, e) =>
            {
                UpdateWindowCorners();
                // InitializeDarkMode(); // Disabled - now using ThemeManager for theme persistence
            };
        }

        private void InitializeDarkMode()
        {
            try
            {
                // Apply dark theme on startup
                Theme theme = _paletteHelper.GetTheme();
                theme.SetBaseTheme(BaseTheme.Dark);
                _paletteHelper.SetTheme(theme);

                // Switch to dark color variants
                var resources = Application.Current.Resources;

                void SwitchColor(string baseName)
                {
                    var sourceKey = baseName + ".Dark";
                    if (resources.Contains(sourceKey))
                    {
                        var sourceBrush = resources[sourceKey] as System.Windows.Media.SolidColorBrush;
                        if (sourceBrush != null)
                        {
                            resources[baseName] = new System.Windows.Media.SolidColorBrush(sourceBrush.Color);
                        }
                    }
                }

                // Apply all dark colors
                SwitchColor("App.Header.Background");
                SwitchColor("App.Header.Foreground");
                SwitchColor("App.Background.Window");
                SwitchColor("App.Background.Surface");
                SwitchColor("App.Background.Elevated");
                SwitchColor("App.Primary");
                SwitchColor("App.Primary.Foreground");
                SwitchColor("App.Primary.Lighter");
                SwitchColor("App.Primary.Darker");
                SwitchColor("App.Secondary");
                SwitchColor("App.Secondary.Foreground");
                SwitchColor("App.Text.Primary");
                SwitchColor("App.Text.Secondary");
                SwitchColor("App.Border.Divider");
                SwitchColor("App.Border.Outline");
                SwitchColor("App.Hover");
                SwitchColor("App.Selection");
                SwitchColor("App.Semantic.Success");
                SwitchColor("App.Semantic.Warning");
                SwitchColor("App.Semantic.Error");
                SwitchColor("App.Semantic.Info");

                // Update theme icon to sun (dark mode active)
                if (ThemeIcon != null)
                {
                    ThemeIcon.Data = Geometry.Parse("M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,2L14.39,5.42C13.65,5.15 12.84,5 12,5C11.16,5 10.35,5.15 9.61,5.42L12,2M3.34,7L7.5,6.65C6.9,7.16 6.36,7.78 5.94,8.5C5.5,9.24 5.25,10 5.11,10.79L3.34,7M3.36,17L5.12,13.23C5.26,14 5.53,14.78 5.95,15.5C6.37,16.24 6.91,16.86 7.5,17.37L3.36,17M20.65,7L18.88,10.79C18.74,10 18.47,9.23 18.05,8.5C17.63,7.78 17.1,7.15 16.5,6.64L20.65,7M20.64,17L16.5,17.36C17.09,16.85 17.62,16.22 18.04,15.5C18.46,14.77 18.73,14 18.87,13.21L20.64,17M12,22L9.59,18.56C10.33,18.83 11.14,19 12,19C12.82,19 13.63,18.83 14.37,18.56L12,22Z");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing dark mode: {ex}");
            }
        }

        #region Window Control Handlers

        protected void Header_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double-click to maximize/restore
                MaximizeButton_Click(null, null);
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Drag window
                this.DragMove();
            }
        }

        protected void MinimizeButton_Click(object? sender, RoutedEventArgs? e)
        {
            this.WindowState = WindowState.Minimized;
        }

        protected void MaximizeButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        protected void CloseButton_Click(object? sender, RoutedEventArgs? e)
        {
            this.Close();
        }

        #endregion

        #region Theme Toggle

        protected void ThemeToggle_Click(object? sender, RoutedEventArgs? e)
        {
            _isDarkMode = !_isDarkMode;

            // Get current theme
            Theme theme = _paletteHelper.GetTheme();

            // Toggle base theme (switches Material Design's default colors)
            theme.SetBaseTheme(_isDarkMode ? BaseTheme.Dark : BaseTheme.Light);

            // Apply Material Design theme
            _paletteHelper.SetTheme(theme);

            // Switch ALL custom App.* colors between Light/Dark variants
            var resources = Application.Current.Resources;
            string suffix = _isDarkMode ? ".Dark" : ".Light";

            // Helper to copy color from .Light/.Dark variant to main resource
            void SwitchColor(string baseName)
            {
                var sourceKey = baseName + suffix;
                if (resources.Contains(sourceKey))
                {
                    var sourceBrush = resources[sourceKey] as System.Windows.Media.SolidColorBrush;
                    if (sourceBrush != null)
                    {
                        // Create new brush (can't modify frozen brushes)
                        resources[baseName] = new System.Windows.Media.SolidColorBrush(sourceBrush.Color);
                    }
                }
            }

            // Switch all colors
            if (_isDarkMode)
            {
                SwitchColor("App.Header.Background");
                SwitchColor("App.Header.Foreground");
                SwitchColor("App.Background.Window");
                SwitchColor("App.Background.Surface");
                SwitchColor("App.Background.Elevated");
                SwitchColor("App.Primary");
                SwitchColor("App.Primary.Foreground");
                SwitchColor("App.Primary.Lighter");
                SwitchColor("App.Primary.Darker");
                SwitchColor("App.Secondary");
                SwitchColor("App.Secondary.Foreground");
                SwitchColor("App.Text.Primary");
                SwitchColor("App.Text.Secondary");
                SwitchColor("App.Border.Divider");
                SwitchColor("App.Border.Outline");
                SwitchColor("App.Hover");
                SwitchColor("App.Selection");
                SwitchColor("App.Semantic.Success");
                SwitchColor("App.Semantic.Warning");
                SwitchColor("App.Semantic.Error");
                SwitchColor("App.Semantic.Info");

                // Update icon to sun
                if (ThemeIcon != null)
                {
                    ThemeIcon.Data = Geometry.Parse("M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,2L14.39,5.42C13.65,5.15 12.84,5 12,5C11.16,5 10.35,5.15 9.61,5.42L12,2M3.34,7L7.5,6.65C6.9,7.16 6.36,7.78 5.94,8.5C5.5,9.24 5.25,10 5.11,10.79L3.34,7M3.36,17L5.12,13.23C5.26,14 5.53,14.78 5.95,15.5C6.37,16.24 6.91,16.86 7.5,17.37L3.36,17M20.65,7L18.88,10.79C18.74,10 18.47,9.23 18.05,8.5C17.63,7.78 17.1,7.15 16.5,6.64L20.65,7M20.64,17L16.5,17.36C17.09,16.85 17.62,16.22 18.04,15.5C18.46,14.77 18.73,14 18.87,13.21L20.64,17M12,22L9.59,18.56C10.33,18.83 11.14,19 12,19C12.82,19 13.63,18.83 14.37,18.56L12,22Z");
                }
            }
            else
            {
                // Light Mode - Switch to light colors
                SwitchColor("App.Header.Background");
                SwitchColor("App.Header.Foreground");
                SwitchColor("App.Background.Window");
                SwitchColor("App.Background.Surface");
                SwitchColor("App.Background.Elevated");
                SwitchColor("App.Primary");
                SwitchColor("App.Primary.Foreground");
                SwitchColor("App.Primary.Lighter");
                SwitchColor("App.Primary.Darker");
                SwitchColor("App.Secondary");
                SwitchColor("App.Secondary.Foreground");
                SwitchColor("App.Text.Primary");
                SwitchColor("App.Text.Secondary");
                SwitchColor("App.Border.Divider");
                SwitchColor("App.Border.Outline");
                SwitchColor("App.Hover");
                SwitchColor("App.Selection");
                SwitchColor("App.Semantic.Success");
                SwitchColor("App.Semantic.Warning");
                SwitchColor("App.Semantic.Error");
                SwitchColor("App.Semantic.Info");

                // Update icon to moon
                if (ThemeIcon != null)
                {
                    ThemeIcon.Data = Geometry.Parse("M17.75,4.09L15.22,6.03L16.13,9.09L13.5,7.28L10.87,9.09L11.78,6.03L9.25,4.09L12.44,4L13.5,1L14.56,4L17.75,4.09M21.25,11L19.61,12.25L20.2,14.23L18.5,13.06L16.8,14.23L17.39,12.25L15.75,11L17.81,10.95L18.5,9L19.19,10.95L21.25,11M18.97,15.95C19.8,15.87 20.69,17.05 20.16,17.8C19.84,18.25 19.5,18.67 19.08,19.07C15.17,23 8.84,23 4.94,19.07C1.03,15.17 1.03,8.83 4.94,4.93C5.34,4.53 5.76,4.17 6.21,3.85C6.96,3.32 8.14,4.21 8.06,5.04C7.79,7.9 8.75,10.87 10.95,13.06C13.14,15.26 16.1,16.22 18.97,15.95M17.33,17.97C14.5,17.81 11.7,16.64 9.53,14.5C7.36,12.31 6.2,9.5 6.04,6.68C3.23,9.82 3.34,14.64 6.35,17.66C9.37,20.67 14.19,20.78 17.33,17.97Z");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Theme switched to: {(_isDarkMode ? "Dark" : "Light")} - Navy-tinted dark mode with professional blue/orange color scheme");
        }

        #endregion

        #region Window Chrome Management

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_GETMINMAXINFO)
            {
                // Get the monitor the window is on
                IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

                if (monitor != IntPtr.Zero)
                {
                    // Get monitor info
                    MONITORINFO monitorInfo = new MONITORINFO();
                    NativeMethods.GetMonitorInfo(monitor, monitorInfo);

                    // Get work area (screen minus taskbar)
                    RECT workArea = monitorInfo.rcWork;
                    RECT monitorArea = monitorInfo.rcMonitor;

                    // Get the MINMAXINFO structure
                    MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));

                    // Set max position (top-left corner of work area relative to monitor)
                    mmi.ptMaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);

                    // Set max size (work area dimensions)
                    mmi.ptMaxSize.X = Math.Abs(workArea.Right - workArea.Left);
                    mmi.ptMaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);

                    // Write back to lParam
                    Marshal.StructureToPtr(mmi, lParam, true);
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        protected virtual void UpdateWindowCorners()
        {
            // CRITICAL: Update WindowChrome properties dynamically to prevent white border
            // The static XAML values (GlassFrameThickness="1", ResizeBorderThickness="6")
            // cause rendering issues during state transitions if not updated
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    // Remove chrome borders - they cause 1px white gaps around dark content
                    chrome.GlassFrameThickness = new Thickness(0);
                    chrome.ResizeBorderThickness = new Thickness(0);
                }
                else
                {
                    // Restore chrome for normal state (allows resizing and glass effect)
                    chrome.GlassFrameThickness = new Thickness(1);
                    chrome.ResizeBorderThickness = new Thickness(6);
                }
            }

            // Adjust WindowBorder for maximized vs normal state
            if (WindowBorder != null)
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    // Maximized: No rounding, no border, no margin
                    WindowBorder.CornerRadius = new CornerRadius(0);
                    WindowBorder.BorderThickness = new Thickness(0);
                    WindowBorder.Margin = new Thickness(0);
                }
                else
                {
                    // Normal: Rounded corners, subtle border, no margin
                    WindowBorder.CornerRadius = new CornerRadius(10);
                    WindowBorder.BorderThickness = new Thickness(1);
                    WindowBorder.Margin = new Thickness(0);
                }
            }

            // Update maximize button icon
            if (MaximizeIcon != null && MaximizeButton != null)
            {
                MaximizeIcon.Data = this.WindowState == WindowState.Maximized
                    ? Geometry.Parse("M4,8H8V4H20V16H16V20H4V8M16,8V14H18V6H10V8H16M6,12V18H14V12H6Z") // Restore icon
                    : Geometry.Parse("M4,4H20V20H4V4M6,8V18H18V8H6Z"); // Maximize icon

                MaximizeButton.ToolTip = this.WindowState == WindowState.Maximized ? "Restore" : "Maximize";
            }
        }

        #endregion
    }
}
