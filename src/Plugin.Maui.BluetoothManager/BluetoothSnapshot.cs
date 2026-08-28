namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Point-in-time adapter, permission, and connection state.
/// </summary>
public sealed class BluetoothSnapshot
{
	public BluetoothSnapshot(
		DateTimeOffset capturedAt,
		BluetoothAdapterState adapterState,
		BluetoothPermissionStatus permissionStatus,
		BluetoothConnectionState connectionState,
		BleDevice? connectedDevice,
		IReadOnlyList<BleDevice> connectedDevices,
		bool isScanning)
	{
		CapturedAt = capturedAt;
		AdapterState = adapterState;
		PermissionStatus = permissionStatus;
		ConnectionState = connectionState;
		ConnectedDevice = connectedDevice;
		ConnectedDevices = connectedDevices;
		IsScanning = isScanning;
	}

	public DateTimeOffset CapturedAt { get; }

	public BluetoothAdapterState AdapterState { get; }

	public BluetoothPermissionStatus PermissionStatus { get; }

	/// <summary>
	/// Gets the state of the primary (most recently connected) session.
	/// </summary>
	public BluetoothConnectionState ConnectionState { get; }

	public BleDevice? ConnectedDevice { get; }

	public IReadOnlyList<BleDevice> ConnectedDevices { get; }

	public bool IsScanning { get; }
}
