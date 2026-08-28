namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Raised when a scan advertisement passes the configured filters.
/// </summary>
public sealed class DeviceDiscoveredEventArgs : EventArgs
{
	public DeviceDiscoveredEventArgs(BleDevice device)
	{
		Device = device;
	}

	public BleDevice Device { get; }
}
