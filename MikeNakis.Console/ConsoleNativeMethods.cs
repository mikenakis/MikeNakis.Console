namespace MikeNakis.Console;

using Sys = System;
using SysGlob = System.Globalization;
using SysInterop = System.Runtime.InteropServices;
using SysCompModel = System.ComponentModel;
using MikeNakis.Kit;
using static MikeNakis.Kit.GlobalStatics;

#pragma warning disable CA1045 // Do not pass types by reference
#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable IDE1006 // Naming Styles

///<summary>Provides functionality for saving and restoring the size and position of a console window.</summary>
public static class ConsoleNativeMethods
{
	[SysInterop.StructLayout( SysInterop.LayoutKind.Sequential )]
	struct RECT
	{
		public int X1;
		public int Y1;
		public int X2;
		public int Y2;

		public int X { readonly get => X1; set => X1 = value; }
		public int Y { readonly get => Y1; set => Y1 = value; }
		public int W { readonly get => X2 - X1; set => X2 = X1 + value; }
		public int H { readonly get => Y2 - Y1; set => Y2 = Y1 + value; }

		public readonly override string ToString() => $"X={X},Y={Y},W={W},H={H}";
		public readonly string StringFrom() => S( $"{X1},{Y1},{X2},{Y2}" );

		public static RECT FromString( string s )
		{
			string[] parts = s.Split( ',' );
			Assert( parts.Length == 4 );
			RECT result = new();
			result.X1 = parseInt( parts[0] );
			result.Y1 = parseInt( parts[1] );
			result.X2 = parseInt( parts[2] );
			result.Y2 = parseInt( parts[3] );
			return result;
		}

		static int parseInt( string s )
		{
			//PEARL: if `int.Parse()` fails, it throws a `System.FormatException`.  This absolutely
			//    retarded exception contains no fields other than the message, and the message
			//    always says nothing but "Input string was not in a correct format."  So, it fails
			//    to convey two very important pieces of information:
			//    1. What was the string that was not in a correct format.
			//    2. What was the correct format.
			//    We correct this insanity here.
			try
			{
				return int.Parse( s, SysGlob.CultureInfo.InvariantCulture );
			}
			catch( Sys.Exception exception )
			{
				throw new Sys.InvalidOperationException( $"Failed to parse '{s}' as an integer", exception );
			}
		}
	}

	[SysInterop.DllImport( "kernel32.dll", SetLastError = true ), SysInterop.DefaultDllImportSearchPaths( SysInterop.DllImportSearchPath.System32 )] static extern nint GetConsoleWindow();
	[SysInterop.DllImport( "user32.dll", SetLastError = true ), SysInterop.DefaultDllImportSearchPaths( SysInterop.DllImportSearchPath.System32 )] static extern bool MoveWindow( nint hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint );
	[SysInterop.DllImport( "user32.dll", SetLastError = true ), SysInterop.DefaultDllImportSearchPaths( SysInterop.DllImportSearchPath.System32 )] static extern bool GetWindowRect( nint hWnd, ref RECT lpRect );
	[SysInterop.DllImport( "user32.dll" ), SysInterop.DefaultDllImportSearchPaths( SysInterop.DllImportSearchPath.System32 )] static extern bool ShowWindow( nint hWnd, int nCmdShow );
	[SysInterop.DllImport( "user32.dll", SetLastError = false ), SysInterop.DefaultDllImportSearchPaths( SysInterop.DllImportSearchPath.System32 )] static extern nint GetDesktopWindow();
	[SysInterop.DllImport( "user32.dll" ), SysInterop.DefaultDllImportSearchPaths( SysInterop.DllImportSearchPath.System32 )] static extern int SendMessage( nint hWnd, int message, int wParam, nint lParam );

	const int SW_HIDE = 0;
	const int SW_SHOW = 5;
	const int WM_SETICON = 0x80;
	const int ICON_SMALL = 0;
	const int ICON_BIG = 1;

	public static void SetConsoleWindowVisibility( bool visible )
	{
		Log.Debug( $"Setting console window visibility to {visible}" );
		nint hWnd = GetConsoleWindow();
		if( hWnd != 0 )
			ShowWindow( hWnd, visible ? SW_SHOW : SW_HIDE );
		else
			Log.Error( "Failed to get the console window" );
	}

	static Sys.Exception lastErrorException() => new SysCompModel.Win32Exception( SysInterop.Marshal.GetLastWin32Error() );

	public static (int left, int top, int width, int height) GetConsoleWindowRectangle()
	{
		nint consoleWindowHandle = GetConsoleWindow();
		if( consoleWindowHandle == 0 )
			throw lastErrorException();
		RECT r = new();
		bool ok = GetWindowRect( consoleWindowHandle, ref r );
		if( !ok )
			throw lastErrorException();
		return (r.X, r.Y, r.W, r.H);
	}

