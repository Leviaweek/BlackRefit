using BlackRefit;
using BlackRefit.Attributes;
using BlackRefitTests;

var client = RestService.For<ITestService>("https://localhost:5000");

var values = await client.GetValuesAsync();
Console.WriteLine($"Values: {values}");

namespace BlackRefitTests
{
    [RestClient]
    public interface ITestService
    {
        [Get("/api/values")]
        Task<string> GetValuesAsync();
    
        [Get("/api/values/{id}")]
        Task<string> GetValueByIdAsync(int id);
    
        [Post("/api/values")]
        Task<string> CreateValueAsync([Body] string value);
    
        [Put("/api/values/{id}")]
        Task<string> UpdateValueAsync(int id, [Body] string value);
    
        [Delete("/api/values/{id}")]
        Task DeleteValueAsync(int id);
    }
}