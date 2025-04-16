namespace BlackRefit.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class GetAttribute(string endpoint = "") : HttpMethodAttribute(endpoint);