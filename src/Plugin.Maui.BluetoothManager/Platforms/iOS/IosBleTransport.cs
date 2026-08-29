#if IOS
using System.Collections.Concurrent;
using CoreBluetooth;
using Foundation;
using Microsoft.Maui.ApplicationModel;

namespace Plugin.Maui.BluetoothManager;

sealed class PlatformBleTransport : IBleTransport
{
	readonly ConcurrentDictionary<string, CBPeripheral> _peripherals = new(StringComparer.OrdinalIgnoreCase);
	readonly ConcurrentDictionary<string, IosGattSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
	readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
	readonly IosCentralDelegate _delegate;
	CBCentralManager? _central;
	BluetoothAdapterState _adapterState = BluetoothAdapterState.Unknown;
	bool _disposed;

	public PlatformBleTransport()
	{
		_delegate = new IosCentralDelegate(this);
		if (MainThread.IsMainThread)
		{
			_central = new CBCentralManager(_delegate, null);
		}
		else
		{
			MainThread.BeginInvokeOnMainThread(() => _central = new CBCentralManager(_delegate, null));
		}
	}

	public bool IsSupported => true;

	public BluetoothManagerPlatformInfo Platform { get; } =
		new(true, "CoreBluetooth", "CBPeripheral.Identifier");

	public BluetoothAdapterState AdapterState => _adapterState;

	public event EventHandler<AdapterStateChangedEventArgs>? AdapterStateChanged;

	public event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscovered;

	public event EventHandler<NativeConnectionEventArgs>? ConnectionChanged;

	public event EventHandler<CharacteristicChangedEventArgs>? CharacteristicChanged;

	public event EventHandler<RssiUpdatedEventArgs>? RssiUpdated;

	public async Task StartScanAsync(ScanOptions options, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
		var central = RequireCentral();

		CBUUID[]? services = options.ServiceUuids is { Count: > 0 }
			? options.ServiceUuids.Select(uuid => CBUUID.FromString(uuid.ToString())).ToArray()
			: null;

		var scanOptions = new PeripheralScanningOptions { AllowDuplicatesKey = options.AllowDuplicates };
		InvokeOnMain(() => central.ScanForPeripherals(services, scanOptions));
	}

	public Task StopScanAsync()
	{
		if (_central is { IsScanning: true })
			InvokeOnMain(() => _central.StopScan());

		return Task.CompletedTask;
	}

	public async Task ConnectNativeAsync(string deviceId, ConnectOptions options, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

		var peripheral = GetOrRestorePeripheral(deviceId);
		var session = _sessions.GetOrAdd(deviceId, id => new IosGattSession(this, id, peripheral));
		session.Attach(peripheral);
		await session.ConnectAsync(RequireCentral(), cancellationToken).ConfigureAwait(false);
	}

	public async Task DisconnectNativeAsync(string deviceId, CancellationToken cancellationToken)
	{
		if (_sessions.TryRemove(deviceId, out var session))
			await session.DisconnectAsync(RequireCentral()).ConfigureAwait(false);
	}

	public Task<IReadOnlyList<BleService>> DiscoverServicesAsync(string deviceId, CancellationToken cancellationToken) =>
		GetSession(deviceId).DiscoverServicesAsync(cancellationToken);

