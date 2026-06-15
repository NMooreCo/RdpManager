using System;
using System.Threading;
using System.Windows;
using System.Runtime.InteropServices;
using Application = System.Windows.Application;
using RdpManager.Data.Database;
using RdpManager.Data.Migration;
using MessageBox = System.Windows.MessageBox;

namespace RdpManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _mutex = null;
        private static bool _mutexOwned = false;
        private const string MutexName = "RdpManager_SingleInstance_Mutex";

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                // Try to bring it to the foreground
                BringExistingInstanceToFront();

                // Shutdown this instance (don't own the mutex, so don't try to release it)
                _mutexOwned = false;
                Application.Current.Shutdown();
                return;
            }

            // This instance owns the mutex
            _mutexOwned = true;

            // Initialize database and run migration if needed
            InitializeDatabase();

            // First instance, create and show the main window
            Window mainWindow = new ModernMainView();
            Application.Current.MainWindow = mainWindow;

            // Apply theme AFTER window is created but BEFORE it's shown
            // This ensures MaterialDesign's theme system is initialized
            LoadThemePreference();

            mainWindow.Show();
        }

        /// <summary>
        /// Load and apply saved theme preference
        /// </summary>
        private void LoadThemePreference()
        {
            try
            {
                var themeManager = RdpManager.Helpers.ThemeManager.Instance;
                var savedTheme = themeManager.GetCurrentTheme();
                System.Diagnostics.Debug.WriteLine($"Loading saved theme: {savedTheme}");
                themeManager.ApplyTheme(savedTheme);
                System.Diagnostics.Debug.WriteLine($"Theme applied successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading theme preference: {ex.Message}\n{ex.StackTrace}");
                // Continue with default theme
            }
        }

        /// <summary>
        /// Initialize database and migrate from JSON if needed
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                var db = DatabaseConnection.Instance;

                // Check if database needs to be created
                if (!db.DatabaseExists)
                {
                    // Create database schema
                    var initializer = new DatabaseInitializer();
                    initializer.Initialize();

                    // Run JSON migration
                    var migrator = new JsonMigrator();
                    var result = migrator.Migrate();

                    // Show migration summary - always show if migration ran, even if 0 items
                    string message;
                    if (result.Success)
                    {
                        if (result.TotalItemsMigrated > 0)
                        {
                            message = $"Successfully migrated data to SQLite database:\n\n" +
                                $"Computers: {result.ComputersMigrated}\n" +
                                $"Connection History: {result.ConnectionHistoryMigrated}\n" +
                                $"Preferences: {result.PreferencesMigrated}\n\n" +
                                $"Original JSON files have been backed up as .json.backup";
                        }
                        else
                        {
                            message = "Database created successfully.\n\nNo existing data found to migrate.";
                        }

                        MessageBox.Show(message, "Database Initialization", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Database created but migration failed:\n\n{result.ErrorMessage}",
                            "Migration Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // Database exists - run schema migrations if needed
                    var schemaMigrator = new RdpManager.Data.Migration.SchemaMigrator();
                    schemaMigrator.Migrate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error initializing database: {ex.Message}\n\n{ex.StackTrace}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void BringExistingInstanceToFront()
        {
            IntPtr hWnd = FindWindow(null, "RDP Manager");
            
            if (hWnd != IntPtr.Zero)
            {
                // If the window is minimized, restore it
                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }
                else
                {
                    ShowWindow(hWnd, SW_SHOW);
                }
                
                // Bring window to foreground
                SetForegroundWindow(hWnd);
            }
        }
        
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _mutex = null;
            }
        }
    }
}