namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Describes how BLE is implemented on the current target.
/// </summary>
public sealed class BluetoothManagerPlatformInfo
{
	public BluetoothManagerPlatformInfo(bool isSupported, string nativeApi, string deviceIdKind)
	{
		IsSupported = isSupported;
		NativeApi = nativeApi;
		DeviceIdKind = deviceIdKind;
	}

	/// <summary>
	/// Gets a value indicating whether native BLE APIs are available.
	/// </summary>
	public bool IsSupported { get; }

	/// <summary>
	/// Gets the native stack name, such as <c>Android BluetoothGatt</c> or <c>CoreBluetooth</c>.
	/// </summary>
	public string NativeApi { get; }

	/// <summary>
	/// Gets how <see cref="BleDevice.Id"/> is assigned on this platform.
	/// </summary>
	public string DeviceIdKind { get; }
}
