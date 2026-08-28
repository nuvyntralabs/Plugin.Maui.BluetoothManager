namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Receives diagnostic messages from the BluetoothManager plugin.
/// </summary>
public interface IBluetoothManagerLogger
{
	void Log(BluetoothManagerLogLevel level, string message, Exception? exception = null);
}
