using Microsoft.Extensions.Logging;

namespace Plugin.Maui.BluetoothManager;

sealed class MicrosoftLoggerAdapter(ILogger logger) : IBluetoothManagerLogger
{
	public void Log(BluetoothManagerLogLevel level, string message, Exception? exception = null)
	{
		logger.Log(ToLogLevel(level), exception, "{Message}", message);
	}

	static LogLevel ToLogLevel(BluetoothManagerLogLevel level) => level switch
	{
		BluetoothManagerLogLevel.Trace => LogLevel.Trace,
		BluetoothManagerLogLevel.Debug => LogLevel.Debug,
		BluetoothManagerLogLevel.Information => LogLevel.Information,
		BluetoothManagerLogLevel.Warning => LogLevel.Warning,
		BluetoothManagerLogLevel.Error => LogLevel.Error,
		_ => LogLevel.Information
	};
}
