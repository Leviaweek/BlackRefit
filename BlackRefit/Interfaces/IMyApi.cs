namespace BlackRefit.Interfaces;

public interface IMyApi
{
    Task<string> GetDataAsync(string id);
}