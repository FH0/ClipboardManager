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
        
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        private bool _isPasting = false;
        private DateTime _lastDeactivatedTime;

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

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            _hotkeyService.Initialize(hwnd);
            _clipboardMonitor.Initialize(hwnd);
            
            SetupWindowSizeAndPosition();
            SetupTrayIcon();

            LoadItems();
            HideWindow();
        }

        private void SetupWindowSizeAndPosition()
        {
            var workArea = SystemParameters.WorkArea;
            this.Width = workArea.Width / 3;
            this.Height = workArea.Height / 3;
            this.Left = workArea.Left + (workArea.Width - this.Width) / 2;
            this.Top = workArea.Top + (workArea.Height - this.Height) / 2;
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            try {
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            } catch {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Clipboard Manager";
            
            _notifyIcon.MouseClick += OnTrayIconMouseClick;

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exitItem.Click += OnExitClicked;
            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void OnTrayIconMouseClick(object? s, System.Windows.Forms.MouseEventArgs args)
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (this.Visibility == Visibility.Visible)
                {
                    HideWindow();
                }
                else
                {
                    if ((DateTime.Now - _lastDeactivatedTime).TotalMilliseconds < 200)
                    {
                        return;
                    }
                    ShowWindow();
                    this.Activate();
                }
            }
        }

        private void OnExitClicked(object? s, EventArgs args)
        {
            if (_notifyIcon != null)
                _notifyIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
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

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            _lastDeactivatedTime = DateTime.Now;
            HideWindow();
        }

        private void OnClipboardTextChanged(object? sender, string text)
        {
            if (_isPasting) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
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

        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            LoadItems(SearchBox.Text);
            if (_items.Count > 0)
            {
                ClipboardList.SelectedIndex = 0;
            }
        }

        private void SearchBox_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
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

        private void ClipboardList_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
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

        private void ClipboardList_MouseDoubleClick(object? sender, MouseButtonEventArgs e)
        {
            ProcessSelection();
        }

        private void Window_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideWindow();
                e.Handled = true;
            }
        }

        private void ProcessSelection()
        {
            if (ClipboardList.SelectedItem is ClipboardItem selectedItem
                && selectedItem.Content is string content)
            {
                _isPasting = true;
                System.Windows.Clipboard.SetText(content);
                _dbService.AddOrUpdateItem(content);
                HideWindow();
                
                System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => 
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnClosed(e);
        }
    }
}