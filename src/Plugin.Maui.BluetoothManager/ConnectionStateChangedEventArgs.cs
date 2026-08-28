namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Raised when a managed device moves between connection states.
/// </summary>
public sealed class ConnectionStateChangedEventArgs : EventArgs
{
	public ConnectionStateChangedEventArgs(
		BleDevice device,
		BluetoothConnectionState previous,
		BluetoothConnectionState current)
	{
		Device = device;
		Previous = previous;
		Current = current;
	}

	public BleDevice Device { get; }

	public BluetoothConnectionState Previous { get; }

	public BluetoothConnectionState Current { get; }
}