	public Task WriteNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, byte[] data, BleWriteType writeType, CancellationToken cancellationToken) =>
		GetSession(deviceId).WriteAsync(serviceId, characteristicId, data, writeType, cancellationToken);

	public Task<byte[]> ReadNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken) =>
		GetSession(deviceId).ReadAsync(serviceId, characteristicId, cancellationToken);

	public Task SubscribeNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken) =>
		GetSession(deviceId).SubscribeAsync(serviceId, characteristicId, enabled: true, cancellationToken);

	public Task UnsubscribeNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken) =>
		GetSession(deviceId).SubscribeAsync(serviceId, characteristicId, enabled: false, cancellationToken);

	public Task<int> ReadRssiNativeAsync(string deviceId, CancellationToken cancellationToken) =>
		GetSession(deviceId).ReadRssiAsync(cancellationToken);

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_ = StopScanAsync();
		var central = _central;
		foreach (var session in _sessions.Values)
		{
			try
			{
				if (central is not null)
					_ = session.DisconnectAsync(central);
			}
			catch
			{
			}

			session.Dispose();
		}

		_sessions.Clear();
		_peripherals.Clear();
		_central?.Dispose();
	}

	internal void HandleState(CBManagerState state)
	{
		var previous = _adapterState;
		_adapterState = state switch
		{
			CBManagerState.PoweredOn => BluetoothAdapterState.On,
			CBManagerState.PoweredOff => BluetoothAdapterState.Off,
			CBManagerState.Unauthorized => BluetoothAdapterState.Unauthorized,
			CBManagerState.Unsupported => BluetoothAdapterState.Unsupported,
			CBManagerState.Resetting => BluetoothAdapterState.Resetting,
			_ => BluetoothAdapterState.Unknown
		};

		_ready.TrySetResult(true);
		if (previous != _adapterState)
			AdapterStateChanged?.Invoke(this, new AdapterStateChangedEventArgs(previous, _adapterState));
	}

	internal void HandleDiscovered(CBPeripheral peripheral, NSDictionary advertisement, NSNumber rssi)
	{
		var id = peripheral.Identifier.AsString();
		_peripherals[id] = peripheral;

		var name = ReadLocalName(advertisement) ?? peripheral.Name;
		var services = ReadServiceUuids(advertisement);
		var connectable = ReadConnectable(advertisement);
		var manufacturer = ReadManufacturer(advertisement);
		var device = new BleDevice(id, name, rssi.Int32Value, connectable, services, manufacturer);
		DeviceDiscovered?.Invoke(this, new DeviceDiscoveredEventArgs(device));
	}

	internal void HandleConnected(CBPeripheral peripheral)
	{
		var id = peripheral.Identifier.AsString();
		_peripherals[id] = peripheral;
		if (_sessions.TryGetValue(id, out var session))
			session.OnConnected(peripheral);

		ConnectionChanged?.Invoke(this, new NativeConnectionEventArgs(id, true));
	}

	internal void HandleConnectFailed(CBPeripheral peripheral, NSError? error)
	{
		var id = peripheral.Identifier.AsString();
		if (_sessions.TryGetValue(id, out var session))
			session.OnConnectFailed(error);

		ConnectionChanged?.Invoke(this, new NativeConnectionEventArgs(id, false, error?.LocalizedDescription));
	}

	internal void HandleDisconnected(CBPeripheral peripheral, NSError? error)
	{
		var id = peripheral.Identifier.AsString();
		if (_sessions.TryGetValue(id, out var session))
			session.OnDisconnected();

		ConnectionChanged?.Invoke(this, new NativeConnectionEventArgs(id, false, error?.LocalizedDescription));
	}

	internal void RaiseCharacteristic(BleCharacteristic characteristic, byte[] value) =>
		CharacteristicChanged?.Invoke(this, new CharacteristicChangedEventArgs(characteristic, value));

	internal void RaiseRssi(BleDevice device, int rssi) =>
		RssiUpdated?.Invoke(this, new RssiUpdatedEventArgs(device, rssi));

	async Task EnsureReadyAsync(CancellationToken cancellationToken)
	{
		if (_adapterState is not BluetoothAdapterState.Unknown and not BluetoothAdapterState.Resetting)
			return;

		using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		linked.CancelAfter(TimeSpan.FromSeconds(5));
		try
		{
			await _ready.Task.WaitAsync(linked.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
		}
	}

	CBPeripheral GetOrRestorePeripheral(string deviceId)
	{
		if (_peripherals.TryGetValue(deviceId, out var peripheral))
			return peripheral;

		var uuid = new NSUuid(deviceId);
		var known = RequireCentral().RetrievePeripheralsWithIdentifiers(uuid);
		peripheral = known?.FirstOrDefault()
			?? throw new BluetoothManagerException(
				BluetoothManagerError.ConnectionFailed,
				$"iOS has not seen device {deviceId}. Scan before connecting.");

		_peripherals[deviceId] = peripheral;
		return peripheral;
	}

	IosGattSession GetSession(string deviceId)
	{
		if (_sessions.TryGetValue(deviceId, out var session))
			return session;

		throw new BluetoothManagerException(BluetoothManagerError.NotConnected, $"No CoreBluetooth session for {deviceId}.");
	}

	CBCentralManager RequireCentral() =>
		_central ?? throw new BluetoothManagerException(BluetoothManagerError.AdapterUnavailable, "CBCentralManager is not ready.");

	static void InvokeOnMain(Action action)
	{
		if (MainThread.IsMainThread)
			action();
		else
			MainThread.BeginInvokeOnMainThread(action);
	}

	static string? ReadLocalName(NSDictionary advertisement)
	{
		if (advertisement.TryGetValue(CBAdvertisement.DataLocalNameKey, out var value) && value is NSString name)
			return name;
		return null;
	}

	static IReadOnlyList<Guid> ReadServiceUuids(NSDictionary advertisement)
	{
		if (!advertisement.TryGetValue(CBAdvertisement.DataServiceUUIDsKey, out var value) || value is not NSArray array)
			return [];

		return array.OfType<CBUUID>().Select(ToGuid).ToArray();
	}

	static readonly NSString ConnectableKey = new("kCBAdvDataIsConnectable");

	static bool ReadConnectable(NSDictionary advertisement)
	{
		if (advertisement.TryGetValue(ConnectableKey, out var value) && value is NSNumber number)
			return number.BoolValue;
		return true;
	}

	static IReadOnlyDictionary<int, byte[]> ReadManufacturer(NSDictionary advertisement)
	{
		if (!advertisement.TryGetValue(CBAdvertisement.DataManufacturerDataKey, out var value) || value is not NSData data)
			return new Dictionary<int, byte[]>();

		var bytes = data.ToArray();
		if (bytes.Length < 2)
			return new Dictionary<int, byte[]>();

		var company = bytes[0] | (bytes[1] << 8);
		return new Dictionary<int, byte[]> { [company] = bytes[2..] };
	}

	internal static Guid ToGuid(CBUUID uuid)
	{
		var value = uuid.Uuid;
		if (value.Length == 4)
			return BleUuid.FromShort(Convert.ToUInt16(value, 16));

		if (value.Length == 8)
			return Guid.Parse($"{value}-0000-1000-8000-00805f9b34fb");

		return Guid.Parse(value);
	}
}

