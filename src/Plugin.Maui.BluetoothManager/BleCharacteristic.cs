namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// A GATT characteristic that can be read, written, or subscribed after connect.
/// </summary>
public sealed class BleCharacteristic
{
	public BleCharacteristic(
		string deviceId,
		Guid serviceId,
		Guid id,
		BleCharacteristicProperties properties)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

		DeviceId = deviceId;
		ServiceId = serviceId;
		Id = id;
		Properties = properties;
	}

	/// <summary>
	/// Gets the connected device that owns this characteristic.
	/// </summary>
	public string DeviceId { get; }

	/// <summary>
	/// Gets the parent service UUID.
	/// </summary>
	public Guid ServiceId { get; }

	/// <summary>
	/// Gets the characteristic UUID.
	/// </summary>
	public Guid Id { get; }

	/// <summary>
	/// Gets the GATT properties reported by the peripheral.
	/// </summary>
	public BleCharacteristicProperties Properties { get; }

	public bool CanRead => Properties.HasFlag(BleCharacteristicProperties.Read);

	public bool CanWrite =>
		Properties.HasFlag(BleCharacteristicProperties.Write) ||
		Properties.HasFlag(BleCharacteristicProperties.WriteWithoutResponse);

	public bool CanNotify =>
		Properties.HasFlag(BleCharacteristicProperties.Notify) ||
		Properties.HasFlag(BleCharacteristicProperties.Indicate);

	public override string ToString() => $"{Id} on {ServiceId}";
}
