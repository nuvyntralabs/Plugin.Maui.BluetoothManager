namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Raised when the platform Bluetooth adapter changes state.
/// </summary>
public sealed class AdapterStateChangedEventArgs : EventArgs
{
	public AdapterStateChangedEventArgs(BluetoothAdapterState previous, BluetoothAdapterState current)
	{
		Previous = previous;
		Current = current;
	}

	public BluetoothAdapterState Previous { get; }

	public BluetoothAdapterState Current { get; }
}
