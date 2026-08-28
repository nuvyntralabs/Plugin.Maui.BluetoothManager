namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Entry point for the BluetoothManager plugin when dependency injection is not used.
/// </summary>
public static class BluetoothManager
{
	static IBluetoothManager? _current;

	/// <summary>
	/// Gets the shared <see cref="IBluetoothManager"/> instance.
	/// </summary>
	public static IBluetoothManager Current => _current ??= Create(new BluetoothManagerOptions());

	/// <summary>
	/// Raised when a managed device changes connection state.
	/// </summary>
	public static event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged
	{
		add => Current.ConnectionStateChanged += value;
		remove => Current.ConnectionStateChanged -= value;
	}

	/// <summary>
	/// Raised after a device leaves the connected state.
	/// </summary>
	public static event EventHandler<DeviceDisconnectedEventArgs>? DeviceDisconnected
	{
		add => Current.DeviceDisconnected += value;
		remove => Current.DeviceDisconnected -= value;
	}

	/// <summary>
	/// Raised when the adapter turns on, off, or becomes unauthorized.
	/// </summary>
	public static event EventHandler<AdapterStateChangedEventArgs>? AdapterStateChanged
	{
		add => Current.AdapterStateChanged += value;
		remove => Current.AdapterStateChanged -= value;
	}

	/// <summary>
	/// Raised when a subscribed characteristic notifies or indicates.
	/// </summary>
	public static event EventHandler<CharacteristicChangedEventArgs>? CharacteristicChanged
	{
		add => Current.CharacteristicChanged += value;
		remove => Current.CharacteristicChanged -= value;
	}

	public static Task<IReadOnlyList<BleDevice>> ScanAsync(ScanOptions? options = null, CancellationToken cancellationToken = default) =>
		Current.ScanAsync(options, cancellationToken);

	public static Task<BleDevice> ConnectAsync(BleDevice device, ConnectOptions? options = null, CancellationToken cancellationToken = default) =>
		Current.ConnectAsync(device, options, cancellationToken);

	public static Task DisconnectAsync(BleDevice? device = null, CancellationToken cancellationToken = default) =>
		Current.DisconnectAsync(device, cancellationToken);

	public static Task WriteAsync(BleCharacteristic characteristic, byte[] data, WriteOptions? options = null, CancellationToken cancellationToken = default) =>
		Current.WriteAsync(characteristic, data, options, cancellationToken);

	public static Task<byte[]> ReadAsync(BleCharacteristic characteristic, CancellationToken cancellationToken = default) =>
		Current.ReadAsync(characteristic, cancellationToken);

	/// <summary>
	/// Creates a new instance using the platform BLE transport and MAUI permissions.
	/// </summary>
	public static IBluetoothManager Create(BluetoothManagerOptions? options = null) =>
		new BluetoothManagerImplementation(
			options ?? new BluetoothManagerOptions(),
			new PlatformBleTransport(),
			new MauiBluetoothPermissionGateway(),
			SystemClock.Instance);

	/// <summary>
	/// Replaces the shared instance. Intended for tests and custom implementations.
	/// </summary>
	public static void SetDefault(IBluetoothManager implementation) =>
		_current = implementation ?? throw new ArgumentNullException(nameof(implementation));

	internal static BluetoothManagerImplementation Create(
		BluetoothManagerOptions options,
		IBleTransport transport,
		IBluetoothPermissionGateway permissions,
		IClock clock) =>
		new(options, transport, permissions, clock);
}
