namespace BlackRefit.Attributes;

[AttributeUsage(AttributeTargets.Interface)]
public sealed class RestClientAttribute(string baseUrl = "") : Attribute
{
    public string BaseUrl { get; } = baseUrl;
}