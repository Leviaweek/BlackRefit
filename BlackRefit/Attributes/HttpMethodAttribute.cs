namespace BlackRefit.Attributes;

public abstract class HttpMethodAttribute(string endpoint) : Attribute
{
    public string Endpoint { get; } = endpoint;
}