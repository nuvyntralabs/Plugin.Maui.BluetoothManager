namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// A BLE peripheral discovered during scan or tracked as a connection session.
/// </summary>
public sealed class BleDevice
{
	public BleDevice(
		string id,
		string? name,
		int rssi,
		bool isConnectable,
		IReadOnlyList<Guid> advertisedServiceIds,
		IReadOnlyDictionary<int, byte[]>? manufacturerData = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		Id = id;
		Name = name;
		Rssi = rssi;
		IsConnectable = isConnectable;
		AdvertisedServiceIds = advertisedServiceIds ?? [];
		ManufacturerData = manufacturerData ?? new Dictionary<int, byte[]>();
	}

	/// <summary>
	/// Gets the platform identifier. Android uses the MAC address from the scan result; iOS uses <c>CBPeripheral.Identifier</c>.
	/// </summary>
	public string Id { get; }

	/// <summary>
	/// Gets the advertised or GAP name when the OS exposes it.
	/// </summary>
	public string? Name { get; }

	/// <summary>
	/// Gets the last observed RSSI in dBm. More negative is weaker.
	/// </summary>
	public int Rssi { get; }

	/// <summary>
	/// Gets a value indicating whether the advertisement marked the device as connectable.
	/// </summary>
	public bool IsConnectable { get; }

	/// <summary>
	/// Gets service UUIDs present in the advertisement. This list is often incomplete until connect.
	/// </summary>
	public IReadOnlyList<Guid> AdvertisedServiceIds { get; }

	/// <summary>
	/// Gets manufacturer-specific advertisement payloads keyed by company identifier.
	/// </summary>
	public IReadOnlyDictionary<int, byte[]> ManufacturerData { get; }

	/// <summary>
	/// Gets a display name that falls back to <see cref="Id"/> when the device is unnamed.
	/// </summary>
	public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

	internal BleDevice WithRssi(int rssi) =>
		new(Id, Name, rssi, IsConnectable, AdvertisedServiceIds, ManufacturerData);

	internal BleDevice WithName(string? name) =>
		new(Id, name ?? Name, Rssi, IsConnectable, AdvertisedServiceIds, ManufacturerData);

	public override string ToString() => $"{DisplayName} ({Id}) RSSI {Rssi}";
}