	public static string? GetConsoleWindowRectangleAsText()
	{
		nint consoleWindowHandle = GetConsoleWindow();
		if( consoleWindowHandle == 0 )
			return null;
		RECT consoleWindowRectangle = new();
		bool ok = GetWindowRect( consoleWindowHandle, ref consoleWindowRectangle );
		Assert( ok );
		return consoleWindowRectangle.StringFrom();
	}

	public static void RealizeConsoleWindowRectangle( int left, int top, int width, int height )
	{
		nint consoleWindowHandle = GetConsoleWindow();
		if( consoleWindowHandle == 0 )
			throw lastErrorException();
		RECT r = new();
		r.X = left;
		r.Y = top;
		r.W = width;
		r.H = height;
		ensureVisible( r );
		moveWindow( consoleWindowHandle, r );
	}

	public static void RealizeConsoleWindowRectangleFromText( string text )
	{
		nint consoleWindowHandle = GetConsoleWindow();
		if( consoleWindowHandle == 0 )
			return;
		RECT consoleWindowRectangle = RECT.FromString( text );
		ensureVisible( consoleWindowRectangle );
		moveWindow( consoleWindowHandle, consoleWindowRectangle );
	}

	static void ensureVisible( RECT windowRectangle )
	{
		// A poor man's validation of a window rectangle to ensure that it is within the visible screen.
		// Proper validation would involve obtaining the current set of monitors and making sure that the window is at
		// least partially visible in at least one of them.
		// Also, to properly do this we should keep track of different monitor configurations we have seen in the past
		// and remember a different window rectangle for each monitor configuration.
		windowRectangle.W = Sys.Math.Max( 100, windowRectangle.W );
		windowRectangle.H = Sys.Math.Max( 100, windowRectangle.H );
		nint desktopWindowHandle = GetDesktopWindow();
		if( desktopWindowHandle == 0 )
		{
			Log.Debug( "failed to get the desktop window!" );
			return;
		}
		RECT desktopWindowRectangle = new();
		bool ok = GetWindowRect( desktopWindowHandle, ref desktopWindowRectangle );
		Assert( ok );
		windowRectangle.X1 = Sys.Math.Max( windowRectangle.X1, desktopWindowRectangle.X1 );
		windowRectangle.Y1 = Sys.Math.Max( windowRectangle.Y1, desktopWindowRectangle.Y1 );
		windowRectangle.X2 = Sys.Math.Min( windowRectangle.X2, desktopWindowRectangle.X2 );
		windowRectangle.Y2 = Sys.Math.Min( windowRectangle.Y2, desktopWindowRectangle.Y2 );
	}

	static void moveWindow( nint windowHandle, RECT rect )
	{
		//PEARL: A single invocation of `MoveWindow()` used to work, and then one day it stopped working correctly:
		//       the window would move to the correct position, but the size would be wrong. (larger.)
		//       This may have had something to do with the fact that one day around that time I temporarily connected
		//       my laptop to a 4k display ONCE, and then returned to my usual setup with a dual-1080p monitor plus the
		//       laptop screen. I suspect that exposure to a 4k display caused windows to start behaving in a way which
		//       is slightly different to how it used to behave. (At least one more app started exhibiting this behavior
		//       around the same time.)
		//       Using PInvoke/SetProcessDPIAware() did not help. (I guess console apps are already DPI aware.)
		//       I figured out that the size is wrong because it uses the scaling of the original monitor, (the monitor
		//       in which the window was at the moment MoveWindow() was invoked,) instead of the scaling of the monitor
		//       identified by the X,Y coordinates passed to MoveWindow().
		//       So, the solution that I found is to invoke MoveWindow twice: the first invocation moves the window to
		//       the correct target monitor, (the size is irrelevant at this moment,) and the second invocation makes it
		//       realize its width and height according to the scaling of that monitor.
		bool ok = MoveWindow( windowHandle, rect.X1, rect.Y1, rect.W, rect.H, true );
		Assert( ok );
		ok = MoveWindow( windowHandle, rect.X1, rect.Y1, rect.W, rect.H, true );
		Assert( ok );
	}

	public static void SetConsoleWindowIconHandle( nint iconHandle )
	{
		nint mainWindowHandle = GetConsoleWindow();
		_ = SendMessage( mainWindowHandle, WM_SETICON, ICON_BIG, iconHandle );
		_ = SendMessage( mainWindowHandle, WM_SETICON, ICON_SMALL, iconHandle );
	}
}
