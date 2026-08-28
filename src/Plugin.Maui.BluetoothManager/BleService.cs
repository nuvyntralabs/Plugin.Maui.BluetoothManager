namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// A GATT service discovered after connect.
/// </summary>
public sealed class BleService
{
	public BleService(string deviceId, Guid id, bool isPrimary, IReadOnlyList<BleCharacteristic> characteristics)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

		DeviceId = deviceId;
		Id = id;
		IsPrimary = isPrimary;
		Characteristics = characteristics ?? [];
	}

	/// <summary>
	/// Gets the connected device that owns this service.
	/// </summary>
	public string DeviceId { get; }

	/// <summary>
	/// Gets the service UUID.
	/// </summary>
	public Guid Id { get; }

	/// <summary>
	/// Gets a value indicating whether this is a primary service.
	/// </summary>
	public bool IsPrimary { get; }

	/// <summary>
	/// Gets characteristics discovered on this service.
	/// </summary>
	public IReadOnlyList<BleCharacteristic> Characteristics { get; }

	public override string ToString() => Id.ToString();
}
