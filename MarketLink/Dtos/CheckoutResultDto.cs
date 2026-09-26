using MarketLink.Models;

namespace MarketLink.Dtos
{
    public class CheckoutResultDto
    {
        public List<string> Errors { get; set; } = new List<string>();

        public List<Order> Orders { get; set; } = new List<Order>();

        public bool Success
        {
            get { return Errors.Count == 0; }
        }
    }
}
