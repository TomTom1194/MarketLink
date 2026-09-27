using System.Security.Claims;
using MarketLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "customer")]
    public class ReviewsController : Controller
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Farmer(int orderId, int rating, string? comment)
        {
            string error = await _reviewService.AddFarmerReviewAsync(GetCustomerId(), orderId, rating, comment);

            if (error != "")
            {
                TempData["Error"] = error;
            }
            else
            {
                TempData["Success"] = "Thanks! Your review has been posted.";
            }

            return BackToOrder(orderId);
        }

        private IActionResult BackToOrder(int orderId)
        {
            string url = Url.Action("Detail", "MyOrders", new { id = orderId }) + "#reviews";
            return Redirect(url);
        }

        private int GetCustomerId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
