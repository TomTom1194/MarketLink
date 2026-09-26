using MarketLink.Dtos;
using MarketLink.Models;

namespace MarketLink.Services
{
    public interface ICheckoutService
    {
        Task<CustomerProfile?> GetCustomerAsync(int customerId);

        List<DateTime> GetPickupDates(Stall stall, Market market);

        List<DateTime> GetCommonPickupDates(List<Stall> stalls, Market market);

        Task<CheckoutResultDto> ReserveNowAsync(int customerId, QuickReserveDto model);

        Task<CheckoutResultDto> PlaceOrdersAsync(int customerId, CheckoutDto model);
    }
}
