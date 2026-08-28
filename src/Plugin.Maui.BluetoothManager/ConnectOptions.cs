namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Per-connection overrides for timeout, reconnect, and service discovery.
/// </summary>
public sealed class ConnectOptions
{
	/// <summary>
	/// Gets or sets the connect timeout. When <c>null</c>, <see cref="BluetoothManagerOptions.ConnectionTimeout"/> is used.
	/// </summary>
	public TimeSpan? Timeout { get; set; }

	/// <summary>
	/// Gets or sets whether this session auto-reconnects. When <c>null</c>, <see cref="BluetoothManagerOptions.AutoReconnect"/> is used.
	/// </summary>
	public bool? AutoReconnect { get; set; }

	/// <summary>
	/// Gets or sets reconnect attempts for this session. When <c>null</c>, <see cref="BluetoothManagerOptions.MaxReconnectAttempts"/> is used.
	/// </summary>
	public int? MaxReconnectAttempts { get; set; }

	/// <summary>
	/// Gets or sets whether GATT services are discovered immediately after the link is up.
	/// Default is <c>true</c>.
	/// </summary>
	public bool DiscoverServices { get; set; } = true;
}
