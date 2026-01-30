namespace DarkVelocity.Fiscalisation.Api.Services;

/// <summary>
/// Factory for creating TSE adapters based on device type.
/// </summary>
public interface ITseAdapterFactory
{
    ITseAdapter GetAdapter(string deviceType);
    IEnumerable<string> GetSupportedDeviceTypes();
}

public class TseAdapterFactory : ITseAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _adapterTypes;

    public TseAdapterFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _adapterTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "MockTSE", typeof(MockTseAdapter) },
            // Future adapters:
            // { "FiskalyCloud", typeof(FiskalyTseAdapter) },
            // { "SwissbitCloud", typeof(SwissbitTseAdapter) },
            // { "SwissbitUSB", typeof(SwissbitUsbTseAdapter) },
            // { "Epson", typeof(EpsonTseAdapter) },
        };
    }

    public ITseAdapter GetAdapter(string deviceType)
    {
        if (!_adapterTypes.TryGetValue(deviceType, out var adapterType))
        {
            throw new ArgumentException($"Unsupported TSE device type: {deviceType}. Supported types: {string.Join(", ", _adapterTypes.Keys)}");
        }

        return (ITseAdapter)ActivatorUtilities.CreateInstance(_serviceProvider, adapterType);
    }

    public IEnumerable<string> GetSupportedDeviceTypes()
    {
        return _adapterTypes.Keys;
    }
}
