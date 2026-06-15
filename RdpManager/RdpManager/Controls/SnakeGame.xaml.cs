using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RdpManager.Controls
{
    public partial class SnakeGame : System.Windows.Controls.UserControl
    {
        private const int GridSize = 20;
        private const int CellSize = 20;
        private const int InitialSpeed = 150; // ms

        private DispatcherTimer _gameTimer;
        private List<System.Windows.Point> _snake;
        private System.Windows.Point _food;
        private Direction _currentDirection;
        private Direction _nextDirection;
        private int _score;
        private bool _isGameOver;
        private Random _random;

        public event EventHandler<int>? GameOver;
        public event EventHandler? RequestExit;

        private enum Direction
        {
            Up,
            Down,
            Left,
            Right
        }

        public SnakeGame()
        {
            InitializeComponent();

            _random = new Random();
            _gameTimer = new DispatcherTimer();
            _gameTimer.Interval = TimeSpan.FromMilliseconds(InitialSpeed);
            _gameTimer.Tick += GameTimer_Tick;

            // Ensure control is focusable
            Focusable = true;

            Loaded += (s, e) =>
            {
                // Set focus with delay to ensure control is fully loaded
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Focus();
                    Keyboard.Focus(this);
                    System.Diagnostics.Debug.WriteLine($"🎮 SnakeGame focused, HasFocus: {IsFocused}, IsKeyboardFocused: {IsKeyboardFocused}");
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                StartNewGame();
            };

            // Capture focus on any mouse activity
            MouseEnter += (s, e) => CaptureFocus();
            MouseMove += (s, e) =>
            {
                if (!IsKeyboardFocused)
                {
                    CaptureFocus();
                }
            };
        }

        private void UserControl_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("🖱️ Game clicked - capturing focus");
            CaptureFocus();
        }

        private void CaptureFocus()
        {
            if (!IsKeyboardFocused)
            {
                Focus();
                Keyboard.Focus(this);
                System.Diagnostics.Debug.WriteLine($"🎯 Focus captured - IsKeyboardFocused: {IsKeyboardFocused}");
            }
        }

        private void StartNewGame()
        {
            _snake = new List<System.Windows.Point> { new System.Windows.Point(10, 10), new System.Windows.Point(9, 10), new System.Windows.Point(8, 10) };
            _currentDirection = Direction.Right;
            _nextDirection = Direction.Right;
            _score = 0;
            _isGameOver = false;

            ScoreText.Text = "0";
            GameOverPanel.Visibility = Visibility.Collapsed;

            SpawnFood();
            DrawGame();
            _gameTimer.Start();

            System.Diagnostics.Debug.WriteLine("🐍 Snake game started!");
        }

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            if (_isGameOver) return;

            // Update direction
            _currentDirection = _nextDirection;

            // Calculate new head position
            var head = _snake[0];
            System.Windows.Point newHead = _currentDirection switch
            {
                Direction.Up => new System.Windows.Point(head.X, head.Y - 1),
                Direction.Down => new System.Windows.Point(head.X, head.Y + 1),
                Direction.Left => new System.Windows.Point(head.X - 1, head.Y),
                Direction.Right => new System.Windows.Point(head.X + 1, head.Y),
                _ => head
            };

            // Check collisions
            if (newHead.X < 0 || newHead.X >= GridSize ||
                newHead.Y < 0 || newHead.Y >= GridSize ||
                _snake.Any(segment => segment == newHead))
            {
                EndGame();
                return;
            }

            // Move snake
            _snake.Insert(0, newHead);

            // Check if food eaten
            if (newHead == _food)
            {
                _score += 10;
                ScoreText.Text = _score.ToString();
                SpawnFood();

                // Speed up slightly
                if (_score % 50 == 0 && _gameTimer.Interval.TotalMilliseconds > 50)
                {
                    _gameTimer.Interval = TimeSpan.FromMilliseconds(_gameTimer.Interval.TotalMilliseconds - 10);
                }
            }
            else
            {
                // Remove tail if no food eaten
                _snake.RemoveAt(_snake.Count - 1);
            }

            DrawGame();
        }

        private void SpawnFood()
        {
            do
            {
                _food = new System.Windows.Point(_random.Next(GridSize), _random.Next(GridSize));
            }
            while (_snake.Any(segment => segment == _food));
        }

        private void DrawGame()
        {
            GameCanvas.Children.Clear();

            // Draw snake
            for (int i = 0; i < _snake.Count; i++)
            {
                var segment = _snake[i];
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = CellSize - 2,
                    Height = CellSize - 2,
                    Fill = i == 0
                        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 181, 246)) // Head - lighter blue
                        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 118, 210))  // Body - primary blue
                };

                Canvas.SetLeft(rect, segment.X * CellSize + 1);
                Canvas.SetTop(rect, segment.Y * CellSize + 1);
                GameCanvas.Children.Add(rect);
            }

            // Draw food
            var food = new Ellipse
            {
                Width = CellSize - 4,
                Height = CellSize - 4,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)) // Green
            };
            Canvas.SetLeft(food, _food.X * CellSize + 2);
            Canvas.SetTop(food, _food.Y * CellSize + 2);
            GameCanvas.Children.Add(food);
        }

        private void EndGame()
        {
            _isGameOver = true;
            _gameTimer.Stop();

            FinalScoreText.Text = $"Score: {_score}";
            GameOverPanel.Visibility = Visibility.Visible;

            System.Diagnostics.Debug.WriteLine($"🐍 Game Over! Final score: {_score}");

            GameOver?.Invoke(this, _score);
        }

        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"🎮 Key pressed: {e.Key}");

            if (_isGameOver) return;

            bool handled = false;

            // Prevent reversing direction (can't go back on yourself)
            switch (e.Key)
            {
                case Key.Up when _currentDirection != Direction.Down:
                    _nextDirection = Direction.Up;
                    handled = true;
                    System.Diagnostics.Debug.WriteLine("  → Direction changed to Up");
                    break;
                case Key.Down when _currentDirection != Direction.Up:
                    _nextDirection = Direction.Down;
                    handled = true;
                    System.Diagnostics.Debug.WriteLine("  → Direction changed to Down");
                    break;
                case Key.Left when _currentDirection != Direction.Right:
                    _nextDirection = Direction.Left;
                    handled = true;
                    System.Diagnostics.Debug.WriteLine("  → Direction changed to Left");
                    break;
                case Key.Right when _currentDirection != Direction.Left:
                    _nextDirection = Direction.Right;
                    handled = true;
                    System.Diagnostics.Debug.WriteLine("  → Direction changed to Right");
                    break;
                case Key.Escape:
                    _gameTimer.Stop();
                    RequestExit?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;
            }

            // Mark event as handled to prevent parent controls from processing it
            if (handled)
            {
                e.Handled = true;
            }
        }

        private void PlayAgainButton_Click(object sender, RoutedEventArgs e)
        {
            StartNewGame();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            _gameTimer.Stop();
            RequestExit?.Invoke(this, EventArgs.Empty);
        }
    }
}
