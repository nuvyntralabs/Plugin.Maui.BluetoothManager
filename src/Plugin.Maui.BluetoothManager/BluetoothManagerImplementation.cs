namespace Plugin.Maui.BluetoothManager;

sealed class BluetoothManagerImplementation : IBluetoothManager, IDisposable
{
	readonly BluetoothManagerOptions _options;
	readonly IBleTransport _transport;
	readonly IBluetoothPermissionGateway _permissions;
	readonly IClock _clock;
	readonly object _gate = new();
	readonly Dictionary<string, ConnectionSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
	readonly Dictionary<string, BleDevice> _discovered = new(StringComparer.OrdinalIgnoreCase);
	readonly SemaphoreSlim _scanLock = new(1, 1);

	IBluetoothManagerLogger _logger = new DebugBluetoothManagerLogger();
	bool _loggingEnabled;
	bool _isScanning;
	string? _primaryDeviceId;
	BluetoothPermissionStatus _lastPermission = BluetoothPermissionStatus.Unknown;
	CancellationTokenSource? _scanCts;
	ScanOptions? _activeScanOptions;

	internal BluetoothManagerImplementation(
		BluetoothManagerOptions options,
		IBleTransport transport,
		IBluetoothPermissionGateway permissions,
		IClock clock)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_transport = transport ?? throw new ArgumentNullException(nameof(transport));
		_permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
		_clock = clock ?? throw new ArgumentNullException(nameof(clock));

		if (options.EnableLogging)
		{
			_loggingEnabled = true;
			_logger = options.Logger ?? new DebugBluetoothManagerLogger();
		}

