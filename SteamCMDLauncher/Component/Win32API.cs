using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SteamCMDLauncher.Component
{
    public static class Win32API
    {
        #region Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr handle, int nCmdShow);
        #endregion

        private const int FORCE_WINDOW_OPEN = 9;

        public static void ForceWindowOpen(ref System.Windows.Window window)
        {
            if (window is null) return;

            IntPtr current_hidden_window = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();

            SetForegroundWindow(current_hidden_window);

            ShowWindow(current_hidden_window, 9);
        }
    }
}
