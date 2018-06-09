using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ClrUtils
{
    class NativeMethods
    {
	    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	    public static extern IntPtr GetCurrentProcess();

	    public const Int32 PROCESS_QUERY_INFORMATION = 0x0400;

	    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	    public static extern IntPtr OpenProcess(int dwDesiredAccess,
		    [MarshalAs(UnmanagedType.Bool)]bool bInheritHandle,
		    int dwProcessId);

	    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	    [return: MarshalAs(UnmanagedType.Bool)]
	    public static extern bool CloseHandle(IntPtr hObject);

	    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	    public static extern IntPtr GetModuleHandle(string moduleName);

	    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	    public static extern IntPtr GetProcAddress(IntPtr hModule,
		    [MarshalAs(UnmanagedType.LPStr)]string procName);

	    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	    [return: MarshalAs(UnmanagedType.Bool)]
	    public static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);
	}
}
