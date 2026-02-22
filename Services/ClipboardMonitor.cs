using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipboardManager.Services
{
    public class ClipboardMonitor : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private IntPtr _windowHandle;
        private HwndSource _source;

        public event EventHandler<string> ClipboardTextChanged;

        public void Initialize(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(HwndHook);
            AddClipboardFormatListener(_windowHandle);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardUpdate();
            }
            return IntPtr.Zero;
        }

        private void OnClipboardUpdate()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        ClipboardTextChanged?.Invoke(this, text);
                    }
                }
            }
            catch (Exception ex)
            {
                // Clipboard exceptions often happen due to other applications holding locks
                Console.WriteLine("Clipboard error: " + ex.Message);
            }
        }

        public void Dispose()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                RemoveClipboardFormatListener(_windowHandle);
            }
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source = null;
            }
        }
    }
}
