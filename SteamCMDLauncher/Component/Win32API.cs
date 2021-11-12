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

        [DllImport("wininet.dll", SetLastError = true)]
        extern static bool InternetGetConnectedState(out int lpdwFlags, int dwReserved = 0);
        #endregion

        private const int FORCE_WINDOW_OPEN = 9;
        private const int FLAG_OFFLINE = 32; // (0x20) = 32

        public static void ForceWindowOpen(ref System.Windows.Window window, int FLAG = FORCE_WINDOW_OPEN)
        {
            if (window is null) return;

            if (window.Visibility == System.Windows.Visibility.Collapsed)
                window.Visibility = System.Windows.Visibility.Visible;

            IntPtr current_hidden_window = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();

            SetForegroundWindow(current_hidden_window);

            ShowWindow(current_hidden_window, FLAG);
        }

        public static bool IsConnectedToInternet()
        {
            bool result = InternetGetConnectedState(out int current_flag);
            
            Config.Log($"[WAPI32] InternetGetConnectedState returned {result} with flag: {current_flag}");
            
            return result;
        }
    }
}