sealed class IosCentralDelegate(PlatformBleTransport transport) : CBCentralManagerDelegate
{
	public override void UpdatedState(CBCentralManager central) =>
		transport.HandleState(central.State);

	public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber RSSI) =>
		transport.HandleDiscovered(peripheral, advertisementData, RSSI);

	public override void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral) =>
		transport.HandleConnected(peripheral);

	public override void FailedToConnectPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error) =>
		transport.HandleConnectFailed(peripheral, error);

	public override void DisconnectedPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error) =>
		transport.HandleDisconnected(peripheral, error);
}

sealed class IosGattSession : IDisposable
{
	readonly PlatformBleTransport _transport;
	readonly string _deviceId;
	readonly IosPeripheralDelegate _delegate;
	readonly SemaphoreSlim _ops = new(1, 1);
	CBPeripheral _peripheral;
	TaskCompletionSource<bool>? _connectTcs;
	TaskCompletionSource<IReadOnlyList<BleService>>? _discoverTcs;
	TaskCompletionSource<byte[]>? _readTcs;
	TaskCompletionSource<bool>? _writeTcs;
	TaskCompletionSource<bool>? _notifyTcs;
	TaskCompletionSource<int>? _rssiTcs;
	int _pendingCharacteristicDiscoveries;

	public IosGattSession(PlatformBleTransport transport, string deviceId, CBPeripheral peripheral)
	{
		_transport = transport;
		_deviceId = deviceId;
		_peripheral = peripheral;
		_delegate = new IosPeripheralDelegate(this);
		_peripheral.Delegate = _delegate;
	}

	public void Attach(CBPeripheral peripheral)
	{
		_peripheral = peripheral;
		_peripheral.Delegate = _delegate;
	}

