using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ClipboardManager.Services
{
    public class HotkeyService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;

        private const uint MOD_ALT = 0x0001;
        private const uint VK_V = 0x56;

        private IntPtr _windowHandle;
        private HwndSource? _source;
        public Action? OnHotkeyActivated { get; set; }

        public void Initialize(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(HwndHook);

            if (!RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_ALT, VK_V))
            {
                Console.WriteLine("Failed to register hotkey.");
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                OnHotkeyActivated?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
            }
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source = null;
            }
        }
    }
}
