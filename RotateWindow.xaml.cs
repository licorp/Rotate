using System;
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            using System.ComponentModel;
using System.Windows;
using System.Diagnostics;

namespace RevitRotateAddin
{
    public partial class RotateWindow : Window
    {
        public double AngleInDegrees { get; private set; } = 0.0;
        public bool IsOKClicked { get; private set; } = false;
        private bool _allowClose = false;
        private bool _shouldClearOnNextInput = true; // Flag to clear text on next input
        
        // Event để thông báo khi user nhấn Run
        public event EventHandler<RotationEventArgs> RotationRequested;

        public RotateWindow()
        {
            Debug.WriteLine("[RotateWindow] Constructor called");
            InitializeComponent();
            Debug.WriteLine("[RotateWindow] InitializeComponent completed");
        }

        // Method để show window (sử dụng Show() thay vì ShowDialog())
        public void ShowRotateWindow()
        {
            Debug.WriteLine("[RotateWindow] === ShowRotateWindow START ===");
            
            // Reset trạng thái CHỈ LẦN ĐẦU TIÊN
            IsOKClicked = false;
            AngleInDegrees = 0.0;
            _shouldClearOnNextInput = true; // Reset flag for new input session
            
            // KHÔNG reset text nếu user đã nhập
            if (string.IsNullOrEmpty(angleTextBox.Text))
            {
                angleTextBox.Text = "0";
                Debug.WriteLine("[RotateWindow] Set default text to '0'");
            }
            else
            {
                Debug.WriteLine($"[RotateWindow] Keeping existing text: '{angleTextBox.Text}'");
            }
            
            _allowClose = false;
            Debug.WriteLine("[RotateWindow] State reset completed");
            
            // Enable controls
            this.IsEnabled = true;
            angleTextBox.IsEnabled = true;
            angleTextBox.IsReadOnly = false;
            
            // Show window (không block)
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.BringIntoView();
            
            // Focus với delay - KHÔNG SelectAll
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                angleTextBox.Focus();
                // DON'T SelectAll here
                Debug.WriteLine("[RotateWindow] Focus set to angleTextBox (no SelectAll)");
            }), System.Windows.Threading.DispatcherPriority.Input);
            
            Debug.WriteLine("[RotateWindow] Window shown and focused");
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"[RotateWindow] btnRun_Click - Input text: '{angleTextBox.Text}'");
            
            if (double.TryParse(angleTextBox.Text, out double angle))
            {
                Debug.WriteLine($"[RotateWindow] Parsed angle: {angle}");
                AngleInDegrees = angle;
                IsOKClicked = true;
                
                // Ẩn window thay vì đóng
                Debug.WriteLine("[RotateWindow] Hiding window for processing");
                this.Hide();
                
                // Trigger event để thông báo cho main command
                Debug.WriteLine($"[RotateWindow] Triggering RotationRequested event with angle: {angle}");
                Debug.WriteLine($"[RotateWindow] Event subscribers count: {RotationRequested?.GetInvocationList()?.Length ?? 0}");
                RotationRequested?.Invoke(this, new RotationEventArgs(angle));
                Debug.WriteLine("[RotateWindow] RotationRequested event triggered");
            }
            else
            {
                Debug.WriteLine("[RotateWindow] Failed to parse angle, showing error message");
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
            Debug.WriteLine("[RotateWindow] btnCancel_Click - Closing window");
            IsOKClicked = false;
            _allowClose = true;
            this.Close(); // Chỉ đóng khi Cancel
        }

        // Event handlers để debug TextBox
        private void angleTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[RotateWindow] angleTextBox_GotFocus");
            Debug.WriteLine($"[RotateWindow] angleTextBox.IsEnabled: {angleTextBox.IsEnabled}");
            Debug.WriteLine($"[RotateWindow] angleTextBox.IsReadOnly: {angleTextBox.IsReadOnly}");
            Debug.WriteLine($"[RotateWindow] angleTextBox.Text: '{angleTextBox.Text}'");
            
            // Select all text when focused so user can type to replace
            angleTextBox.SelectAll();
            _shouldClearOnNextInput = true;
            Debug.WriteLine("[RotateWindow] Focus restored to angleTextBox");
        }

        private void angleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[RotateWindow] angleTextBox_LostFocus");
            Debug.WriteLine($"[RotateWindow] Final text: '{angleTextBox.Text}'");
        }

        private void angleTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Debug.WriteLine($"[RotateWindow] angleTextBox_TextChanged: '{angleTextBox.Text}'");
        }

        private void angleTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Debug.WriteLine($"[RotateWindow] angleTextBox_KeyDown: {e.Key}");
            Debug.WriteLine($"[RotateWindow] Current text before key: '{angleTextBox.Text}'");
            
            // Force enable if needed
            if (!angleTextBox.IsEnabled)
            {
                angleTextBox.IsEnabled = true;
                Debug.WriteLine("[RotateWindow] Force enabled angleTextBox");
            }
            
            if (angleTextBox.IsReadOnly)
            {
                angleTextBox.IsReadOnly = false;
                Debug.WriteLine("[RotateWindow] Force set angleTextBox not readonly");
            }
            
            // Allow Enter to trigger Run
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                btnRun_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            
            // Manual handling for numeric keys
            string keyChar = "";
            bool isValidKey = false;
            
            // Handle regular number keys
            if (e.Key >= System.Windows.Input.Key.D0 && e.Key <= System.Windows.Input.Key.D9)
            {
                keyChar = ((int)(e.Key - System.Windows.Input.Key.D0)).ToString();
                isValidKey = true;
            }
            // Handle NumPad keys
            else if (e.Key >= System.Windows.Input.Key.NumPad0 && e.Key <= System.Windows.Input.Key.NumPad9)
            {
                keyChar = ((int)(e.Key - System.Windows.Input.Key.NumPad0)).ToString();
                isValidKey = true;
            }
            // Handle minus sign
            else if (e.Key == System.Windows.Input.Key.OemMinus || e.Key == System.Windows.Input.Key.Subtract)
            {
                keyChar = "-";
                isValidKey = true;
            }
            // Handle decimal point
            else if (e.Key == System.Windows.Input.Key.OemPeriod || e.Key == System.Windows.Input.Key.Decimal)
            {
                keyChar = ".";
                isValidKey = true;
            }
            // Handle Backspace
            else if (e.Key == System.Windows.Input.Key.Back)
            {
                _shouldClearOnNextInput = false; // Reset flag when editing
                if (angleTextBox.Text.Length > 0)
                {
                    int caretIndex = angleTextBox.CaretIndex;
                    if (caretIndex > 0)
                    {
                        string newText = angleTextBox.Text.Remove(caretIndex - 1, 1);
                        angleTextBox.Text = newText;
                        angleTextBox.CaretIndex = caretIndex - 1;
                        Debug.WriteLine($"[RotateWindow] Manual Backspace: new text = '{angleTextBox.Text}'");
                    }
                }
                e.Handled = true;
                return;
            }
            // Handle Delete
            else if (e.Key == System.Windows.Input.Key.Delete)
            {
                _shouldClearOnNextInput = false; // Reset flag when editing
                if (angleTextBox.Text.Length > 0)
                {
                    int caretIndex = angleTextBox.CaretIndex;
                    if (caretIndex < angleTextBox.Text.Length)
                    {
                        string newText = angleTextBox.Text.Remove(caretIndex, 1);
                        angleTextBox.Text = newText;
                        angleTextBox.CaretIndex = caretIndex;
                        Debug.WriteLine($"[RotateWindow] Manual Delete: new text = '{angleTextBox.Text}'");
                    }
                }
                e.Handled = true;
                return;
            }
            
            // Manual text insertion for valid keys
            if (isValidKey)
            {
                // Clear text on first input if flag is set
                if (_shouldClearOnNextInput)
                {
                    angleTextBox.Text = "";
                    angleTextBox.CaretIndex = 0;
                    _shouldClearOnNextInput = false;
                    Debug.WriteLine("[RotateWindow] Cleared text for new input");
                }
                
                int caretIndex = angleTextBox.CaretIndex;
                string currentText = angleTextBox.Text;
                string newText = currentText.Insert(caretIndex, keyChar);
                
                angleTextBox.Text = newText;
                angleTextBox.CaretIndex = caretIndex + 1;
                
                Debug.WriteLine($"[RotateWindow] Manual key insert '{keyChar}': new text = '{angleTextBox.Text}'");
                e.Handled = true;
                return;
            }
            
            // For other keys, let default processing handle it
            e.Handled = false;
            
            // Use Dispatcher to check text after key processing
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                Debug.WriteLine($"[RotateWindow] Text after KeyDown processing: '{angleTextBox.Text}'");
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        // Method để hiện lại form sau khi hoàn thành xử lý
        public void ShowAgainAfterProcessing()
        {
            Debug.WriteLine("[RotateWindow] ShowAgainAfterProcessing - Showing window again");
            
            // Enable controls
            this.IsEnabled = true;
            angleTextBox.IsEnabled = true;
            angleTextBox.IsReadOnly = false;
            
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.BringIntoView();
            
            // Focus với delay
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                angleTextBox.Focus();
                angleTextBox.SelectAll();
                Debug.WriteLine("[RotateWindow] Focus restored to angleTextBox");
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        // Override để ngăn đóng window khi nhấn X (trừ khi nhấn Cancel)
        protected override void OnClosing(CancelEventArgs e)
        {
            Debug.WriteLine($"[RotateWindow] OnClosing - AllowClose: {_allowClose}");
            
            if (!_allowClose)
            {
                Debug.WriteLine("[RotateWindow] Cancelling close, hiding instead");
                e.Cancel = true; // Ngăn đóng
                this.Hide(); // Chỉ ẩn
            }
            else
            {
                Debug.WriteLine("[RotateWindow] Allowing close");
            }
        }
    }

    // Event args cho rotation request
    public class RotationEventArgs : EventArgs
    {
        public double AngleInDegrees { get; }
        
        public RotationEventArgs(double angleInDegrees)
        {
            AngleInDegrees = angleInDegrees;
        }
    }
}