	public async Task ConnectAsync(CBCentralManager central, CancellationToken cancellationToken)
	{
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_connectTcs = NewTcs<bool>(cancellationToken);
			InvokeOnMain(() => central.ConnectPeripheral(_peripheral, new PeripheralConnectionOptions()));
			await _connectTcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_ops.Release();
		}
	}

	public Task DisconnectAsync(CBCentralManager central)
	{
		InvokeOnMain(() => central.CancelPeripheralConnection(_peripheral));
		return Task.CompletedTask;
	}

	public async Task<IReadOnlyList<BleService>> DiscoverServicesAsync(CancellationToken cancellationToken)
	{
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_discoverTcs = NewTcs<IReadOnlyList<BleService>>(cancellationToken);
			_pendingCharacteristicDiscoveries = 0;
			InvokeOnMain(() => _peripheral.DiscoverServices());
			return await _discoverTcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_ops.Release();
		}
	}

	public async Task WriteAsync(Guid serviceId, Guid characteristicId, byte[] data, BleWriteType writeType, CancellationToken cancellationToken)
	{
		var characteristic = FindCharacteristic(serviceId, characteristicId);
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var nativeType = writeType == BleWriteType.WithoutResponse
				? CBCharacteristicWriteType.WithoutResponse
				: CBCharacteristicWriteType.WithResponse;

			_writeTcs = NewTcs<bool>(cancellationToken);
			InvokeOnMain(() => _peripheral.WriteValue(NSData.FromArray(data), characteristic, nativeType));
			if (nativeType == CBCharacteristicWriteType.WithoutResponse)
			{
				_writeTcs.TrySetResult(true);
				return;
			}

			await _writeTcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_ops.Release();
		}
	}

	public async Task<byte[]> ReadAsync(Guid serviceId, Guid characteristicId, CancellationToken cancellationToken)
	{
		var characteristic = FindCharacteristic(serviceId, characteristicId);
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_readTcs = NewTcs<byte[]>(cancellationToken);
			InvokeOnMain(() => _peripheral.ReadValue(characteristic));
			return await _readTcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_ops.Release();
		}
	}

	public async Task SubscribeAsync(Guid serviceId, Guid characteristicId, bool enabled, CancellationToken cancellationToken)
	{
		var characteristic = FindCharacteristic(serviceId, characteristicId);
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_notifyTcs = NewTcs<bool>(cancellationToken);
			InvokeOnMain(() => _peripheral.SetNotifyValue(enabled, characteristic));
			await _notifyTcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_ops.Release();
		}
	}

	public async Task<int> ReadRssiAsync(CancellationToken cancellationToken)
	{
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_rssiTcs = NewTcs<int>(cancellationToken);
			InvokeOnMain(() => _peripheral.ReadRSSI());
			return await _rssiTcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_ops.Release();
		}
	}

	internal void OnConnected(CBPeripheral peripheral)
	{
		Attach(peripheral);
		_connectTcs?.TrySetResult(true);
	}

	internal void OnConnectFailed(NSError? error)
	{
		_connectTcs?.TrySetException(new BluetoothManagerException(
			BluetoothManagerError.ConnectionFailed,
			error?.LocalizedDescription ?? "iOS failed to connect to the peripheral."));
	}

	internal void OnDisconnected()
	{
		_connectTcs?.TrySetCanceled();
	}

	internal void OnServicesDiscovered(NSError? error)
	{
		if (error is not null)
		{
			_discoverTcs?.TrySetException(new BluetoothManagerException(BluetoothManagerError.ConnectionFailed, error.LocalizedDescription));
			return;
		}

		var services = _peripheral.Services;
		if (services is null || services.Length == 0)
		{
			_discoverTcs?.TrySetResult([]);
			return;
		}

		_pendingCharacteristicDiscoveries = services.Length;
		foreach (var service in services)
			_peripheral.DiscoverCharacteristics(service);
	}

	internal void OnCharacteristicsDiscovered(CBService service, NSError? error)
	{
		_ = service;
		_ = error;
		if (Interlocked.Decrement(ref _pendingCharacteristicDiscoveries) > 0)
			return;

		var mapped = new List<BleService>();
		if (_peripheral.Services is { } services)
		{
			foreach (var item in services)
			{
				var serviceId = PlatformBleTransport.ToGuid(item.UUID);
				var characteristics = item.Characteristics?
					.Select(characteristic => new BleCharacteristic(
						_deviceId,
						serviceId,
						PlatformBleTransport.ToGuid(characteristic.UUID),
						MapProperties(characteristic.Properties)))
					.ToArray() ?? [];

				mapped.Add(new BleService(_deviceId, serviceId, item.Primary, characteristics));
			}
		}

		_discoverTcs?.TrySetResult(mapped);
	}

	internal void OnValueUpdated(CBCharacteristic characteristic, NSError? error)
	{
		if (_readTcs is { Task.IsCompleted: false })
		{
			if (error is not null)
			{
				_readTcs.TrySetException(new BluetoothManagerException(BluetoothManagerError.OperationFailed, error.LocalizedDescription));
				return;
			}

			_readTcs.TrySetResult(characteristic.Value?.ToArray() ?? []);
			return;
		}

		if (characteristic.Service is null)
			return;

		var mapped = new BleCharacteristic(
			_deviceId,
			PlatformBleTransport.ToGuid(characteristic.Service.UUID),
			PlatformBleTransport.ToGuid(characteristic.UUID),
			MapProperties(characteristic.Properties));

		_transport.RaiseCharacteristic(mapped, characteristic.Value?.ToArray() ?? []);
	}

	internal void OnWrote(NSError? error)
	{
		if (error is null)
			_writeTcs?.TrySetResult(true);
		else
			_writeTcs?.TrySetException(new BluetoothManagerException(BluetoothManagerError.OperationFailed, error.LocalizedDescription));
	}

	internal void OnNotificationState(NSError? error)
	{
		if (error is null)
			_notifyTcs?.TrySetResult(true);
		else
			_notifyTcs?.TrySetException(new BluetoothManagerException(BluetoothManagerError.OperationFailed, error.LocalizedDescription));
	}

	internal void OnRssi(NSNumber rssi, NSError? error)
	{
		if (error is not null)
		{
			_rssiTcs?.TrySetException(new BluetoothManagerException(BluetoothManagerError.OperationFailed, error.LocalizedDescription));
			return;
		}

		var value = rssi.Int32Value;
		_rssiTcs?.TrySetResult(value);
		_transport.RaiseRssi(new BleDevice(_deviceId, _peripheral.Name, value, true, []), value);
	}

	public void Dispose()
	{
		_peripheral.Delegate = null!;
		_ops.Dispose();
		_connectTcs?.TrySetCanceled();
		_discoverTcs?.TrySetCanceled();
		_readTcs?.TrySetCanceled();
		_writeTcs?.TrySetCanceled();
		_notifyTcs?.TrySetCanceled();
		_rssiTcs?.TrySetCanceled();
	}

	CBCharacteristic FindCharacteristic(Guid serviceId, Guid characteristicId)
	{
		var service = _peripheral.Services?.FirstOrDefault(item => PlatformBleTransport.ToGuid(item.UUID) == serviceId)
			?? throw new BluetoothManagerException(BluetoothManagerError.CharacteristicNotFound, $"Service {serviceId} was not found.");

		return service.Characteristics?.FirstOrDefault(item => PlatformBleTransport.ToGuid(item.UUID) == characteristicId)
			?? throw new BluetoothManagerException(BluetoothManagerError.CharacteristicNotFound, $"Characteristic {characteristicId} was not found.");
	}

	static TaskCompletionSource<T> NewTcs<T>(CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
		return tcs;
	}

	static void InvokeOnMain(Action action)
	{
		if (MainThread.IsMainThread)
			action();
		else
			MainThread.BeginInvokeOnMainThread(action);
	}

	static BleCharacteristicProperties MapProperties(CBCharacteristicProperties properties)
	{
		var mapped = BleCharacteristicProperties.None;
		if (properties.HasFlag(CBCharacteristicProperties.Broadcast))
			mapped |= BleCharacteristicProperties.Broadcast;
		if (properties.HasFlag(CBCharacteristicProperties.Read))
			mapped |= BleCharacteristicProperties.Read;
		if (properties.HasFlag(CBCharacteristicProperties.WriteWithoutResponse))
			mapped |= BleCharacteristicProperties.WriteWithoutResponse;
		if (properties.HasFlag(CBCharacteristicProperties.Write))
			mapped |= BleCharacteristicProperties.Write;
		if (properties.HasFlag(CBCharacteristicProperties.Notify))
			mapped |= BleCharacteristicProperties.Notify;
		if (properties.HasFlag(CBCharacteristicProperties.Indicate))
			mapped |= BleCharacteristicProperties.Indicate;
		if (properties.HasFlag(CBCharacteristicProperties.AuthenticatedSignedWrites))
			mapped |= BleCharacteristicProperties.AuthenticatedSignedWrites;
		if (properties.HasFlag(CBCharacteristicProperties.ExtendedProperties))
			mapped |= BleCharacteristicProperties.ExtendedProperties;
		return mapped;
	}
}

sealed class IosPeripheralDelegate(IosGattSession session) : CBPeripheralDelegate
{
	public override void DiscoveredService(CBPeripheral peripheral, NSError? error) =>
		session.OnServicesDiscovered(error);

	public override void DiscoveredCharacteristics(CBPeripheral peripheral, CBService service, NSError? error) =>
		session.OnCharacteristicsDiscovered(service, error);

	public override void UpdatedCharacterteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error) =>
		session.OnValueUpdated(characteristic, error);

	public override void WroteCharacteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error) =>
		session.OnWrote(error);

	public override void UpdatedNotificationState(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error) =>
		session.OnNotificationState(error);

	public override void RssiRead(CBPeripheral peripheral, NSNumber rssi, NSError? error) =>
		session.OnRssi(rssi, error);
}
#endif
