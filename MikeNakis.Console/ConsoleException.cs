namespace MikeNakis.Console;

using Sys = System;

///<summary>An exception about some problem that can be expected to happen, so we want the console application to
/// terminate with a single error message instead of a full stack trace.</summary>
[Sys.Serializable]
public class ConsoleException : Sys.Exception
{
	/// Constructor
	public ConsoleException( string message )
		: base( message )
	{
	}
}
