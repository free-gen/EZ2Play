using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace EZParser.Views
{
    public partial class ApiKeyDialogPage : UserControl
    {
        public string ApiKey => ApiKeyPasswordBox.Password;

        private ContentDialog _parentDialog;

        public ApiKeyDialogPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApiKeyPasswordBox.Focus();
            UpdatePrimaryButtonState();
        }

        private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePrimaryButtonState();
        }

        public void SetParentDialog(ContentDialog dialog)
        {
            _parentDialog = dialog;
            UpdatePrimaryButtonState();
        }

        private void UpdatePrimaryButtonState()
        {
            if (_parentDialog != null)
            {
                _parentDialog.IsPrimaryButtonEnabled = 
                    !string.IsNullOrWhiteSpace(ApiKeyPasswordBox.Password);
            }
        }
    }
}