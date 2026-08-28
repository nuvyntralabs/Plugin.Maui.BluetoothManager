#if ANDROID
using System.Collections.Concurrent;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Java.Util;
using AndroidApp = Android.App.Application;
using AndroidBluetoothManager = Android.Bluetooth.BluetoothManager;
using ScanMode = Android.Bluetooth.LE.ScanMode;

namespace Plugin.Maui.BluetoothManager;

sealed class PlatformBleTransport : IBleTransport
{
	readonly ConcurrentDictionary<string, AndroidGattSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
	readonly AdapterStateReceiver _receiver;
	readonly AndroidScanCallback _scanCallback;
	readonly AndroidBluetoothManager? _bluetoothManager;
	readonly BluetoothAdapter? _adapter;
	bool _disposed;

	public PlatformBleTransport()
	{
		_bluetoothManager = AndroidApp.Context.GetSystemService(Context.BluetoothService) as AndroidBluetoothManager;
		_adapter = _bluetoothManager?.Adapter;
		_scanCallback = new AndroidScanCallback(this);
		_receiver = new AdapterStateReceiver(this);

		var filter = new IntentFilter(BluetoothAdapter.ActionStateChanged);
		AndroidApp.Context.RegisterReceiver(_receiver, filter);
	}

	public bool IsSupported => _adapter is not null;

	public BluetoothManagerPlatformInfo Platform { get; } =
		new(true, "Android BluetoothGatt", "MAC address");

	public BluetoothAdapterState AdapterState => MapAdapter(_adapter);

	public event EventHandler<AdapterStateChangedEventArgs>? AdapterStateChanged;

	public event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscovered;

	public event EventHandler<NativeConnectionEventArgs>? ConnectionChanged;

	public event EventHandler<CharacteristicChangedEventArgs>? CharacteristicChanged;

	public event EventHandler<RssiUpdatedEventArgs>? RssiUpdated;

	public Task StartScanAsync(ScanOptions options, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		var scanner = _adapter?.BluetoothLeScanner
			?? throw new BluetoothManagerException(BluetoothManagerError.AdapterUnavailable, "Bluetooth LE scanner is not available.");

		var filters = new List<ScanFilter>();
		if (options.ServiceUuids is { Count: > 0 })
		{
			foreach (var uuid in options.ServiceUuids)
			{
				filters.Add(new ScanFilter.Builder()
					.SetServiceUuid(new ParcelUuid(ToJavaUuid(uuid)))
					.Build()!);
			}
		}

		var settings = new ScanSettings.Builder()
			.SetScanMode(ScanMode.LowLatency)
			.Build();

		_scanCallback.AllowDuplicates = options.AllowDuplicates;
		scanner.StartScan(filters.Count == 0 ? null : filters, settings, _scanCallback);
		return Task.CompletedTask;
	}

	public Task StopScanAsync()
	{
		try
		{
			_adapter?.BluetoothLeScanner?.StopScan(_scanCallback);
		}
		catch (Java.Lang.Exception)
		{
		}

		return Task.CompletedTask;
	}

	public async Task ConnectNativeAsync(string deviceId, ConnectOptions options, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

		if (_adapter is null)
			throw new BluetoothManagerException(BluetoothManagerError.AdapterUnavailable, "Bluetooth adapter is not available.");

		var session = _sessions.GetOrAdd(deviceId, id => new AndroidGattSession(this, id));
		await session.ConnectAsync(_adapter, cancellationToken).ConfigureAwait(false);
	}

	public async Task DisconnectNativeAsync(string deviceId, CancellationToken cancellationToken)
	{
		if (_sessions.TryRemove(deviceId, out var session))
			await session.DisconnectAsync().ConfigureAwait(false);
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
		try
		{
			AndroidApp.Context.UnregisterReceiver(_receiver);
		}
		catch (Java.Lang.Exception)
		{
		}

		_ = StopScanAsync();
		foreach (var session in _sessions.Values)
			session.Dispose();

		_sessions.Clear();
	}

	internal void RaiseDiscovered(BleDevice device) =>
		DeviceDiscovered?.Invoke(this, new DeviceDiscoveredEventArgs(device));

	internal void RaiseConnection(string deviceId, bool connected, string? error) =>
		ConnectionChanged?.Invoke(this, new NativeConnectionEventArgs(deviceId, connected, error));

