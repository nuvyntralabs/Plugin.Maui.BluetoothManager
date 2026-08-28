using System.Diagnostics;

namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Writes plugin diagnostics to <see cref="Debug.WriteLine(string?)"/>.
/// </summary>
public sealed class DebugBluetoothManagerLogger : IBluetoothManagerLogger
{
	public void Log(BluetoothManagerLogLevel level, string message, Exception? exception = null)
	{
		var line = exception is null
			? $"[BluetoothManager] {level}: {message}"
			: $"[BluetoothManager] {level}: {message}{Environment.NewLine}{exception}";

		Debug.WriteLine(line);
	}
}
