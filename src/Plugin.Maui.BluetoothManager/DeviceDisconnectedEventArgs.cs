namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Raised after a managed device is no longer connected.
/// </summary>
public sealed class DeviceDisconnectedEventArgs : EventArgs
{
	public DeviceDisconnectedEventArgs(BleDevice device, DisconnectReason reason, string? message = null)
	{
		Device = device;
		Reason = reason;
		Message = message;
	}

	public BleDevice Device { get; }

	public DisconnectReason Reason { get; }

	public string? Message { get; }
}
