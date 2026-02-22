using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ClipboardManager.Services;

namespace ClipboardManager
{
    public partial class MainWindow : Window
    {
        private DatabaseService _dbService;
        private HotkeyService _hotkeyService;
        private ClipboardMonitor _clipboardMonitor;
        private ObservableCollection<ClipboardItem> _items;
        
        // Prevent recursive clipboard updates when we copy to paste
        private bool _isPasting = false;

        public MainWindow()
        {
            InitializeComponent();
            _items = new ObservableCollection<ClipboardItem>();
            ClipboardList.ItemsSource = _items;

            _dbService = new DatabaseService();
            _hotkeyService = new HotkeyService();
            _clipboardMonitor = new ClipboardMonitor();

            _hotkeyService.OnHotkeyActivated = ToggleWindow;
            _clipboardMonitor.ClipboardTextChanged += OnClipboardTextChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            _hotkeyService.Initialize(hwnd);
            _clipboardMonitor.Initialize(hwnd);
            
            LoadItems();
            HideWindow(); // Start hidden
        }

        private void ToggleWindow()
        {
            if (this.Visibility == Visibility.Visible)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }

        private void ShowWindow()
        {
            LoadItems();
            SearchBox.Text = string.Empty;
            this.Visibility = Visibility.Visible;
            this.Activate();
            SearchBox.Focus();
            
            if (_items.Count > 0)
            {
                ClipboardList.SelectedIndex = 0;
            }
        }

        private void HideWindow()
        {
            this.Visibility = Visibility.Hidden;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            HideWindow();
        }

        private void OnClipboardTextChanged(object sender, string text)
        {
            if (_isPasting) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                _dbService.AddOrUpdateItem(text);
                if (this.Visibility == Visibility.Visible)
                {
                    LoadItems(SearchBox.Text);
                }
            });
        }

        private void LoadItems(string searchQuery = "")
        {
            _items.Clear();
            var items = _dbService.GetItems(searchQuery);
            foreach (var item in items)
            {
                _items.Add(item);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadItems(SearchBox.Text);
            if (_items.Count > 0)
            {
                ClipboardList.SelectedIndex = 0;
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                ClipboardList.Focus();
                if (ClipboardList.Items.Count > 0 && ClipboardList.SelectedIndex < 0)
                {
                    ClipboardList.SelectedIndex = 0;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ProcessSelection();
                e.Handled = true;
            }
        }

        private void ClipboardList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ProcessSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && ClipboardList.SelectedIndex <= 0)
            {
                SearchBox.Focus();
                e.Handled = true;
            }
        }

        private void ClipboardList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ProcessSelection();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideWindow();
                e.Handled = true;
            }
        }

        private void ProcessSelection()
        {
            if (ClipboardList.SelectedItem is ClipboardItem selectedItem)
            {
                _isPasting = true;
                Clipboard.SetText(selectedItem.Content);
                _dbService.AddOrUpdateItem(selectedItem.Content); // Move to top
                HideWindow();
                
                // Small delay to ensure DB and Window hiding completes
                System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => 
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PasteAction.SimulateCtrlV();
                        _isPasting = false;
                    });
                });
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _hotkeyService.Dispose();
            _clipboardMonitor.Dispose();
            base.OnClosed(e);
        }
    }
}