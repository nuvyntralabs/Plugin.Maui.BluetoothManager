namespace Plugin.Maui.BluetoothManager.Tests;

sealed class FakeClock : IClock
{
	public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

	public TimeSpan LastDelay { get; private set; }

	public void Advance(TimeSpan duration) => UtcNow += duration;

	public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
	{
		LastDelay = delay;
		cancellationToken.ThrowIfCancellationRequested();
		UtcNow += delay;
		return Task.CompletedTask;
	}
}

sealed class FakePermissions : IBluetoothPermissionGateway
{
	public BluetoothPermissionStatus Status { get; set; } = BluetoothPermissionStatus.Granted;

	public int RequestCount { get; private set; }

	public Task<BluetoothPermissionStatus> CheckAsync() => Task.FromResult(Status);

	public Task<BluetoothPermissionStatus> RequestAsync()
	{
		RequestCount++;
		return Task.FromResult(Status);
	}
}

sealed class FakeBleTransport : IBleTransport
{
	readonly Dictionary<string, List<BleService>> _services = new(StringComparer.OrdinalIgnoreCase);
	readonly Dictionary<string, byte[]> _values = new(StringComparer.OrdinalIgnoreCase);
	readonly HashSet<string> _connected = new(StringComparer.OrdinalIgnoreCase);

	public bool IsSupported { get; set; } = true;

	public BluetoothManagerPlatformInfo Platform { get; } = new(true, "fake", "id");

	public BluetoothAdapterState AdapterState { get; private set; } = BluetoothAdapterState.On;

	public bool ConnectShouldTimeout { get; set; }

	public bool ConnectShouldFail { get; set; }

	public int WritesBeforeSuccess { get; set; }

	public int WriteAttempts { get; private set; }

	public int ConnectAttempts { get; private set; }

	public bool IsScanning { get; private set; }

	public List<BleDevice> PendingDiscoveries { get; } = [];

	public event EventHandler<AdapterStateChangedEventArgs>? AdapterStateChanged;

	public event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscovered;

	public event EventHandler<NativeConnectionEventArgs>? ConnectionChanged;

	public event EventHandler<CharacteristicChangedEventArgs>? CharacteristicChanged;

	public event EventHandler<RssiUpdatedEventArgs>? RssiUpdated;

	public Task StartScanAsync(ScanOptions options, CancellationToken cancellationToken)
	{
		IsScanning = true;
		foreach (var device in PendingDiscoveries)
			DeviceDiscovered?.Invoke(this, new DeviceDiscoveredEventArgs(device));
		return Task.CompletedTask;
	}

	public Task StopScanAsync()
	{
		IsScanning = false;
		return Task.CompletedTask;
	}

	public async Task ConnectNativeAsync(string deviceId, ConnectOptions options, CancellationToken cancellationToken)
	{
		ConnectAttempts++;
		if (ConnectShouldTimeout)
		{
			await Task.Delay(Timeout.Infinite, cancellationToken);
		}

		if (ConnectShouldFail)
		{
			throw new InvalidOperationException("simulated connect failure");
		}

		_connected.Add(deviceId);
		ConnectionChanged?.Invoke(this, new NativeConnectionEventArgs(deviceId, true));
	}

	public Task DisconnectNativeAsync(string deviceId, CancellationToken cancellationToken)
	{
		_connected.Remove(deviceId);
		ConnectionChanged?.Invoke(this, new NativeConnectionEventArgs(deviceId, false, "user"));
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<BleService>> DiscoverServicesAsync(string deviceId, CancellationToken cancellationToken)
	{
		if (_services.TryGetValue(deviceId, out var services))
			return Task.FromResult<IReadOnlyList<BleService>>(services);

		return Task.FromResult<IReadOnlyList<BleService>>([]);
	}

	public Task WriteNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, byte[] data, BleWriteType writeType, CancellationToken cancellationToken)
	{
		WriteAttempts++;
		if (WriteAttempts <= WritesBeforeSuccess)
			throw new InvalidOperationException("transient write failure");

		_values[Key(deviceId, serviceId, characteristicId)] = data;
		return Task.CompletedTask;
	}

	public Task<byte[]> ReadNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken)
	{
		_values.TryGetValue(Key(deviceId, serviceId, characteristicId), out var value);
		return Task.FromResult(value ?? []);
	}

	public Task SubscribeNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	public Task UnsubscribeNativeAsync(string deviceId, Guid serviceId, Guid characteristicId, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	public Task<int> ReadRssiNativeAsync(string deviceId, CancellationToken cancellationToken) =>
		Task.FromResult(-55);

	public void Dispose()
	{
	}

	public void SetAdapterState(BluetoothAdapterState state)
	{
		var previous = AdapterState;
		AdapterState = state;
		AdapterStateChanged?.Invoke(this, new AdapterStateChangedEventArgs(previous, state));
	}

	public void Discover(BleDevice device) =>
		DeviceDiscovered?.Invoke(this, new DeviceDiscoveredEventArgs(device));

	public void Drop(string deviceId, string? message = "link lost")
	{
		_connected.Remove(deviceId);
		ConnectionChanged?.Invoke(this, new NativeConnectionEventArgs(deviceId, false, message));
	}

	public void Notify(BleCharacteristic characteristic, byte[] value) =>
		CharacteristicChanged?.Invoke(this, new CharacteristicChangedEventArgs(characteristic, value));

	public void AddService(string deviceId, BleService service)
	{
		if (!_services.TryGetValue(deviceId, out var list))
		{
			list = [];
			_services[deviceId] = list;
		}

		list.Add(service);
	}

	static string Key(string deviceId, Guid serviceId, Guid characteristicId) =>
		$"{deviceId}:{serviceId}:{characteristicId}";
}

static class ManagerHarness
{
	public static (BluetoothManagerImplementation Manager, FakeBleTransport Transport, FakePermissions Permissions, FakeClock Clock) Create(
		Action<BluetoothManagerOptions>? configure = null,
		Action<FakeBleTransport>? transport = null,
		Action<FakePermissions>? permissions = null)
	{
		var options = new BluetoothManagerOptions
		{
			EnableLogging = false,
			DefaultScanDuration = TimeSpan.FromMilliseconds(10),
			ConnectionTimeout = TimeSpan.FromMilliseconds(50),
			OperationRetryDelay = TimeSpan.FromMilliseconds(1),
			ReconnectInitialDelay = TimeSpan.FromMilliseconds(1),
			ReconnectMaxDelay = TimeSpan.FromMilliseconds(4)
		};
		configure?.Invoke(options);

		var fakeTransport = new FakeBleTransport();
		transport?.Invoke(fakeTransport);

		var fakePermissions = new FakePermissions();
		permissions?.Invoke(fakePermissions);

		var clock = new FakeClock();
		var manager = BluetoothManager.Create(options, fakeTransport, fakePermissions, clock);
		return (manager, fakeTransport, fakePermissions, clock);
	}

	public static BleDevice Device(string id = "AA:BB:CC:DD:EE:FF", string? name = "POS-01", int rssi = -40, params Guid[] services) =>
		new(id, name, rssi, true, services);
}
