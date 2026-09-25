using MarketLink.Dtos;
using MarketLink.Models;

namespace MarketLink.Services
{
    public interface ICustomerAccountService
    {
        Task<Dictionary<string, string>> ValidateRegisterAsync(RegisterDto model);

        Task<User> RegisterAsync(RegisterDto model);

        Task<List<City>> GetCitiesAsync();

        Task<List<District>> GetDistrictsAsync(int cityId);
    }
}