	internal void RaiseCharacteristic(BleCharacteristic characteristic, byte[] value) =>
		CharacteristicChanged?.Invoke(this, new CharacteristicChangedEventArgs(characteristic, value));

	internal void RaiseRssi(BleDevice device, int rssi) =>
		RssiUpdated?.Invoke(this, new RssiUpdatedEventArgs(device, rssi));

	internal void RaiseAdapterState(BluetoothAdapterState previous, BluetoothAdapterState current)
	{
		if (previous == current)
			return;

		AdapterStateChanged?.Invoke(this, new AdapterStateChangedEventArgs(previous, current));
	}

	AndroidGattSession GetSession(string deviceId)
	{
		if (_sessions.TryGetValue(deviceId, out var session))
			return session;

		throw new BluetoothManagerException(BluetoothManagerError.NotConnected, $"No GATT session for {deviceId}.");
	}

	internal static BluetoothAdapterState MapAdapter(BluetoothAdapter? adapter)
	{
		if (adapter is null)
			return BluetoothAdapterState.Unsupported;

		return adapter.State switch
		{
			State.On => BluetoothAdapterState.On,
			State.Off => BluetoothAdapterState.Off,
			State.TurningOn or State.TurningOff => BluetoothAdapterState.Resetting,
			_ => BluetoothAdapterState.Unknown
		};
	}

	internal static UUID ToJavaUuid(Guid guid) => UUID.FromString(guid.ToString())!;

	internal static Guid ToNetGuid(UUID uuid) => Guid.Parse(uuid.ToString()!);
}

sealed class AndroidScanCallback(PlatformBleTransport transport) : ScanCallback
{
	public bool AllowDuplicates { get; set; }

	public override void OnScanResult(ScanCallbackType callbackType, ScanResult? result)
	{
		if (result?.Device is null)
			return;

		transport.RaiseDiscovered(ToDevice(result));
	}

	public override void OnBatchScanResults(IList<ScanResult>? results)
	{
		if (results is null)
			return;

		foreach (var result in results)
			OnScanResult(ScanCallbackType.AllMatches, result);
	}

	static BleDevice ToDevice(ScanResult result)
	{
		var record = result.ScanRecord;
		var name = record?.DeviceName ?? result.Device?.Name;
		var services = record?.ServiceUuids?
			.Select(uuid => PlatformBleTransport.ToNetGuid(uuid.Uuid!))
			.ToArray() ?? [];

		var manufacturer = new Dictionary<int, byte[]>();
		var sparse = record?.ManufacturerSpecificData;
		if (sparse is not null && record is not null)
		{
			for (var i = 0; i < sparse.Size(); i++)
			{
				var companyId = sparse.KeyAt(i);
				var value = record.GetManufacturerSpecificData(companyId);
				if (value is not null)
					manufacturer[companyId] = value;
			}
		}

		var connectable = !OperatingSystem.IsAndroidVersionAtLeast(26) || result.IsConnectable;
		return new BleDevice(result.Device!.Address!, name, result.Rssi, connectable, services, manufacturer);
	}
}

sealed class AdapterStateReceiver(PlatformBleTransport transport) : BroadcastReceiver
{
	BluetoothAdapterState _last = transport.AdapterState;

	public override void OnReceive(Context? context, Intent? intent)
	{
		if (intent?.Action != BluetoothAdapter.ActionStateChanged)
			return;

		var current = transport.AdapterState;
		var previous = _last;
		_last = current;
		transport.RaiseAdapterState(previous, current);
	}
}

sealed class AndroidGattSession : IDisposable
{
	readonly PlatformBleTransport _transport;
	readonly string _deviceId;
	readonly SemaphoreSlim _ops = new(1, 1);
	readonly AndroidGattCallback _callback;
	BluetoothGatt? _gatt;
	TaskCompletionSource<bool>? _connectTcs;
	TaskCompletionSource<IReadOnlyList<BleService>>? _discoverTcs;
	TaskCompletionSource<byte[]>? _readTcs;
	TaskCompletionSource<bool>? _writeTcs;
	TaskCompletionSource<bool>? _descriptorTcs;
	TaskCompletionSource<int>? _rssiTcs;

	public AndroidGattSession(PlatformBleTransport transport, string deviceId)
	{
		_transport = transport;
		_deviceId = deviceId;
		_callback = new AndroidGattCallback(this);
	}

