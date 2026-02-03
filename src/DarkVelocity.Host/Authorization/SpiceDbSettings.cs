namespace DarkVelocity.Host.Authorization;

public sealed class SpiceDbSettings
{
    public string Endpoint { get; set; } = "http://localhost:50051";
    public string PresharedKey { get; set; } = "darkvelocity_dev_key";
    public bool UseTls { get; set; } = false;
}
