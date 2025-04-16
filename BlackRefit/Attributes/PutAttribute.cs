namespace BlackRefit.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PutAttribute(string endpoint = "") : HttpMethodAttribute(endpoint);