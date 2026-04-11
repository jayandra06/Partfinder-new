using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;

namespace PartFinder
{
    public sealed partial class MainWindow : Window
    {
        private int _step = 1;
        private bool _isCustomDbConnectionSuccessful;
        private readonly string _setupFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder",
            "setup-state.json");

        public MainWindow()
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            BackButton.IsEnabled = false;
            if (IsSetupCompleted())
            {
                ShowShell();
                return;
            }
            UpdateStepUi();
        }

        private bool IsSetupCompleted()
        {
            try
            {
                if (!File.Exists(_setupFilePath))
                {
                    return false;
                }

                var json = File.ReadAllText(_setupFilePath);
                var state = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                return state is not null && state.ContainsKey("completed");
            }
            catch
            {
                return false;
            }
        }

        private void SaveSetup()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_setupFilePath)!);
            var state = new
            {
                completed = true,
                orgCode = OrgCodeBox.Text,
                databaseMode = DatabaseModeButtons.SelectedIndex == 1 ? "Custom" : "Default",
                adminName = AdminNameBox.Text,
                adminEmail = AdminEmailBox.Text
            };
            File.WriteAllText(_setupFilePath, JsonSerializer.Serialize(state));
        }

        private void ShowShell()
        {
            SetupRoot.Visibility = Visibility.Collapsed;
            ShellRoot.Visibility = Visibility.Visible;
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            if (_step > 1)
            {
                _step--;
                UpdateStepUi();
            }
        }

        private void OnNextClicked(object sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;

            if (_step == 1 && string.IsNullOrWhiteSpace(OrgCodeBox.Text))
            {
                ValidationText.Text = "Please enter organization code.";
                return;
            }

            if (_step == 3)
            {
                if (string.IsNullOrWhiteSpace(AdminNameBox.Text) || string.IsNullOrWhiteSpace(AdminEmailBox.Text) || string.IsNullOrWhiteSpace(AdminPasswordBox.Password))
                {
                    ValidationText.Text = "Name, Email ID and Password are required.";
                    return;
                }

                if (!IsValidEmail(AdminEmailBox.Text))
                {
                    ValidationText.Text = "Please enter a valid Email ID.";
                    return;
                }

                if (!IsStrongPassword(AdminPasswordBox.Password))
                {
                    ValidationText.Text = "Password must be at least 8 chars with upper, lower, and number.";
                    return;
                }

                SaveSetup();
            }

            if (_step < 4)
            {
                _step++;
                UpdateStepUi();
            }
        }

        private void OnLetsGoClicked(object sender, RoutedEventArgs e)
        {
            ShowShell();
        }

        private void OnDatabaseModeChanged(object sender, SelectionChangedEventArgs e)
        {
            var isCustom = DatabaseModeButtons.SelectedIndex == 1;
            CustomDbPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

            if (!isCustom)
            {
                _isCustomDbConnectionSuccessful = true;
                TestConnectionButton.Visibility = Visibility.Visible;
                ConnectionSuccessText.Visibility = Visibility.Collapsed;
                ConnectionSuccessText.Text = "DB connection successful.";
            }
            else
            {
                _isCustomDbConnectionSuccessful = false;
                TestConnectionButton.Visibility = Visibility.Visible;
                ConnectionSuccessText.Visibility = Visibility.Collapsed;
            }

            if (_step == 2)
            {
                NextButton.IsEnabled = !isCustom || _isCustomDbConnectionSuccessful;
            }
        }

        private void OnTestConnectionClicked(object sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(MongoUriBox.Text))
            {
                ValidationText.Text = "Please paste MongoDB URI first.";
                return;
            }

            // Placeholder validation for now; replace with real ping later.
            _isCustomDbConnectionSuccessful = true;
            TestConnectionButton.Visibility = Visibility.Collapsed;
            ConnectionSuccessText.Visibility = Visibility.Visible;
            ConnectionSuccessText.Text = "DB connection successful.";
            NextButton.IsEnabled = true;
        }

        private void OnAdminPasswordChanged(object sender, RoutedEventArgs e)
        {
            var isStrong = IsStrongPassword(AdminPasswordBox.Password);
            PasswordHintText.Text = isStrong
                ? "Password strength: strong"
                : "Password must be at least 8 characters and include upper, lower, and a number.";
            PasswordHintText.Foreground = isStrong
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 163, 74))
                : (Brush)Application.Current.Resources["AppSubtleTextBrush"];
        }

        private void UpdateStepUi()
        {
            Step1Panel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;
            NavigationRow.Visibility = _step == 4 ? Visibility.Collapsed : Visibility.Visible;
            BackButton.IsEnabled = _step > 1;
            if (_step == 2)
            {
                var isCustom = DatabaseModeButtons.SelectedIndex == 1;
                CustomDbPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
                if (!isCustom)
                {
                    _isCustomDbConnectionSuccessful = true;
                }

                NextButton.IsEnabled = !isCustom || _isCustomDbConnectionSuccessful;
            }
            else
            {
                NextButton.IsEnabled = true;
            }

            StepTitle.Text = _step switch
            {
                1 => "Step 1 of 4: Organization",
                2 => "Step 2 of 4: Database",
                3 => "Step 3 of 4: Create Admin",
                _ => "Step 4 of 4: Completed"
            };
            ProgressText.Text = _step switch
            {
                1 => "● ○ ○ ○",
                2 => "● ● ○ ○",
                3 => "● ● ● ○",
                _ => "● ● ● ●"
            };
            NextButton.Content = _step == 3 ? "Create Admin User" : "Next";
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var at = email.IndexOf('@');
            var dot = email.LastIndexOf('.');
            return at > 0 && dot > at + 1 && dot < email.Length - 1;
        }

        private static bool IsStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                return false;
            }

            var hasUpper = password.Any(char.IsUpper);
            var hasLower = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            return hasUpper && hasLower && hasDigit;
        }
    }
}
