using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace vkLearn
{
    public class Window
    {
		static string AppName = "DrawHello Program";
		static string ClassName = "DrawHelloClass";
		public int Width = 0;
	    public int Height = 0;
	    public IntPtr hwnd;
		Win32.MSG Msg;
		int rv;

		public Window(int width, int height, string title)
        {
			AppName = title;
			Width = width;
			Height = height;

			Msg = new Win32.MSG();
			if (RegisterClass() == 0)
				return;
			if (Create() == 0)
				return;
			// Main message loop:

		}

		private int RegisterClass()
		{
			Win32.WNDCLASSEX wcex = new Win32.WNDCLASSEX();
			wcex.style = Win32.ClassStyles.DoubleClicks;
			wcex.cbSize = (uint)Marshal.SizeOf(wcex);
			wcex.lpfnWndProc = WndProc;
			wcex.cbClsExtra = 0;
			wcex.cbWndExtra = 0;
			wcex.hIcon = Win32.LoadIcon(IntPtr.Zero, (IntPtr)Win32.IDI_APPLICATION);
			wcex.hCursor = Win32.LoadCursor(IntPtr.Zero, (int)Win32.IDC_ARROW);
			wcex.hIconSm = IntPtr.Zero;
			wcex.hbrBackground = (IntPtr)(Win32.COLOR_WINDOW + 1);
			wcex.lpszMenuName = null;
			wcex.lpszClassName = ClassName;
			if (Win32.RegisterClassEx(ref wcex) == 0)
			{
				Win32.MessageBox(IntPtr.Zero, "RegisterClassEx failed", AppName,
					(int)(Win32.MB_OK | Win32.MB_ICONEXCLAMATION | Win32.MB_SETFOREGROUND));
				return (0);
			}
			return (1);
		}

		private int Create()
		{
			hwnd = Win32.CreateWindowEx(0, ClassName, AppName, Win32.WS_OVERLAPPEDWINDOW | Win32.WS_VISIBLE,
				250, 250, Width, Height, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
			if (hwnd != IntPtr.Zero)
				return (1);
			Win32.MessageBox(IntPtr.Zero, "CreateWindow failed", AppName,
				(int)(Win32.MB_OK | Win32.MB_ICONEXCLAMATION | Win32.MB_SETFOREGROUND));
			return (0);
		}

		public void Run(Action onRender)
        {
			while ((rv = Win32.GetMessage(out Msg, IntPtr.Zero, 0, 0)) > 0)
			{
				Win32.TranslateMessage(ref Msg);
				Win32.DispatchMessage(ref Msg);
				onRender.Invoke();
			}
        }

		private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
		{
			switch (message)
			{
				case Win32.WM_PAINT:
					{		 
						return IntPtr.Zero;
					}
				case Win32.WM_DESTROY:
					Win32.PostQuitMessage(0);
					return IntPtr.Zero;
				default:
					return (Win32.DefWindowProc(hwnd, message, wParam, lParam));
			}
		}
	}
}
