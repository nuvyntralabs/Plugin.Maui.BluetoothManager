#if !ANDROID && !IOS
namespace Plugin.Maui.BluetoothManager;

sealed class PlatformBleTransport : IBleTransport
{
	public bool IsSupported => false;

	public BluetoothManagerPlatformInfo Platform { get; } =
		new(false, "none", "unavailable");

	public BluetoothAdapterState AdapterState => BluetoothAdapterState.Unsupported;

	public event EventHandler<AdapterStateChangedEventArgs>? AdapterStateChanged
	{
		add { }
		remove { }
	}

	public event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscovered
	{
		add { }
		remove { }
	}

	public event EventHandler<NativeConnectionEventArgs>? ConnectionChanged
	{
		add { }
		remove { }
	}

	public event EventHandler<CharacteristicChangedEventArgs>? CharacteristicChanged
	{
		add { }
		remove { }
	}

	public event EventHandler<RssiUpdatedEventArgs>? RssiUpdated
	{
		add { }
		remove { }
	}

	public Task StartScanAsync(ScanOptions options, CancellationToken cancellationToken) =>
		Task.FromException(FeatureNotSupported());

	public Task StopScanAsync() => Task.CompletedTask;

	public Task ConnectNativeAsync(string deviceId, ConnectOptions options, CancellationToken cancellationToken) =>
		Task.FromException(FeatureNotSupported());

	public Task DisconnectNativeAsync(string deviceId, CancellationToken cancellationToken) => Task.CompletedTask;

	public Task<IReadOnlyList<BleService>> DiscoverServicesAsync(string deviceId, CancellationToken cancellationToken) =>
		Task.FromException<IReadOnlyList<BleService>>(FeatureNotSupported());

	public Task WriteNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, byte[] data, BleWriteType writeType, CancellationToken cancellationToken) =>
		Task.FromException(FeatureNotSupported());

	public Task<byte[]> ReadNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken) =>
		Task.FromException<byte[]>(FeatureNotSupported());

	public Task SubscribeNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken) =>
		Task.FromException(FeatureNotSupported());

	public Task UnsubscribeNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	public Task<int> ReadRssiNativeAsync(string deviceId, CancellationToken cancellationToken) =>
		Task.FromException<int>(FeatureNotSupported());

	public void Dispose()
	{
	}

	static BluetoothManagerException FeatureNotSupported() =>
		new(BluetoothManagerError.FeatureNotSupported,
			"Plugin.Maui.BluetoothManager requires Android or iOS. The shared net10.0 surface is for tests and class libraries.");
}
#endif
