using MaterialDesignThemes.Wpf;
using RdpManager.Data.Repositories;

namespace RdpManager.Helpers
{
    public class ThemeManager
    {
        private static ThemeManager? _instance;
        private readonly PreferencesRepository _preferencesRepo;

        public static ThemeManager Instance
        {
            get
            {
                _instance ??= new ThemeManager();
                return _instance;
            }
        }

        private ThemeManager()
        {
            _preferencesRepo = new PreferencesRepository();
        }

        public string GetCurrentTheme()
        {
            var theme = _preferencesRepo.GetString("ThemeMode", "Dark");
            System.Diagnostics.Debug.WriteLine($"GetCurrentTheme() = {theme}");
            return theme;
        }

        public bool IsDarkMode()
        {
            return GetCurrentTheme() == "Dark";
        }

        public void ToggleTheme()
        {
            var currentTheme = GetCurrentTheme();
            var newTheme = currentTheme == "Dark" ? "Light" : "Dark";
            ApplyTheme(newTheme);
        }

        public void ApplyTheme(string theme)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyTheme({theme}) called");

            _preferencesRepo.Set("ThemeMode", theme, "string");

            var resources = System.Windows.Application.Current.Resources;

            // Update MaterialDesign theme via PaletteHelper (the correct MD 5.x API)
            UpdateMaterialDesignTheme(theme);

            // Update our custom App.* colors
            if (theme == "Dark")
                ApplyDarkTheme(resources);
            else
                ApplyLightTheme(resources);

            System.Diagnostics.Debug.WriteLine("Theme applied successfully");
        }

        private void UpdateMaterialDesignTheme(string theme)
        {
            try
            {
                // PaletteHelper is the correct way to switch MaterialDesign themes at runtime.
                // Directly setting BundledTheme.BaseTheme does NOT propagate to all MD controls.
                var paletteHelper = new MaterialDesignThemes.Wpf.PaletteHelper();
                var mdTheme = paletteHelper.GetTheme();

                mdTheme.SetBaseTheme(theme == "Dark"
                    ? MaterialDesignThemes.Wpf.BaseTheme.Dark
                    : MaterialDesignThemes.Wpf.BaseTheme.Light);

                paletteHelper.SetTheme(mdTheme);
                System.Diagnostics.Debug.WriteLine($"Updated MaterialDesign theme via PaletteHelper to {theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PaletteHelper failed: {ex.Message}, trying BundledTheme fallback");

                // Fallback: direct BundledTheme update
                try
                {
                    var bundledTheme = FindBundledTheme(System.Windows.Application.Current.Resources);
                    if (bundledTheme != null)
                    {
                        bundledTheme.BaseTheme = theme == "Dark"
                            ? MaterialDesignThemes.Wpf.BaseTheme.Dark
                            : MaterialDesignThemes.Wpf.BaseTheme.Light;
                    }
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"BundledTheme fallback also failed: {ex2.Message}");
                }
            }
        }

        private MaterialDesignThemes.Wpf.BundledTheme? FindBundledTheme(System.Windows.ResourceDictionary dict)
        {
            if (dict is MaterialDesignThemes.Wpf.BundledTheme bundledTheme)
                return bundledTheme;

            foreach (var merged in dict.MergedDictionaries)
            {
                var found = FindBundledTheme(merged);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void ApplyDarkTheme(System.Windows.ResourceDictionary resources)
        {
            UpdateResource(resources, "App.Background.Window", "#0A1929");
            UpdateResource(resources, "App.Background.Surface", "#1E2A3A");
            UpdateResource(resources, "App.Background.Elevated", "#2D3E50");

            UpdateResource(resources, "App.Header.Background", "#2D3E50");
            UpdateResource(resources, "App.Header.Foreground", "#B0BEC5");

            UpdateResource(resources, "App.Primary", "#64B5F6");
            UpdateResource(resources, "App.Primary.Foreground", "#000000");
            UpdateResource(resources, "App.Primary.Lighter", "#9BE7FF");
            UpdateResource(resources, "App.Primary.Darker", "#2286C3");

            UpdateResource(resources, "App.Secondary", "#FFB74D");
            UpdateResource(resources, "App.Secondary.Foreground", "#000000");

            UpdateResource(resources, "App.Text.Primary", "#FFFFFF");
            UpdateResource(resources, "App.Text.Secondary", "#B0BEC5");

            UpdateResource(resources, "App.Border.Divider", "#37474F");
            UpdateResource(resources, "App.Border.Outline", "#546E7A");

            UpdateResource(resources, "App.Hover", "#2D3E50");
            UpdateResource(resources, "App.Selection", "#1E3A5F");
        }

        private void ApplyLightTheme(System.Windows.ResourceDictionary resources)
        {
            UpdateResource(resources, "App.Background.Window", "#FAFAFA");
            UpdateResource(resources, "App.Background.Surface", "#FFFFFF");
            UpdateResource(resources, "App.Background.Elevated", "#F5F5F5");

            UpdateResource(resources, "App.Header.Background", "#37474F");
            UpdateResource(resources, "App.Header.Foreground", "#FFFFFF");

            UpdateResource(resources, "App.Primary", "#1976D2");
            UpdateResource(resources, "App.Primary.Foreground", "#FFFFFF");
            UpdateResource(resources, "App.Primary.Lighter", "#42A5F5");
            UpdateResource(resources, "App.Primary.Darker", "#1565C0");

            UpdateResource(resources, "App.Secondary", "#FF6F00");
            UpdateResource(resources, "App.Secondary.Foreground", "#FFFFFF");

            UpdateResource(resources, "App.Text.Primary", "#212121");
            UpdateResource(resources, "App.Text.Secondary", "#757575");

            UpdateResource(resources, "App.Border.Divider", "#E0E0E0");
            UpdateResource(resources, "App.Border.Outline", "#BDBDBD");

            UpdateResource(resources, "App.Hover", "#F9FAFB");
            UpdateResource(resources, "App.Selection", "#F0F0F0");
        }

        private void UpdateResource(System.Windows.ResourceDictionary resources, string key, string colorHex)
        {
            var brush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));
            brush.Freeze(); // Frozen brushes are thread-safe and work in popup windows
            resources[key] = brush;
        }
    }
}