		_transport.AdapterStateChanged += OnAdapterStateChanged;
		_transport.DeviceDiscovered += OnNativeDeviceDiscovered;
		_transport.ConnectionChanged += OnNativeConnectionChanged;
		_transport.CharacteristicChanged += OnNativeCharacteristicChanged;
		_transport.RssiUpdated += OnNativeRssiUpdated;
	}

	public bool IsSupported => _transport.IsSupported;

	public BluetoothManagerPlatformInfo Platform => _transport.Platform;

	public BluetoothAdapterState AdapterState => _transport.AdapterState;

	public BluetoothConnectionState ConnectionState
	{
		get
		{
			lock (_gate)
			{
				return TryGetPrimarySessionLocked()?.State ?? BluetoothConnectionState.Disconnected;
			}
		}
	}

	public BleDevice? ConnectedDevice
	{
		get
		{
			lock (_gate)
			{
				var session = TryGetPrimarySessionLocked();
				return session is { State: BluetoothConnectionState.Connected } ? session.Device : null;
			}
		}
	}

	public IReadOnlyList<BleDevice> ConnectedDevices
	{
		get
		{
			lock (_gate)
			{
				return _sessions.Values
					.Where(session => session.State == BluetoothConnectionState.Connected)
					.Select(session => session.Device)
					.ToArray();
			}
		}
	}

	public bool IsScanning
	{
		get
		{
			lock (_gate)
				return _isScanning;
		}
	}

	public BluetoothSnapshot Snapshot
	{
		get
		{
			lock (_gate)
			{
				var primary = TryGetPrimarySessionLocked();
				var connected = _sessions.Values
					.Where(session => session.State == BluetoothConnectionState.Connected)
					.Select(session => session.Device)
					.ToArray();

				return new BluetoothSnapshot(
					_clock.UtcNow,
					_transport.AdapterState,
					_lastPermission,
					primary?.State ?? BluetoothConnectionState.Disconnected,
					primary is { State: BluetoothConnectionState.Connected } ? primary.Device : null,
					connected,
					_isScanning);
			}
		}
	}

	public event EventHandler<AdapterStateChangedEventArgs>? AdapterStateChanged;

	public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

	public event EventHandler<DeviceDisconnectedEventArgs>? DeviceDisconnected;

	public event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscovered;

	public event EventHandler<CharacteristicChangedEventArgs>? CharacteristicChanged;

	public event EventHandler<RssiUpdatedEventArgs>? RssiUpdated;

	public async Task<BluetoothPermissionStatus> CheckPermissionsAsync()
	{
		var status = await _permissions.CheckAsync().ConfigureAwait(false);
		_lastPermission = status;
		return status;
	}

	public async Task<BluetoothPermissionStatus> RequestPermissionsAsync()
	{
		Log(BluetoothManagerLogLevel.Information, "Requesting Bluetooth permissions.");
		var status = await _permissions.RequestAsync().ConfigureAwait(false);
		_lastPermission = status;
		Log(BluetoothManagerLogLevel.Information, $"Permission result: {status}");
		return status;
	}

	public async Task<IReadOnlyList<BleDevice>> ScanAsync(ScanOptions? options = null, CancellationToken cancellationToken = default)
	{
		options ??= new ScanOptions();
		await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

		await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		CancellationTokenSource? linked = null;
		try
		{
			lock (_gate)
			{
				_discovered.Clear();
				_activeScanOptions = options;
				_isScanning = true;
			}

			_scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			linked = _scanCts;
			var duration = options.Duration ?? _options.DefaultScanDuration;
			linked.CancelAfter(duration);

			Log(BluetoothManagerLogLevel.Information, $"Starting scan for {duration.TotalSeconds:0.#}s.");
			await _transport.StartScanAsync(options, linked.Token).ConfigureAwait(false);

			try
			{
				await _clock.Delay(duration, linked.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				// Duration elapsed.
			}

			await _transport.StopScanAsync().ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();

			lock (_gate)
			{
				var results = _discovered.Values
					.Where(device => DeviceFilter.Matches(device, options, _options.MinimumRssi))
					.OrderByDescending(device => device.Rssi)
					.ToArray();

				Log(BluetoothManagerLogLevel.Information, $"Scan finished with {results.Length} device(s).");
				return results;
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			await SafeStopScanAsync().ConfigureAwait(false);
			throw;
		}
		catch (BluetoothManagerException)
		{
			await SafeStopScanAsync().ConfigureAwait(false);
			throw;
		}
		catch (Exception ex)
		{
			await SafeStopScanAsync().ConfigureAwait(false);
			throw new BluetoothManagerException(BluetoothManagerError.ScanFailed, "Bluetooth scan failed.", ex);
		}
		finally
		{
			lock (_gate)
			{
				_isScanning = false;
				_activeScanOptions = null;
			}

			if (ReferenceEquals(_scanCts, linked))
				_scanCts = null;

			linked?.Dispose();
			_scanLock.Release();
		}
	}

	public async Task StopScanAsync()
	{
		_scanCts?.Cancel();
		await SafeStopScanAsync().ConfigureAwait(false);
		lock (_gate)
			_isScanning = false;
	}

	public async Task<BleDevice> ConnectAsync(BleDevice device, ConnectOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(device);
		options ??= new ConnectOptions();

		await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

		ConnectionSession session;
		lock (_gate)
		{
			if (_sessions.TryGetValue(device.Id, out var existing) &&
				existing.State is BluetoothConnectionState.Connected or BluetoothConnectionState.Discovering)
			{
				return existing.Device;
			}

			existing?.CancelReconnect();
			session = new ConnectionSession(device, options);
			_sessions[device.Id] = session;
			_primaryDeviceId = device.Id;
		}

		try
		{
			await ConnectSessionAsync(session, isReconnect: false, cancellationToken).ConfigureAwait(false);
			return session.Device;
		}
		catch
		{
			SetState(session, BluetoothConnectionState.Failed);
			throw;
		}
	}

	public async Task DisconnectAsync(BleDevice? device = null, CancellationToken cancellationToken = default)
	{
		ConnectionSession? session;
		lock (_gate)
		{
			session = device is null ? TryGetPrimarySessionLocked() : TryGetSessionLocked(device.Id);
			if (session is null)
				return;

			session.UserRequestedDisconnect = true;
			session.CancelReconnect();
		}

		SetState(session, BluetoothConnectionState.Disconnecting);

		try
		{
			await _transport.DisconnectNativeAsync(session.DeviceId, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Log(BluetoothManagerLogLevel.Warning, $"Disconnect reported an error for {session.DeviceId}.", ex);
		}

		CompleteDisconnect(session, DisconnectReason.UserRequested, "Disconnected by the application.");
	}

	public async Task<IReadOnlyList<BleService>> GetServicesAsync(BleDevice? device = null, CancellationToken cancellationToken = default)
	{
		var session = RequireConnectedSession(device);
		if (session.Services.Count > 0)
			return session.Services;

		var services = await _transport.DiscoverServicesAsync(session.DeviceId, cancellationToken).ConfigureAwait(false);
		session.Services = services;
		return services;
	}

	public async Task<BleCharacteristic?> FindCharacteristicAsync(Guid serviceId, Guid characteristicId, BleDevice? device = null, CancellationToken cancellationToken = default)
	{
		var services = await GetServicesAsync(device, cancellationToken).ConfigureAwait(false);
		return services
			.Where(service => service.Id == serviceId)
			.SelectMany(service => service.Characteristics)
			.FirstOrDefault(characteristic => characteristic.Id == characteristicId);
	}

	public async Task WriteAsync(BleCharacteristic characteristic, byte[] data, WriteOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(characteristic);
		ArgumentNullException.ThrowIfNull(data);
		options ??= new WriteOptions();

		var session = RequireConnectedSession(characteristic.DeviceId);
		var retries = options.RetryCount ?? _options.OperationRetryCount;
		var writeType = ResolveWriteType(characteristic, options.WriteType);

		await ExecuteWithRetryAsync(
			"write",
			retries,
			() => _transport.WriteNativeAsync(session.DeviceId, characteristic.ServiceId, characteristic.Id, data, writeType, cancellationToken),
			cancellationToken).ConfigureAwait(false);
	}

	public async Task<byte[]> ReadAsync(BleCharacteristic characteristic, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(characteristic);
		var session = RequireConnectedSession(characteristic.DeviceId);
		byte[]? value = null;

		await ExecuteWithRetryAsync(
			"read",
			_options.OperationRetryCount,
			async () =>
			{
				value = await _transport.ReadNativeAsync(session.DeviceId, characteristic.ServiceId, characteristic.Id, cancellationToken).ConfigureAwait(false);
			},
			cancellationToken).ConfigureAwait(false);

		return value ?? [];
	}

	public async Task SubscribeAsync(BleCharacteristic characteristic, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(characteristic);
		RequireConnectedSession(characteristic.DeviceId);
		await _transport.SubscribeNativeAsync(characteristic.DeviceId, characteristic.ServiceId, characteristic.Id, cancellationToken).ConfigureAwait(false);
		Log(BluetoothManagerLogLevel.Information, $"Subscribed to {characteristic.Id}.");
	}

	public async Task UnsubscribeAsync(BleCharacteristic characteristic, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(characteristic);
		await _transport.UnsubscribeNativeAsync(characteristic.DeviceId, characteristic.ServiceId, characteristic.Id, cancellationToken).ConfigureAwait(false);
	}

	public async Task<int> ReadRssiAsync(BleDevice? device = null, CancellationToken cancellationToken = default)
	{
		var session = RequireConnectedSession(device);
		var rssi = await _transport.ReadRssiNativeAsync(session.DeviceId, cancellationToken).ConfigureAwait(false);
		session.Device = session.Device.WithRssi(rssi);
		RssiUpdated?.Invoke(this, new RssiUpdatedEventArgs(session.Device, rssi));
		return rssi;
	}

	public void EnableLogging(bool enabled, IBluetoothManagerLogger? logger = null)
	{
		_loggingEnabled = enabled;
		if (logger is not null)
			_logger = logger;
	}

	public void Dispose()
	{
		_transport.AdapterStateChanged -= OnAdapterStateChanged;
		_transport.DeviceDiscovered -= OnNativeDeviceDiscovered;
		_transport.ConnectionChanged -= OnNativeConnectionChanged;
		_transport.CharacteristicChanged -= OnNativeCharacteristicChanged;
		_transport.RssiUpdated -= OnNativeRssiUpdated;
		_scanCts?.Cancel();
		_scanCts?.Dispose();

		lock (_gate)
		{
			foreach (var session in _sessions.Values)
				session.CancelReconnect();

			_sessions.Clear();
		}

		_transport.Dispose();
		_scanLock.Dispose();
	}

	async Task ConnectSessionAsync(ConnectionSession session, bool isReconnect, CancellationToken cancellationToken)
	{
		SetState(session, isReconnect ? BluetoothConnectionState.Reconnecting : BluetoothConnectionState.Connecting);

		var timeout = session.Options.Timeout ?? _options.ConnectionTimeout;
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(timeout);

		try
		{
			Log(BluetoothManagerLogLevel.Information, $"{(isReconnect ? "Reconnecting" : "Connecting")} to {session.Device.DisplayName}.");
			await _transport.ConnectNativeAsync(session.DeviceId, session.Options, timeoutCts.Token).ConfigureAwait(false);

			if (session.Options.DiscoverServices)
			{
				SetState(session, BluetoothConnectionState.Discovering);
				session.Services = await _transport.DiscoverServicesAsync(session.DeviceId, timeoutCts.Token).ConfigureAwait(false);
			}

			session.ReconnectAttempt = 0;
			SetState(session, BluetoothConnectionState.Connected);
			Log(BluetoothManagerLogLevel.Information, $"Connected to {session.Device.DisplayName}.");
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new BluetoothManagerException(
				BluetoothManagerError.ConnectionTimeout,
				$"Connecting to {session.Device.DisplayName} timed out after {timeout.TotalSeconds:0.#}s.");
		}
		catch (BluetoothManagerException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new BluetoothManagerException(
				BluetoothManagerError.ConnectionFailed,
				$"Connecting to {session.Device.DisplayName} failed.",
				ex);
		}
	}

	async Task EnsureReadyAsync(CancellationToken cancellationToken)
	{
		if (!_transport.IsSupported)
		{
			throw new BluetoothManagerException(
				BluetoothManagerError.FeatureNotSupported,
				"Bluetooth Low Energy is not available on this target.");
		}

		cancellationToken.ThrowIfCancellationRequested();

		var permission = await CheckPermissionsAsync().ConfigureAwait(false);
		if (permission != BluetoothPermissionStatus.Granted)
		{
			permission = await RequestPermissionsAsync().ConfigureAwait(false);
			if (permission != BluetoothPermissionStatus.Granted)
			{
				throw new BluetoothManagerException(
					BluetoothManagerError.PermissionDenied,
					$"Bluetooth permission is {permission}. Grant Bluetooth (and location on Android 11 and below) in Settings.");
			}
		}

		var adapter = _transport.AdapterState;
		if (adapter != BluetoothAdapterState.On)
		{
			throw new BluetoothManagerException(
				BluetoothManagerError.AdapterUnavailable,
				$"Bluetooth adapter is {adapter}. Turn Bluetooth on and try again.");
		}
	}

	async Task ExecuteWithRetryAsync(string operation, int retries, Func<Task> action, CancellationToken cancellationToken)
	{
		Exception? last = null;
		for (var attempt = 0; attempt <= retries; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				await action().ConfigureAwait(false);
				return;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				last = ex;
				Log(BluetoothManagerLogLevel.Warning, $"{operation} attempt {attempt + 1} failed.", ex);
				if (attempt == retries)
					break;

				await _clock.Delay(_options.OperationRetryDelay, cancellationToken).ConfigureAwait(false);
			}
		}

		throw new BluetoothManagerException(
			BluetoothManagerError.OperationFailed,
			$"GATT {operation} failed after {retries + 1} attempt(s).",
			last);
	}

	void OnNativeDeviceDiscovered(object? sender, DeviceDiscoveredEventArgs e)
	{
		BleDevice device;
		lock (_gate)
		{
			if (!_isScanning || _activeScanOptions is null)
				return;

			if (!DeviceFilter.Matches(e.Device, _activeScanOptions, _options.MinimumRssi))
				return;

			if (_discovered.TryGetValue(e.Device.Id, out var existing))
			{
				device = e.Device.WithName(existing.Name ?? e.Device.Name);
				if (!_activeScanOptions.AllowDuplicates && existing.Rssi == device.Rssi)
					return;
			}
			else
			{
				device = e.Device;
			}

			_discovered[device.Id] = device;
		}

		DeviceDiscovered?.Invoke(this, new DeviceDiscoveredEventArgs(device));
	}

	void OnNativeConnectionChanged(object? sender, NativeConnectionEventArgs e)
	{
		ConnectionSession? session;
		lock (_gate)
		{
			_sessions.TryGetValue(e.DeviceId, out session);
		}

		if (session is null)
			return;

		if (e.IsConnected)
			return;

		if (session.UserRequestedDisconnect)
			return;

		if (session.State is BluetoothConnectionState.Connecting or BluetoothConnectionState.Discovering or BluetoothConnectionState.Reconnecting)
			return;

		Log(BluetoothManagerLogLevel.Warning, $"Device {session.Device.DisplayName} disconnected unexpectedly: {e.ErrorMessage}");
		CompleteDisconnect(session, DisconnectReason.RemoteDisconnected, e.ErrorMessage, raiseOnly: false);
		ScheduleReconnect(session);
	}

	void OnNativeCharacteristicChanged(object? sender, CharacteristicChangedEventArgs e) =>
		CharacteristicChanged?.Invoke(this, e);

	void OnNativeRssiUpdated(object? sender, RssiUpdatedEventArgs e)
	{
		lock (_gate)
		{
			if (_sessions.TryGetValue(e.Device.Id, out var session))
				session.Device = session.Device.WithRssi(e.Rssi);
		}

		RssiUpdated?.Invoke(this, e);
	}

	void OnAdapterStateChanged(object? sender, AdapterStateChangedEventArgs e)
	{
		Log(BluetoothManagerLogLevel.Information, $"Adapter state {e.Previous} -> {e.Current}.");
		AdapterStateChanged?.Invoke(this, e);

		if (e.Current is BluetoothAdapterState.On)
			return;

		ConnectionSession[] sessions;
		lock (_gate)
			sessions = _sessions.Values.ToArray();

		foreach (var session in sessions)
		{
			if (session.State == BluetoothConnectionState.Disconnected)
				continue;

			session.UserRequestedDisconnect = true;
			session.CancelReconnect();
			CompleteDisconnect(session, DisconnectReason.AdapterUnavailable, $"Adapter is {e.Current}.");
		}
	}

	void ScheduleReconnect(ConnectionSession session)
	{
		if (session.UserRequestedDisconnect)
			return;

		if (!ReconnectPolicy.ShouldReconnect(session.Options, _options, session.ReconnectAttempt))
		{
			SetState(session, BluetoothConnectionState.Failed);
			return;
		}

		session.CancelReconnect();
		var cts = new CancellationTokenSource();
		session.ReconnectCts = cts;
		var attempt = session.ReconnectAttempt;
		var delay = ReconnectPolicy.DelayForAttempt(attempt, _options.ReconnectInitialDelay, _options.ReconnectMaxDelay);
		session.ReconnectAttempt = attempt + 1;

		_ = Task.Run(async () =>
		{
			try
			{
				Log(BluetoothManagerLogLevel.Information, $"Reconnect attempt {session.ReconnectAttempt} for {session.Device.DisplayName} in {delay.TotalSeconds:0.#}s.");
				await _clock.Delay(delay, cts.Token).ConfigureAwait(false);
				await ConnectSessionAsync(session, isReconnect: true, cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				Log(BluetoothManagerLogLevel.Warning, $"Reconnect attempt {session.ReconnectAttempt} failed.", ex);
				if (!cts.IsCancellationRequested)
					ScheduleReconnect(session);
			}
		}, cts.Token);
	}

	void CompleteDisconnect(ConnectionSession session, DisconnectReason reason, string? message, bool raiseOnly = false, bool alreadyDisconnected = false)
	{
		if (!alreadyDisconnected && session.State != BluetoothConnectionState.Disconnected)
			SetState(session, BluetoothConnectionState.Disconnected);

		if (!raiseOnly)
		{
			lock (_gate)
			{
				if (string.Equals(_primaryDeviceId, session.DeviceId, StringComparison.OrdinalIgnoreCase) &&
					session.State == BluetoothConnectionState.Disconnected)
				{
					_primaryDeviceId = _sessions.Values
						.FirstOrDefault(item => item.State == BluetoothConnectionState.Connected)?.DeviceId;
				}
			}
		}

		DeviceDisconnected?.Invoke(this, new DeviceDisconnectedEventArgs(session.Device, reason, message));
	}

	void SetState(ConnectionSession session, BluetoothConnectionState next)
	{
		BluetoothConnectionState previous;
		lock (_gate)
		{
			previous = session.State;
			if (previous == next)
				return;

			session.State = next;
		}

		Log(BluetoothManagerLogLevel.Debug, $"{session.Device.DisplayName}: {previous} -> {next}");
		ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(session.Device, previous, next));
	}

	ConnectionSession RequireConnectedSession(BleDevice? device) =>
		RequireConnectedSession(device?.Id);

	ConnectionSession RequireConnectedSession(string? deviceId)
	{
		lock (_gate)
		{
			var session = deviceId is null ? TryGetPrimarySessionLocked() : TryGetSessionLocked(deviceId);
			if (session is null || session.State != BluetoothConnectionState.Connected)
			{
				throw new BluetoothManagerException(
					BluetoothManagerError.NotConnected,
					deviceId is null
						? "No Bluetooth device is connected."
						: $"Device {deviceId} is not connected.");
			}

			return session;
		}
	}

	ConnectionSession? TryGetPrimarySessionLocked()
	{
		if (_primaryDeviceId is not null && _sessions.TryGetValue(_primaryDeviceId, out var primary))
			return primary;

		return _sessions.Values.FirstOrDefault();
	}

	ConnectionSession? TryGetSessionLocked(string deviceId) =>
		_sessions.TryGetValue(deviceId, out var session) ? session : null;

	async Task SafeStopScanAsync()
	{
		try
		{
			await _transport.StopScanAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Log(BluetoothManagerLogLevel.Debug, "StopScan ignored an error.", ex);
		}
	}

	static BleWriteType ResolveWriteType(BleCharacteristic characteristic, BleWriteType requested)
	{
		if (requested != BleWriteType.Default)
			return requested;

		return characteristic.Properties.HasFlag(BleCharacteristicProperties.Write)
			? BleWriteType.WithResponse
			: BleWriteType.WithoutResponse;
	}

	void Log(BluetoothManagerLogLevel level, string message, Exception? exception = null)
	{
		if (!_loggingEnabled)
			return;

		_logger.Log(level, message, exception);
	}
}
