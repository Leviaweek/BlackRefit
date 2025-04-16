namespace BlackRefit.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostAttribute(string endpoint = "") : HttpMethodAttribute(endpoint);