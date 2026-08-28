namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// High-level BLE device connection manager for printers, POS, medical, IoT, and industrial peripherals.
/// </summary>
public interface IBluetoothManager
{
	/// <summary>
	/// Gets a value indicating whether native BLE APIs are available on this target.
	/// </summary>
	bool IsSupported { get; }

	/// <summary>
	/// Gets the native stack used on this platform.
	/// </summary>
	BluetoothManagerPlatformInfo Platform { get; }

	/// <summary>
	/// Gets the current adapter / authorization state.
	/// </summary>
	BluetoothAdapterState AdapterState { get; }

	/// <summary>
	/// Gets the state of the primary (most recently connected) session.
	/// </summary>
	BluetoothConnectionState ConnectionState { get; }

	/// <summary>
	/// Gets the primary connected device, or <c>null</c> when none is connected.
	/// </summary>
	BleDevice? ConnectedDevice { get; }

	/// <summary>
	/// Gets every device that currently has an active GATT session.
	/// </summary>
	IReadOnlyList<BleDevice> ConnectedDevices { get; }

	/// <summary>
	/// Gets a value indicating whether a scan is running.
	/// </summary>
	bool IsScanning { get; }

	/// <summary>
	/// Gets a point-in-time adapter, permission, and connection payload.
	/// </summary>
	BluetoothSnapshot Snapshot { get; }

	/// <summary>
	/// Raised when the adapter turns on, off, or becomes unauthorized.
	/// </summary>
	event EventHandler<AdapterStateChangedEventArgs>? AdapterStateChanged;

	/// <summary>
	/// Raised when a managed device changes connection state.
	/// </summary>
	event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

	/// <summary>
	/// Raised after a device leaves the connected state.
	/// </summary>
	event EventHandler<DeviceDisconnectedEventArgs>? DeviceDisconnected;

	/// <summary>
	/// Raised for each advertisement that passes scan filters.
	/// </summary>
	event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscovered;

	/// <summary>
	/// Raised when a subscribed characteristic notifies or indicates.
	/// </summary>
	event EventHandler<CharacteristicChangedEventArgs>? CharacteristicChanged;

	/// <summary>
	/// Raised after <see cref="ReadRssiAsync"/> or a platform RSSI callback.
	/// </summary>
	event EventHandler<RssiUpdatedEventArgs>? RssiUpdated;

	/// <summary>
	/// Checks Bluetooth (and older-Android location) permission without prompting.
	/// </summary>
	Task<BluetoothPermissionStatus> CheckPermissionsAsync();

	/// <summary>
	/// Requests Bluetooth (and older-Android location) permission.
	/// </summary>
	Task<BluetoothPermissionStatus> RequestPermissionsAsync();

	/// <summary>
	/// Scans for peripherals and returns devices that pass <paramref name="options"/>.
	/// </summary>
	Task<IReadOnlyList<BleDevice>> ScanAsync(ScanOptions? options = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Stops an in-flight scan.
	/// </summary>
	Task StopScanAsync();

	/// <summary>
	/// Connects to <paramref name="device"/>, optionally discovers services, and starts lifecycle tracking.
	/// </summary>
	Task<BleDevice> ConnectAsync(BleDevice device, ConnectOptions? options = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Disconnects <paramref name="device"/> or the primary session when <paramref name="device"/> is <c>null</c>.
	/// Cancels auto-reconnect for that session.
	/// </summary>
	Task DisconnectAsync(BleDevice? device = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns discovered GATT services for a connected device.
	/// </summary>
	Task<IReadOnlyList<BleService>> GetServicesAsync(BleDevice? device = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Finds a characteristic on a connected device by service and characteristic UUID.
	/// </summary>
	Task<BleCharacteristic?> FindCharacteristicAsync(Guid serviceId, Guid characteristicId, BleDevice? device = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Writes <paramref name="data"/> to <paramref name="characteristic"/> with optional retry.
	/// </summary>
	Task WriteAsync(BleCharacteristic characteristic, byte[] data, WriteOptions? options = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads the current value of <paramref name="characteristic"/> with optional retry.
	/// </summary>
	Task<byte[]> ReadAsync(BleCharacteristic characteristic, CancellationToken cancellationToken = default);

	/// <summary>
	/// Enables notify or indicate on <paramref name="characteristic"/>.
	/// </summary>
	Task SubscribeAsync(BleCharacteristic characteristic, CancellationToken cancellationToken = default);

	/// <summary>
	/// Disables notify or indicate on <paramref name="characteristic"/>.
	/// </summary>
	Task UnsubscribeAsync(BleCharacteristic characteristic, CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads RSSI for a connected device.
	/// </summary>
	Task<int> ReadRssiAsync(BleDevice? device = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Enables or disables plugin diagnostics.
	/// </summary>
	void EnableLogging(bool enabled, IBluetoothManagerLogger? logger = null);
}
