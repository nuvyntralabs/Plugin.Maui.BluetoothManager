namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Thrown when a Bluetooth manager operation cannot complete.
/// </summary>
public sealed class BluetoothManagerException : Exception
{
	public BluetoothManagerException(BluetoothManagerError error, string message, Exception? innerException = null)
		: base(message, innerException)
	{
		Error = error;
	}

	/// <summary>
	/// Gets the classified failure.
	/// </summary>
	public BluetoothManagerError Error { get; }
}
