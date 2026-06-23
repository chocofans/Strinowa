using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace StrinowaWPF
{
    public partial class GameWindow : Window
    {
        public GameWindow() => InitializeComponent();

        void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e) => DragMove();
        void CloseBtn_Click(object s, RoutedEventArgs e) => Close();

        void BrowseDll_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Select DLL to inject",
                Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*",
            };
            if (dlg.ShowDialog() == true)
                DllPathBox.Text = dlg.FileName;
        }

        void InjectBtn_Click(object s, RoutedEventArgs e)
        {
            var dllPath = DllPathBox.Text.Trim();
            if (string.IsNullOrEmpty(dllPath))
            {
                SetStatus("No DLL selected.", error: true);
                return;
            }
            if (!File.Exists(dllPath))
            {
                SetStatus("DLL file not found.", error: true);
                return;
            }

            var item = TargetProcessBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var processName = item?.Content?.ToString()?.Replace(".exe", "") ?? "";
            if (string.IsNullOrEmpty(processName))
            {
                SetStatus("No target process selected.", error: true);
                return;
            }

            var procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0)
            {
                SetStatus($"Process '{processName}.exe' not found. Launch the game first.", error: true);
                return;
            }

            try
            {
                Inject(procs[0], dllPath);
                SetStatus($"Injected into {processName}.exe (PID {procs[0].Id}).");
            }
            catch (Exception ex)
            {
                SetStatus($"Injection failed: {ex.Message}", error: true);
            }
        }

        void SetStatus(string msg, bool error = false)
        {
            StatusLabel.Text = msg;
            StatusLabel.Foreground = error
                ? new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xFF, 0x55, 0x55))
                : new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x55, 0xCC, 0x88));
        }

        // Standard LoadLibrary injection via CreateRemoteThread
        static void Inject(Process target, string dllPath)
        {
            var fullPath = Path.GetFullPath(dllPath);
            var pathBytes = Encoding.Unicode.GetBytes(fullPath + "\0");

            var hProcess = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_ALL_ACCESS, false, (uint)target.Id);
            if (hProcess == IntPtr.Zero)
                throw new InvalidOperationException("OpenProcess failed.");

            try
            {
                var mem = NativeMethods.VirtualAllocEx(
                    hProcess, IntPtr.Zero, (uint)pathBytes.Length,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                    NativeMethods.PAGE_READWRITE);
                if (mem == IntPtr.Zero)
                    throw new InvalidOperationException("VirtualAllocEx failed.");

                if (!NativeMethods.WriteProcessMemory(
                    hProcess, mem, pathBytes, (uint)pathBytes.Length, out _))
                    throw new InvalidOperationException("WriteProcessMemory failed.");

                var kernel32 = NativeMethods.GetModuleHandle("kernel32.dll");
                var loadLib  = NativeMethods.GetProcAddress(kernel32, "LoadLibraryW");

                var thread = NativeMethods.CreateRemoteThread(
                    hProcess, IntPtr.Zero, 0, loadLib, mem, 0, out _);
                if (thread == IntPtr.Zero)
                    throw new InvalidOperationException("CreateRemoteThread failed.");

                NativeMethods.WaitForSingleObject(thread, 5000);
                NativeMethods.CloseHandle(thread);
                NativeMethods.VirtualFreeEx(hProcess, mem, 0, NativeMethods.MEM_RELEASE);
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }
    }

    static class NativeMethods
    {
        public const uint PROCESS_ALL_ACCESS  = 0x001F0FFF;
        public const uint MEM_COMMIT          = 0x1000;
        public const uint MEM_RESERVE         = 0x2000;
        public const uint MEM_RELEASE         = 0x8000;
        public const uint PAGE_READWRITE      = 0x04;

        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint dwAccess, bool bInherit, uint dwPID);
        [DllImport("kernel32.dll")] public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);
        [DllImport("kernel32.dll")] public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);
        [DllImport("kernel32.dll")] public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);
        [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] public static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    }
}