	public async Task ConnectAsync(BluetoothAdapter adapter, CancellationToken cancellationToken)
	{
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var device = adapter.GetRemoteDevice(_deviceId)
				?? throw new BluetoothManagerException(BluetoothManagerError.ConnectionFailed, $"Android could not resolve {_deviceId}.");

			_connectTcs = NewTcs<bool>(cancellationToken);
			_gatt = OperatingSystem.IsAndroidVersionAtLeast(23)
				? device.ConnectGatt(AndroidApp.Context, false, _callback, BluetoothTransports.Le)
				: device.ConnectGatt(AndroidApp.Context, false, _callback);

			if (_gatt is null)
				throw new BluetoothManagerException(BluetoothManagerError.ConnectionFailed, "ConnectGatt returned null.");

			await _connectTcs.Task.ConfigureAwait(false);
			_gatt.RequestMtu(517);
		}
		finally
		{
			_ops.Release();
		}
	}

	public async Task DisconnectAsync()
	{
		try
		{
			_gatt?.Disconnect();
		}
		catch (Java.Lang.Exception)
		{
		}

		Dispose();
	}

	public async Task<IReadOnlyList<BleService>> DiscoverServicesAsync(CancellationToken cancellationToken)
	{
		var gatt = RequireGatt();
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_discoverTcs = NewTcs<IReadOnlyList<BleService>>(cancellationToken);
			if (!gatt.DiscoverServices())
				throw new BluetoothManagerException(BluetoothManagerError.ConnectionFailed, "DiscoverServices was rejected.");

			return await _discoverTcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_ops.Release();
		}
	}

	public async Task WriteAsync(Guid serviceId, Guid characteristicId, byte[] data, BleWriteType writeType, CancellationToken cancellationToken)
	{
		var gatt = RequireGatt();
		var characteristic = FindCharacteristic(gatt, serviceId, characteristicId);
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_writeTcs = NewTcs<bool>(cancellationToken);
			var nativeType = writeType == BleWriteType.WithoutResponse ? GattWriteType.NoResponse : GattWriteType.Default;
			if (!WriteCompat(gatt, characteristic, data, nativeType))
				throw new BluetoothManagerException(BluetoothManagerError.OperationFailed, "WriteCharacteristic was rejected.");

			if (nativeType == GattWriteType.NoResponse)
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
		var gatt = RequireGatt();
		var characteristic = FindCharacteristic(gatt, serviceId, characteristicId);
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_readTcs = NewTcs<byte[]>(cancellationToken);
			if (!gatt.ReadCharacteristic(characteristic))
				throw new BluetoothManagerException(BluetoothManagerError.OperationFailed, "ReadCharacteristic was rejected.");

			return await _readTcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_ops.Release();
		}
	}

	public async Task SubscribeAsync(Guid serviceId, Guid characteristicId, bool enabled, CancellationToken cancellationToken)
	{
		var gatt = RequireGatt();
		var characteristic = FindCharacteristic(gatt, serviceId, characteristicId);
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (!gatt.SetCharacteristicNotification(characteristic, enabled))
				throw new BluetoothManagerException(BluetoothManagerError.OperationFailed, "SetCharacteristicNotification was rejected.");

			var descriptor = characteristic.GetDescriptor(PlatformBleTransport.ToJavaUuid(BleUuid.ClientCharacteristicConfiguration));
			if (descriptor is null)
				return;

			var value = !enabled
				? BluetoothGattDescriptor.DisableNotificationValue
				: characteristic.Properties.HasFlag(GattProperty.Indicate)
					? BluetoothGattDescriptor.EnableIndicationValue
					: BluetoothGattDescriptor.EnableNotificationValue;

			_descriptorTcs = NewTcs<bool>(cancellationToken);
			if (!WriteDescriptorCompat(gatt, descriptor, [.. value!]))
				throw new BluetoothManagerException(BluetoothManagerError.OperationFailed, "Writing the CCCD descriptor was rejected.");

			await _descriptorTcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_ops.Release();
		}
	}

	public async Task<int> ReadRssiAsync(CancellationToken cancellationToken)
	{
		var gatt = RequireGatt();
		await _ops.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_rssiTcs = NewTcs<int>(cancellationToken);
			if (!gatt.ReadRemoteRssi())
				throw new BluetoothManagerException(BluetoothManagerError.OperationFailed, "ReadRemoteRssi was rejected.");

			return await _rssiTcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_ops.Release();
		}
	}

	internal void OnConnectionStateChange(BluetoothGatt? gatt, GattStatus status, ProfileState newState)
	{
		if (newState == ProfileState.Connected && status == GattStatus.Success)
		{
			_gatt = gatt;
			_connectTcs?.TrySetResult(true);
			_transport.RaiseConnection(_deviceId, true, null);
			return;
		}

		var message = status == GattStatus.Success ? null : status.ToString();
		if (_connectTcs is { Task.IsCompleted: false })
		{
			_connectTcs.TrySetException(new BluetoothManagerException(
				BluetoothManagerError.ConnectionFailed,
				$"Android GATT connect failed ({status})."));
		}

		_transport.RaiseConnection(_deviceId, false, message);
	}

	internal void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status)
	{
		if (gatt is null || status != GattStatus.Success)
		{
			_discoverTcs?.TrySetException(new BluetoothManagerException(
				BluetoothManagerError.ConnectionFailed,
				$"Service discovery failed ({status})."));
			return;
		}

		var services = new List<BleService>();
		if (gatt.Services is { } nativeServices)
		{
			foreach (var service in nativeServices)
			{
				if (service.Uuid is null)
					continue;

				var serviceId = PlatformBleTransport.ToNetGuid(service.Uuid);
				var characteristics = new List<BleCharacteristic>();
				if (service.Characteristics is { } nativeCharacteristics)
				{
					foreach (var characteristic in nativeCharacteristics)
					{
						if (characteristic.Uuid is null)
							continue;

						characteristics.Add(new BleCharacteristic(
							_deviceId,
							serviceId,
							PlatformBleTransport.ToNetGuid(characteristic.Uuid),
							MapProperties(characteristic.Properties)));
					}
				}

				services.Add(new BleService(_deviceId, serviceId, service.Type == GattServiceType.Primary, characteristics));
			}
		}

		_discoverTcs?.TrySetResult(services);
	}

	internal void OnCharacteristicRead(BluetoothGattCharacteristic? characteristic, byte[]? value, GattStatus status)
	{
		if (status != GattStatus.Success)
		{
			_readTcs?.TrySetException(new BluetoothManagerException(BluetoothManagerError.OperationFailed, $"Read failed ({status})."));
			return;
		}

		_readTcs?.TrySetResult(value ?? characteristic?.GetValue() ?? []);
	}

	internal void OnCharacteristicWrite(GattStatus status)
	{
		if (status == GattStatus.Success)
			_writeTcs?.TrySetResult(true);
		else
			_writeTcs?.TrySetException(new BluetoothManagerException(BluetoothManagerError.OperationFailed, $"Write failed ({status})."));
	}

	internal void OnCharacteristicChanged(BluetoothGattCharacteristic characteristic, byte[]? value)
	{
		if (characteristic.Uuid is null || characteristic.Service?.Uuid is null)
			return;

		var mapped = new BleCharacteristic(
			_deviceId,
			PlatformBleTransport.ToNetGuid(characteristic.Service.Uuid),
			PlatformBleTransport.ToNetGuid(characteristic.Uuid),
			MapProperties(characteristic.Properties));

		_transport.RaiseCharacteristic(mapped, value ?? characteristic.GetValue() ?? []);
	}

	internal void OnDescriptorWrite(GattStatus status)
	{
		if (status == GattStatus.Success)
			_descriptorTcs?.TrySetResult(true);
		else
			_descriptorTcs?.TrySetException(new BluetoothManagerException(BluetoothManagerError.OperationFailed, $"Descriptor write failed ({status})."));
	}

	internal void OnRssi(int rssi, GattStatus status)
	{
		if (status != GattStatus.Success)
		{
			_rssiTcs?.TrySetException(new BluetoothManagerException(BluetoothManagerError.OperationFailed, $"RSSI read failed ({status})."));
			return;
		}

		_rssiTcs?.TrySetResult(rssi);
		_transport.RaiseRssi(new BleDevice(_deviceId, _gatt?.Device?.Name, rssi, true, []), rssi);
	}

	public void Dispose()
	{
		try
		{
			_gatt?.Close();
		}
		catch (Java.Lang.Exception)
		{
		}

		_gatt = null;
		_ops.Dispose();
		_connectTcs?.TrySetCanceled();
		_discoverTcs?.TrySetCanceled();
		_readTcs?.TrySetCanceled();
		_writeTcs?.TrySetCanceled();
		_descriptorTcs?.TrySetCanceled();
		_rssiTcs?.TrySetCanceled();
	}

	BluetoothGatt RequireGatt() =>
		_gatt ?? throw new BluetoothManagerException(BluetoothManagerError.NotConnected, $"Device {_deviceId} is not connected.");

	BluetoothGattCharacteristic FindCharacteristic(BluetoothGatt gatt, Guid serviceId, Guid characteristicId)
	{
		var service = gatt.GetService(PlatformBleTransport.ToJavaUuid(serviceId))
			?? throw new BluetoothManagerException(BluetoothManagerError.CharacteristicNotFound, $"Service {serviceId} was not found.");

		return service.GetCharacteristic(PlatformBleTransport.ToJavaUuid(characteristicId))
			?? throw new BluetoothManagerException(BluetoothManagerError.CharacteristicNotFound, $"Characteristic {characteristicId} was not found.");
	}

	static TaskCompletionSource<T> NewTcs<T>(CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
		return tcs;
	}

	static bool WriteCompat(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] data, GattWriteType writeType)
	{
		if (OperatingSystem.IsAndroidVersionAtLeast(33))
			return gatt.WriteCharacteristic(characteristic, data, (int)writeType) == 0;

		characteristic.SetValue(data);
		characteristic.WriteType = writeType;
		return gatt.WriteCharacteristic(characteristic);
	}

	static bool WriteDescriptorCompat(BluetoothGatt gatt, BluetoothGattDescriptor descriptor, byte[] value)
	{
		if (OperatingSystem.IsAndroidVersionAtLeast(33))
			return gatt.WriteDescriptor(descriptor, value) == 0;

		descriptor.SetValue(value);
		return gatt.WriteDescriptor(descriptor);
	}

	static BleCharacteristicProperties MapProperties(GattProperty properties)
	{
		var mapped = BleCharacteristicProperties.None;
		if (properties.HasFlag(GattProperty.Broadcast))
			mapped |= BleCharacteristicProperties.Broadcast;
		if (properties.HasFlag(GattProperty.Read))
			mapped |= BleCharacteristicProperties.Read;
		if (properties.HasFlag(GattProperty.WriteNoResponse))
			mapped |= BleCharacteristicProperties.WriteWithoutResponse;
		if (properties.HasFlag(GattProperty.Write))
			mapped |= BleCharacteristicProperties.Write;
		if (properties.HasFlag(GattProperty.Notify))
			mapped |= BleCharacteristicProperties.Notify;
		if (properties.HasFlag(GattProperty.Indicate))
			mapped |= BleCharacteristicProperties.Indicate;
		if (properties.HasFlag(GattProperty.SignedWrite))
			mapped |= BleCharacteristicProperties.AuthenticatedSignedWrites;
		if (properties.HasFlag(GattProperty.ExtendedProps))
			mapped |= BleCharacteristicProperties.ExtendedProperties;
		return mapped;
	}
}

