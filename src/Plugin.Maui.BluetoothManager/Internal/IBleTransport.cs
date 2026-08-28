namespace Plugin.Maui.BluetoothManager;

internal interface IBleTransport : IDisposable
{
	bool IsSupported { get; }

	BluetoothManagerPlatformInfo Platform { get; }

	BluetoothAdapterState AdapterState { get; }

	event EventHandler<AdapterStateChangedEventArgs>? AdapterStateChanged;

	event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscovered;

	event EventHandler<NativeConnectionEventArgs>? ConnectionChanged;

	event EventHandler<CharacteristicChangedEventArgs>? CharacteristicChanged;

	event EventHandler<RssiUpdatedEventArgs>? RssiUpdated;

	Task StartScanAsync(ScanOptions options, CancellationToken cancellationToken);

	Task StopScanAsync();

	Task ConnectNativeAsync(string deviceId, ConnectOptions options, CancellationToken cancellationToken);

	Task DisconnectNativeAsync(string deviceId, CancellationToken cancellationToken);

	Task<IReadOnlyList<BleService>> DiscoverServicesAsync(string deviceId, CancellationToken cancellationToken);

	Task WriteNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, byte[] data, BleWriteType writeType, CancellationToken cancellationToken);

	Task<byte[]> ReadNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken);

	Task SubscribeNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken);

	Task UnsubscribeNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken);

	Task<int> ReadRssiNativeAsync(string deviceId, CancellationToken cancellationToken);
}
