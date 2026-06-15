using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace RdpManager.Views
{
    public partial class GamesView : System.Windows.Controls.UserControl
    {
        public event EventHandler? RequestClose;

        public GamesView()
        {
            InitializeComponent();
            LoadHighScores();
        }

        private void LoadHighScores()
        {
            try
            {
                var prefsRepo = new RdpManager.Data.Repositories.PreferencesRepository();
                var snakeHigh = prefsRepo.GetInt("SnakeHighScore", 0);
                SnakeHighScore.Text = $"High Score: {snakeHigh}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading high scores: {ex.Message}");
            }
        }

        private void SnakeCard_Click(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("🐍 Starting Snake game...");

            // Create and show Snake game
            var snakeGame = new RdpManager.Controls.SnakeGame();
            snakeGame.GameOver += (s, score) =>
            {
                System.Diagnostics.Debug.WriteLine($"Snake game over! Score: {score}");

                // Update high score if beaten
                try
                {
                    var prefsRepo = new RdpManager.Data.Repositories.PreferencesRepository();
                    var currentHigh = prefsRepo.GetInt("SnakeHighScore", 0);
                    if (score > currentHigh)
                    {
                        prefsRepo.SetInt("SnakeHighScore", score);
                        MessageBox.Show($"New High Score: {score}!", "🎉 Congratulations!", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadHighScores();
                    }
                }
                catch { }

                // Show game selection again
                ShowGameSelection();
            };

            snakeGame.RequestExit += (s, e) =>
            {
                ShowGameSelection();
            };

            // Hide selection, show game
            GameSelectionPanel.Visibility = Visibility.Collapsed;
            GameContent.Content = snakeGame;
            GameContent.Visibility = Visibility.Visible;

            // Focus the game so keyboard input works (with delay to ensure it's rendered)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                snakeGame.Focus();
                Keyboard.Focus(snakeGame);
                System.Diagnostics.Debug.WriteLine($"🎮 Focused SnakeGame, IsFocused: {snakeGame.IsFocused}");
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void BackCard_Click(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("🚪 Exiting games tab");
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private void ShowGameSelection()
        {
            GameContent.Visibility = Visibility.Collapsed;
            GameContent.Content = null;
            GameSelectionPanel.Visibility = Visibility.Visible;
        }
    }
}
