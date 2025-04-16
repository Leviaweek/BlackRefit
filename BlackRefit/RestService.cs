namespace BlackRefit;

public static class RestService
{
    private static readonly Dictionary<Type, Func<HttpClient, object>> Clients = new();
    
    public static void RegisterClient<TService>(Func<HttpClient, TService> clientFactory)
        where TService : class
    {
        var type = typeof(TService);
        if (!Clients.TryAdd(type, clientFactory))
        {
            throw new InvalidOperationException($"Client for {type.Name} is already registered");
        }
    }
    
    public static TService For<TService>(string baseUrl)
        where TService : class
    {
        var type = typeof(TService);

        ValidateData(baseUrl, type);

        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
        
        if (!Clients.TryGetValue(type, out var clientFactory))
        {
            throw new InvalidOperationException($"No client factory registered for {type.Name}");
        }
        
        return clientFactory(client) as TService ??
               throw new InvalidOperationException($"Failed to create client for {type.Name}");
    }

    private static void ValidateData(string baseUrl, Type type)
    {
        if (!type.IsInterface || !type.IsPublic)
        {
            throw new ArgumentException("TService must be an interface");
        }
        
        if (type.IsGenericType)
        {
            throw new ArgumentException("TService cannot be a generic type");
        }
        
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("baseUrl cannot be null or empty");
        }
    }
}