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
        public static readonly DependencyProperty ShowTimeEnabledProperty =
            DependencyProperty.Register("ShowTimeEnabled", typeof(bool), typeof(MainWindow), new PropertyMetadata(false, OnShowTimeEnabledChanged));

        public bool ShowTimeEnabled
        {
            get { return (bool)GetValue(ShowTimeEnabledProperty); }
            set { SetValue(ShowTimeEnabledProperty, value); }
        }

        private static void OnShowTimeEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MainWindow window)
            {
                SettingsService.Instance.Settings.ShowTime = (bool)e.NewValue;
                SettingsService.Instance.Save();
            }
        }

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

            ShowTimeEnabled = SettingsService.Instance.Settings.ShowTime;
            ShowTimeToggle.IsChecked = ShowTimeEnabled;
            LimitBox.Text = SettingsService.Instance.Settings.RecordLimit.ToString();
            UpdateCounts();
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
            this.Height = workArea.Height / 2;
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

            if (_items.Count > 0)
            {
                ClipboardList.SelectedIndex = 0;
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    ClipboardList.UpdateLayout();
                    FocusItemAt(0);
                }));
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
                else
                {
                    UpdateCounts();
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
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            int total = _dbService.GetTotalCount();
            int limit = SettingsService.Instance.Settings.RecordLimit;
            int unused = Math.Max(0, limit - total);
            
            UsedCountText.Text = total.ToString();
            UnusedCountText.Text = unused.ToString();
        }

        private void LimitBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyLimit();
        }

        private void LimitBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyLimit();
                e.Handled = true;
            }
        }

        private void ApplyLimit()
        {
            if (int.TryParse(LimitBox.Text, out int newLimit) && newLimit > 0)
            {
                SettingsService.Instance.Settings.RecordLimit = newLimit;
                SettingsService.Instance.Save();
                UpdateCounts();
            }
            else
            {
                LimitBox.Text = SettingsService.Instance.Settings.RecordLimit.ToString();
            }
        }

        private void ShowTimeToggle_Checked(object sender, RoutedEventArgs e)
        {
            ShowTimeEnabled = true;
        }

        private void ShowTimeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            ShowTimeEnabled = false;
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
                FocusSelectedListItem();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ProcessSelection();
                e.Handled = true;
            }
        }

        private void FocusSelectedListItem()
        {
            if (ClipboardList.Items.Count == 0) return;
            int nextIndex = ClipboardList.SelectedIndex < 0
                ? 0
                : Math.Min(ClipboardList.SelectedIndex + 1, ClipboardList.Items.Count - 1);
            ClipboardList.SelectedIndex = nextIndex;
            FocusItemAt(nextIndex);
        }

        private void FocusItemAt(int index)
        {
            var container = ClipboardList.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
            container?.Focus();
        }

        private void Window_PreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            if (SearchBox.IsFocused) return;
            SearchBox.Focus();
            SearchBox.AppendText(e.Text);
            SearchBox.CaretIndex = SearchBox.Text.Length;
            e.Handled = true;
        }

        private void ClipboardList_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ProcessSelection();
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