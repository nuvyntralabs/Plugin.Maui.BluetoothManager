namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Classifies a <see cref="BluetoothManagerException"/>.
/// </summary>
public enum BluetoothManagerError
{
	/// <summary>
	/// The operation is not valid in the current connection or adapter state.
	/// </summary>
	InvalidOperation = 0,

	/// <summary>
	/// BLE is not available on this target.
	/// </summary>
	FeatureNotSupported = 1,

	/// <summary>
	/// Scan or connect permissions were denied or restricted.
	/// </summary>
	PermissionDenied = 2,

	/// <summary>
	/// The Bluetooth adapter is off, resetting, or unauthorized.
	/// </summary>
	AdapterUnavailable = 3,

	/// <summary>
	/// Scan did not complete because the adapter or OS rejected it.
	/// </summary>
	ScanFailed = 4,

	/// <summary>
	/// Connect did not finish before <see cref="ConnectOptions.Timeout"/>.
	/// </summary>
	ConnectionTimeout = 5,

	/// <summary>
	/// The GATT connect or service discovery failed.
	/// </summary>
	ConnectionFailed = 6,

	/// <summary>
	/// The device is not currently connected.
	/// </summary>
	NotConnected = 7,

	/// <summary>
	/// A characteristic read, write, or subscribe failed after retries.
	/// </summary>
	OperationFailed = 8,

	/// <summary>
	/// The requested service or characteristic was not found.
	/// </summary>
	CharacteristicNotFound = 9
}
