namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Shared configuration applied when the plugin is registered with <c>UseBluetoothManager</c>.
/// </summary>
public sealed class BluetoothManagerOptions
{
	public static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromSeconds(15);
	public static readonly TimeSpan DefaultReconnectInitialDelay = TimeSpan.FromSeconds(1);
	public static readonly TimeSpan DefaultReconnectMaxDelay = TimeSpan.FromSeconds(30);
	public static readonly TimeSpan DefaultOperationRetryDelay = TimeSpan.FromMilliseconds(400);

	TimeSpan _connectionTimeout = DefaultConnectionTimeout;
	TimeSpan _scanDuration = TimeSpan.FromSeconds(10);
	TimeSpan _reconnectInitialDelay = DefaultReconnectInitialDelay;
	TimeSpan _reconnectMaxDelay = DefaultReconnectMaxDelay;
	TimeSpan _operationRetryDelay = DefaultOperationRetryDelay;
	int _maxReconnectAttempts = 5;
	int _operationRetryCount = 2;

	/// <summary>
	/// Gets or sets a value indicating whether plugin logging starts enabled.
	/// </summary>
	public bool EnableLogging { get; set; }

	/// <summary>
	/// Gets or sets a custom logger. When <c>null</c>, the plugin uses Microsoft.Extensions.Logging if available, otherwise a debug logger.
	/// </summary>
	public IBluetoothManagerLogger? Logger { get; set; }

	/// <summary>
	/// Gets or sets whether an unexpected disconnect starts automatic reconnect.
	/// Default is <c>true</c>.
	/// </summary>
	public bool AutoReconnect { get; set; } = true;

	/// <summary>
	/// Gets or sets how long a connect may take before it fails.
	/// Must be greater than zero. Default is 15 seconds.
	/// </summary>
	public TimeSpan ConnectionTimeout
	{
		get => _connectionTimeout;
		set => _connectionTimeout = EnsurePositive(value, nameof(ConnectionTimeout));
	}

	/// <summary>
	/// Gets or sets the default scan window used when <see cref="ScanOptions.Duration"/> is not set.
	/// Must be greater than zero. Default is 10 seconds.
	/// </summary>
	public TimeSpan DefaultScanDuration
	{
		get => _scanDuration;
		set => _scanDuration = EnsurePositive(value, nameof(DefaultScanDuration));
	}

	/// <summary>
	/// Gets or sets how many reconnect attempts run after an unexpected drop.
	/// Use <c>0</c> to disable. Default is 5.
	/// </summary>
	public int MaxReconnectAttempts
	{
		get => _maxReconnectAttempts;
		set
		{
			if (value < 0)
				throw new ArgumentOutOfRangeException(nameof(value), "MaxReconnectAttempts cannot be negative.");

			_maxReconnectAttempts = value;
		}
	}

	/// <summary>
	/// Gets or sets the first reconnect delay. Subsequent delays double until <see cref="ReconnectMaxDelay"/>.
	/// </summary>
	public TimeSpan ReconnectInitialDelay
	{
		get => _reconnectInitialDelay;
		set => _reconnectInitialDelay = EnsurePositive(value, nameof(ReconnectInitialDelay));
	}

	/// <summary>
	/// Gets or sets the maximum reconnect backoff.
	/// </summary>
	public TimeSpan ReconnectMaxDelay
	{
		get => _reconnectMaxDelay;
		set => _reconnectMaxDelay = EnsurePositive(value, nameof(ReconnectMaxDelay));
	}

	/// <summary>
	/// Gets or sets how many extra times a read or write is retried after a transient GATT failure.
	/// Default is 2.
	/// </summary>
	public int OperationRetryCount
	{
		get => _operationRetryCount;
		set
		{
			if (value < 0)
				throw new ArgumentOutOfRangeException(nameof(value), "OperationRetryCount cannot be negative.");

			_operationRetryCount = value;
		}
	}

	/// <summary>
	/// Gets or sets the delay between read / write retries.
	/// </summary>
	public TimeSpan OperationRetryDelay
	{
		get => _operationRetryDelay;
		set => _operationRetryDelay = EnsurePositive(value, nameof(OperationRetryDelay));
	}

	/// <summary>
	/// Gets or sets a global minimum RSSI filter applied when a scan does not specify its own.
	/// </summary>
	public int? MinimumRssi { get; set; }

	/// <summary>
	/// Gets or sets whether <c>UseBluetoothManager</c> requests Bluetooth permissions during startup.
	/// Default is <c>false</c> so the host can show rationale first.
	/// </summary>
	public bool RequestPermissionsOnInitialize { get; set; }

	static TimeSpan EnsurePositive(TimeSpan value, string name)
	{
		if (value <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(name, $"{name} must be greater than zero.");

		return value;
	}
}
