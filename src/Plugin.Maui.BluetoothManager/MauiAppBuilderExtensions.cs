using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Registers the BluetoothManager plugin with the MAUI dependency injection container.
/// </summary>
public static class MauiAppBuilderExtensions
{
	/// <summary>
	/// Adds <see cref="IBluetoothManager"/> as a singleton.
	/// </summary>
	/// <example>
	/// <code>
	/// builder.UseBluetoothManager(options =>
	/// {
	///     options.EnableLogging = true;
	///     options.AutoReconnect = true;
	///     options.ConnectionTimeout = TimeSpan.FromSeconds(15);
	/// });
	/// </code>
	/// </example>
	public static MauiAppBuilder UseBluetoothManager(this MauiAppBuilder builder, Action<BluetoothManagerOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var options = new BluetoothManagerOptions();
		configure?.Invoke(options);

		builder.Services.AddSingleton(options);
		builder.Services.AddSingleton<IBluetoothManager>(services =>
		{
			options.Logger ??= CreateLoggerAdapter(services);
			var manager = BluetoothManager.Create(options);
			BluetoothManager.SetDefault(manager);
			return manager;
		});
		builder.Services.AddTransient<IMauiInitializeService, BluetoothManagerInitializer>();

		return builder;
	}

	internal static IBluetoothManagerLogger? CreateLoggerAdapter(IServiceProvider serviceProvider)
	{
		var factory = serviceProvider.GetService<ILoggerFactory>();
		return factory is null ? null : new MicrosoftLoggerAdapter(factory.CreateLogger("Plugin.Maui.BluetoothManager"));
	}
}
