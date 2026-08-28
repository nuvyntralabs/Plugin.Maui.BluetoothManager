namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Why a managed BLE session left the connected state.
/// </summary>
public enum DisconnectReason
{
	/// <summary>
	/// The app called <see cref="IBluetoothManager.DisconnectAsync"/>.
	/// </summary>
	UserRequested = 0,

	/// <summary>
	/// The peripheral or OS dropped the link.
	/// </summary>
	RemoteDisconnected = 1,

	/// <summary>
	/// Connect or reconnect exceeded the configured timeout.
	/// </summary>
	Timeout = 2,

	/// <summary>
	/// The adapter turned off or became unauthorized.
	/// </summary>
	AdapterUnavailable = 3,

	/// <summary>
	/// Auto-reconnect exhausted <see cref="ConnectOptions.MaxReconnectAttempts"/>.
	/// </summary>
	ReconnectExhausted = 4,

	/// <summary>
	/// An unexpected platform error ended the session.
	/// </summary>
	Failed = 5
}
