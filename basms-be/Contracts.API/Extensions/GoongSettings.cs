namespace Contracts.API.Extensions;

public class GoongSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string GeocodingEndpoint { get; set; } = "https://rsapi.goong.io/geocode";
}
