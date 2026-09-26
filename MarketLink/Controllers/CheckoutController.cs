using System.Security.Claims;
using MarketLink.Dtos;
using MarketLink.Models;
using MarketLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "customer")]
    public class CheckoutController : Controller
    {
        private readonly ICheckoutService _checkoutService;
        private readonly ICustomerOrderService _customerOrderService;

        public CheckoutController(ICheckoutService checkoutService, ICustomerOrderService customerOrderService)
        {
            _checkoutService = checkoutService;
            _customerOrderService = customerOrderService;
        }

        [HttpGet]
        public IActionResult Index(int? marketId)
        {
            return RedirectToAction("Index", "Cart", new { marketId = marketId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(CheckoutDto model)
        {
            if (!ModelState.IsValid)
            {
                var errorList = new List<string>();
                foreach (var field in ModelState.Values)
                {
                    foreach (var error in field.Errors)
                    {
                        errorList.Add(error.ErrorMessage);
                    }
                }
                TempData["Error"] = string.Join("\n", errorList);
                return RedirectToAction("Index", "Cart", new { marketId = model.MarketId });
            }

            var result = await _checkoutService.PlaceOrdersAsync(GetCustomerId(), model);

            if (!result.Success)
            {
                TempData["Error"] = string.Join("\n", result.Errors);
                return RedirectToAction("Index", "Cart", new { marketId = model.MarketId });
            }

            var orderIds = new List<string>();
            foreach (var order in result.Orders)
            {
                orderIds.Add(order.OrderId.ToString());
            }
            TempData["NewOrderIds"] = string.Join(",", orderIds);

            return RedirectToAction("Success");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReserveNow(QuickReserveDto model)
        {
            var result = await _checkoutService.ReserveNowAsync(GetCustomerId(), model);

            if (!result.Success)
            {
                TempData["Error"] = string.Join("\n", result.Errors);
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }
                return RedirectToAction("Detail", "Shop", new { id = model.StockPriceId });
            }

            TempData["NewOrderIds"] = result.Orders[0].OrderId.ToString();
            return RedirectToAction("Success");
        }

        [HttpGet]
        public async Task<IActionResult> Success()
        {
            string? orderIdText = TempData["NewOrderIds"] as string;
            if (string.IsNullOrEmpty(orderIdText))
            {
                return RedirectToAction("Index", "MyOrders");
            }

            int customerId = GetCustomerId();
            var orders = new List<Order>();

            foreach (string s in orderIdText.Split(','))
            {
                int orderId;
                if (int.TryParse(s, out orderId))
                {
                    var order = await _customerOrderService.GetOrderDetailAsync(customerId, orderId);
                    if (order != null)
                    {
                        orders.Add(order);
                    }
                }
            }

            return View(orders);
        }

        private int GetCustomerId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
