namespace BlackRefit.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DeleteAttribute(string endpoint = "") : HttpMethodAttribute(endpoint);