sealed class AndroidGattCallback(AndroidGattSession session) : BluetoothGattCallback
{
	public override void OnConnectionStateChange(BluetoothGatt? gatt, [GeneratedEnum] GattStatus status, [GeneratedEnum] ProfileState newState) =>
		session.OnConnectionStateChange(gatt, status, newState);

	public override void OnServicesDiscovered(BluetoothGatt? gatt, [GeneratedEnum] GattStatus status) =>
		session.OnServicesDiscovered(gatt, status);

	public override void OnCharacteristicRead(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, [GeneratedEnum] GattStatus status) =>
		session.OnCharacteristicRead(characteristic, characteristic?.GetValue(), status);

	public override void OnCharacteristicRead(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, byte[]? value, [GeneratedEnum] GattStatus status) =>
		session.OnCharacteristicRead(characteristic, value, status);

	public override void OnCharacteristicWrite(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, [GeneratedEnum] GattStatus status) =>
		session.OnCharacteristicWrite(status);

	public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic)
	{
		if (characteristic is not null)
			session.OnCharacteristicChanged(characteristic, characteristic.GetValue());
	}

	public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, byte[] value)
	{
		if (characteristic is not null)
			session.OnCharacteristicChanged(characteristic, value);
	}

	public override void OnDescriptorWrite(BluetoothGatt? gatt, BluetoothGattDescriptor? descriptor, [GeneratedEnum] GattStatus status) =>
		session.OnDescriptorWrite(status);

	public override void OnReadRemoteRssi(BluetoothGatt? gatt, int rssi, [GeneratedEnum] GattStatus status) =>
		session.OnRssi(rssi, status);
}
#endif
