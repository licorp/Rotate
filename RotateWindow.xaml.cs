using System;
using System.Windows;

namespace RevitRotateAddin
{
    public partial class RotateWindow : Window
    {
        public double AngleInDegrees { get; private set; } = 0.0;
        public bool IsOKClicked { get; private set; } = false;

        public RotateWindow()
        {
            InitializeComponent();
            angleTextBox.Focus();
            angleTextBox.SelectAll();
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(angleTextBox.Text, out double angle))
            {
                AngleInDegrees = angle;
                IsOKClicked = true;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid number for rotation angle.\nVui lòng nhập số hợp lệ cho góc xoay.", 
                    "Input Error / Lỗi nhập liệu", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                angleTextBox.Focus();
                angleTextBox.SelectAll();
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsOKClicked = false;
            this.DialogResult = false;
            this.Close();
        }
    }
}
