namespace CB.Updater.Utils;

using System.Runtime.InteropServices;

public static partial class Win32ApiUtil
{
    [LibraryImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(IntPtr hWnd, uint nCmdShow);
}