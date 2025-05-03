namespace MikeNakis.Console;

using MikeNakis.Kit;

#pragma warning disable RS0030 //banned symbols

public static class ConsoleHelpers
{
	public static bool Pause { get; set; }

	public static void Run( bool hasIcon, Sys.Func<int> mainFunction )
	{
		registerAppDomainHandlers();

		//NOTE: the following invocation must happen before anything gets printed to the console!
		bool isLaunchedFromGuiApplication = calculateIsLaunchedFromGuiApplication();
		Pause = isLaunchedFromGuiApplication;

		int exitCode = invokeSafe( () =>
		{
			if( isLaunchedFromGuiApplication )
			{
				restoreWindowRectangle();
				if( hasIcon )
					realizeConsoleWindowIcon();
				setConsoleOutputEncoding();
			}

			int result = mainFunction.Invoke();
			//DotNetHelpers.PerformGarbageCollectionAndWait();
			return result;
		} );

		if( Pause )
		{
			SysConsole.Write( $"Terminating with exit code {exitCode}; press [Enter]: " );
			SysConsole.ReadLine();
		}

		if( isLaunchedFromGuiApplication )
			saveWindowRectangle();

		Sys.Environment.Exit( exitCode );
		throw new Sys.InvalidOperationException();

		static bool calculateIsLaunchedFromGuiApplication()
		{
			if( Sys.OperatingSystem.IsWindows() )
			{
				//When a console application is launched from a GUI application, the title of the console window is the
				//exact full path to the executable. (It does _not_ include command-line arguments.)
				if( SysConsole.Title == SysReflect.Assembly.GetEntryAssembly()?.Location )
					return true;

				//When a console application is launched from a pre-existing console window running a command prompt, the
				//window title is the "console name" (see below) followed by a dash and then the entire command line of the
				//currently executing process, exactly as it was supplied to the command prompt, double quotes and all.
				//The "console name" depends on the shortcut that started the console window, so it cannot be relied upon.
				//(It might be the full path to cmd.exe or it might be something like "Command Prompt", or anything.)
				if( SysConsole.Title.EndsWith( $" - {Sys.Environment.CommandLine}", Sys.StringComparison.InvariantCulture ) )
					return false;
			}

			//When a console application is launched from a GUI application, it receives a brand new empty console
			//window, so the console cursor is at (0, 0).
			//When a console application is launched from a pre-existing console window running a command prompt, stuff
			//have already been printed in the window, so the console cursor is not at (0, 0).
			//PEARL: this will fail with IOException "the handle is invalid" in all scenarios where the output of the
			//       console application has been redirected.
			try
			{
				if( SysConsole.CursorTop != 0 || SysConsole.CursorLeft != 0 )
					return false;
			}
			catch( SysIo.IOException )
			{
				//SysConsole.Error.WriteLine( $"Warning: Failed to get console cursor position: {exception.GetType().FullName}: {exception.Message}" );
				return false;
			}
			return true;
		}

		static void saveWindowRectangle()
		{
			int left;
			int top;
			int width;
			int height;
			try
			{
				(left, top, width, height) = ConsoleNativeMethods.GetConsoleWindowRectangle();
			}
			catch( Sys.Exception e )
			{
				SysConsole.Error.WriteLine( $"Warning: Failed to obtain console window rectangle: {e.GetType().FullName}: {e.Message}" );
				return;
			}
			try
			{
				string myLocalAppData = getMyLocalAppData();
				if( !SysIo.Directory.Exists( myLocalAppData ) )
					SysIo.Directory.CreateDirectory( myLocalAppData );
				string settingsFilePath = getSettingsFilePath( myLocalAppData );
				SysIo.File.WriteAllText( settingsFilePath, $"{left}, {top}, {width}, {height}" );
			}
			catch( Sys.Exception e )
			{
				SysConsole.Error.WriteLine( $"Warning: Failed to save console window rectangle: {e.GetType().FullName}: {e.Message}" );
			}
		}

		static void restoreWindowRectangle()
		{
			string myLocalAppData = getMyLocalAppData();
			string settingsFilePath = getSettingsFilePath( myLocalAppData );
			if( !SysIo.File.Exists( settingsFilePath ) )
				return;
			int left, top, width, height;
			try
			{
				string text = SysIo.File.ReadAllText( settingsFilePath );
				string[] parts = text.Split( ',' );
				if( parts.Length != 4 )
					throw new AssertionFailureException();
				left = parseInt( parts[0] );
				top = parseInt( parts[1] );
				width = parseInt( parts[2] );
				height = parseInt( parts[3] );
			}
			catch( Sys.Exception e ) // the file could exist, but be somehow corrupt, causing the application to fail to start
			{
				SysConsole.Error.WriteLine( $"Failed to read settings file: {e.GetType().FullName}: {e.Message}" );
				return;
			}
			try
			{
				ConsoleNativeMethods.RealizeConsoleWindowRectangle( left, top, width, height );
			}
			catch( Sys.Exception e ) // as observed in the field, the file could exist, but be somehow corrupt, causing the application to fail to start
			{
				SysConsole.Error.WriteLine( $"Failed to realize window rectangle: {e.GetType().FullName}: {e.Message}" );
			}
		}

		static int parseInt( string s )
		{
			//PEARL: When `int.Parse()` fails, it throws a `System.FormatException`.
			//    This absolutely retarded exception contains no fields other than the message,
			//    and the message says nothing but "Input string was not in a correct format."
			//    So, when this exception is thrown, it fails to convey two very important pieces of information:
			//      1. What was the string that was not in a correct format.
			//      2. What was the correct format.
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

		static string getMainModuleName()
		{
			string mainModuleName = NotNull( SysDiag.Process.GetCurrentProcess().MainModule ).ModuleName;
			const string exeExtension = ".exe";
			Assert( mainModuleName.EndsWith( exeExtension, Sys.StringComparison.OrdinalIgnoreCase ) );
			return mainModuleName[..^exeExtension.Length];
		}

		static string getMyLocalAppData()
		{
			string localAppData = Sys.Environment.GetFolderPath( Sys.Environment.SpecialFolder.LocalApplicationData );
			string mainModuleName = getMainModuleName();
			return SysIo.Path.Combine( localAppData, mainModuleName );
		}

		static string getSettingsFilePath( string myLocalAppData )
		{
			return SysIo.Path.Combine( myLocalAppData, "settings.txt" );
		}

		static int invokeSafe( Sys.Func<int> function )
		{
			if( SysDiag.Debugger.IsAttached )
			{
				return function.Invoke();
			}
			try
			{
				return function.Invoke();
			}
			catch( ConsoleException exception )
			{
				SysConsole.Error.WriteLine( $"ERROR: {exception.Message}" );
				return -1;
			}
			catch( Sys.Exception exception )
			{
				SysConsole.Error.WriteLine( $"ERROR: unhandled exception: {exception.GetType().FullName}: {exception.Message}" );
				return -1;
			}
		}

		static void setConsoleOutputEncoding()
		{
			// PEARL: A wrong choice of project output type can cause mysterious console-related failures.
			//        If the "OutputType" property in the project file is set to "Exe", then the console works fine.
			//        If the "OutputType" property in the project file is mistakenly set to "WinExe", then the console
			//        works mostly fine, EXCEPT THAT trying to set the OutputEncoding of the console blows up
			//        with an exception saying something retarded like "the handle is invalid".
			//        To avoid this, check whether the name of the type implementing System.Console.In is "NullStreamReader".
			Assert( SysConsole.In.GetType().Name != "NullStreamReader" );
			SysConsole.OutputEncoding = SysText.Encoding.UTF8;
		}

		static void realizeConsoleWindowIcon()
		{
			string assemblyLocation = SysReflect.Assembly.GetEntryAssembly()!.Location;
			SysDraw.Icon? icon = SysDraw.Icon.ExtractAssociatedIcon( assemblyLocation );
			if( icon == null )
				SysConsole.Error.WriteLine( $"Warning: Failed to get associated icon of {assemblyLocation}" );
			else
				ConsoleNativeMethods.SetConsoleWindowIconHandle( icon.Handle );
		}
	}

	static void registerAppDomainHandlers()
	{
		//Sys.AppDomain.CurrentDomain.FirstChanceException += firstChanceExceptionHandler;
		Sys.AppDomain.CurrentDomain.ProcessExit += processExitHandler;
		Sys.AppDomain.CurrentDomain.UnhandledException += unhandledExceptionHandler;
	}

	//static void firstChanceExceptionHandler( object? sender, FirstChanceExceptionEventArgs e )
	//{
	//	if( e.Exception.Source == "WindowsBase" )
	//		return;
	//	if( e.Exception.Source == "System.IO.Pipes" )
	//		return;
	//	if( e.Exception.Source == "System.Net.Sockets" )
	//		return;
	//	//if( KitHelpers.FailureTesting.Value )
	//	//	return;
	//	SysDiag.Debug.WriteLine( $"First-chance Exception Event in {e.Exception.Source}: {e.Exception.GetType().FullName}: {e.Exception.Message}" );
	//}

	static void processExitHandler( object? sender, Sys.EventArgs e )
	{
		//Assert( ReferenceEquals( sender, Sys.AppDomain.CurrentDomain ) );
		//Assert( e != null ); //e == null );
		SysDiag.Debug.WriteLine( $"Process '{Sys.AppDomain.CurrentDomain.FriendlyName}' exit code: {Sys.Environment.ExitCode}" );
	}

	static void unhandledExceptionHandler( object sender, Sys.UnhandledExceptionEventArgs e )
	{
		//Assert( sender == null );
		string message = e.ExceptionObject is Sys.Exception exception ? $"{exception.Message}" : $"{e.ExceptionObject}";
		SysDiag.Debug.WriteLine( $"AppDomain Unhandled Exception! (terminating={e.IsTerminating}): {e.ExceptionObject.GetType().FullName}: {message}" );
	}
}
