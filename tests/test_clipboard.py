import time
import subprocess
import os
import uiautomation as auto
import pytest
import win32gui
import win32ui
import win32con
from ctypes import windll

def take_print_window_screenshot(hwnd, filename):
    """Takes a process-level screenshot using PrintWindow, saving directly as a BMP."""
    try:
        # Get window bounds
        left, top, right, bottom = win32gui.GetWindowRect(hwnd)
        width = right - left
        height = bottom - top

        # Create device context
        hwndDC = win32gui.GetWindowDC(hwnd)
        mfcDC  = win32ui.CreateDCFromHandle(hwndDC)
        saveDC = mfcDC.CreateCompatibleDC()

        # Create bitmap object
        saveBitMap = win32ui.CreateBitmap()
        saveBitMap.CreateCompatibleBitmap(mfcDC, width, height)

        saveDC.SelectObject(saveBitMap)

        # PrintWindow flag 3 enables capturing hardware accelerated/layered windows (PW_CLIENTONLY | PW_RENDERFULLCONTENT)
        # Note: In Windows 8.1+, PW_RENDERFULLCONTENT (2) helps capture composed windows.
        result = windll.user32.PrintWindow(hwnd, saveDC.GetSafeHdc(), 3)
        
        if result == 1:
            saveBitMap.SaveBitmapFile(saveDC, filename)
            print(f"Screenshot saved to {filename}")
        else:
            print(f"PrintWindow failed. Result: {result}")

    except Exception as e:
        print(f"Screenshot error: {e}")
    finally:
        # Clean up resources
        if 'saveDC' in locals():
            saveDC.DeleteDC()
        if 'mfcDC' in locals():
            mfcDC.DeleteDC()
        if 'hwndDC' in locals():
            win32gui.ReleaseDC(hwnd, hwndDC)
        if 'saveBitMap' in locals():
            win32gui.DeleteObject(saveBitMap.GetHandle())

@pytest.fixture(scope="module")
def app_runner():
    # from tests/ run, the relative path to bin is ../bin/...
    app_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "bin", "Debug", "net8.0-windows", "ClipboardManager.exe"))
    
    if not os.path.exists(app_path):
        pytest.fail(f"Could not find {app_path}. Did you build the project?")
        
    process = subprocess.Popen([app_path])
    time.sleep(2) # Wait for app to initialize hidden window
    yield process
    
    # Cleanup
    process.terminate()
    try:
        process.wait(timeout=2)
    except subprocess.TimeoutExpired:
        process.kill()

def test_clipboard_manager_ui(app_runner):
    # 1. Simulate copying text
    test_text = "Hello UIAutomation Testing!"
    auto.SetClipboardText(test_text)
    time.sleep(0.5)
    
    # 2. Open via hotkey Alt + V
    auto.SendKeys('{Alt}v')
    time.sleep(1) # Wait for window animation/rendering
    
    # 3. Find the main window
    window = auto.WindowControl(Name="Clipboard Manager")
    assert window.Exists(3, 1), "Main window not found"
    
    # Set to foreground
    window.SetFocus()
    time.sleep(0.5)

    # 4. Find UI Elements
    search_box = window.EditControl()
    assert search_box.Exists(3, 1), "Search box not found"
    
    list_box = window.ListControl()
    assert list_box.Exists(3, 1), "List box not found"
    
    def get_text_from_item(item):
        if item.Name:
            return item.Name
        text_block = item.TextControl()
        if text_block.Exists(0, 0):
            return text_block.Name
        return ""

    # Check that our test text made it into the list
    found_item = False
    
    # Retry loop for latency
    for _ in range(5):
        list_items = list_box.GetChildren()
        print(f"Items found: {len(list_items)}")
        if len(list_items) > 0:
            for item in list_items:
                item_text = get_text_from_item(item)
                print(f"Item text: {item_text}")
                if test_text in item_text:
                    found_item = True
                    break
        if found_item:
            break
        time.sleep(1) # Increase wait time for WPF and DB latency
            
    assert found_item, "Test copied text was not found in the UI list after retries"

    # Take screenshot using PrintWindow
    hwnd = window.NativeWindowHandle
    take_print_window_screenshot(hwnd, "clipboard_manager_active.bmp")

    # Clean test closure
    auto.SendKeys('{Esc}')
    time.sleep(0.5)
    assert not window.Exists(1, 1), "Window should be hidden after Escape"
