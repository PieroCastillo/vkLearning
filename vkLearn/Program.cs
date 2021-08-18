using System;
using System.Runtime.InteropServices;

namespace vkLearn
{
    class Program
    {
		static string AppName = "DrawHello Program";
		static string ClassName = "DrawHelloClass";
		static IntPtr hWnd;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			//var window = new Window(800, 600, "Hello");
			//window.Run();

			using var sample = new Sample();
		}

	}
}
