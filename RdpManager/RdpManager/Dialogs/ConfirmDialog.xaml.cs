using System.Windows.Controls;

namespace RdpManager.Dialogs
{
    public partial class ConfirmDialog : System.Windows.Controls.UserControl
    {
        public ConfirmDialog(string title, string message)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
        }
    }
}