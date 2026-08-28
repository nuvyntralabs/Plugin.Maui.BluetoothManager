namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Raised when a connected device reports a new RSSI reading.
/// </summary>
public sealed class RssiUpdatedEventArgs : EventArgs
{
	public RssiUpdatedEventArgs(BleDevice device, int rssi)
	{
		Device = device;
		Rssi = rssi;
	}

	public BleDevice Device { get; }

	public int Rssi { get; }
}
