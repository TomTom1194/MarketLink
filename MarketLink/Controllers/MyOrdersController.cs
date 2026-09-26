using System.Security.Claims;
using MarketLink.Dtos;
using MarketLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "customer")]
    public class MyOrdersController : Controller
    {
        private readonly ICustomerOrderService _customerOrderService;
        private readonly IFavoriteService _favoriteService;
        private readonly ICartService _cartService;

        public MyOrdersController(ICustomerOrderService customerOrderService, IFavoriteService favoriteService, ICartService cartService)
        {
            _customerOrderService = customerOrderService;
            _favoriteService = favoriteService;
            _cartService = cartService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status)
        {
            int customerId = GetCustomerId();
            var schedule = await _customerOrderService.GetPickupScheduleAsync(customerId);
            int scheduleCount = CountScheduleOrders(schedule);

            if (status == null && scheduleCount > 0)
            {
                return RedirectToAction("Schedule");
            }

            if (status == "all")
            {
                status = null;
            }

            var orders = await _customerOrderService.GetOrdersAsync(customerId, status);
            ViewBag.Status = status ?? "all";
            ViewBag.ScheduleCount = scheduleCount;
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Schedule(string? day)
        {
            int customerId = GetCustomerId();
            var schedule = await _customerOrderService.GetPickupScheduleAsync(customerId);

            DateTime? selectedDay = null;
            DateTime parsedDay;
            if (day != null && DateTime.TryParseExact(day, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsedDay)
                && schedule.Any(d => d.Date == parsedDay.Date))
            {
                selectedDay = parsedDay.Date;
            }
            DateTime tomorrow = DateTime.Today.AddDays(1);
            if (selectedDay == null && schedule.Any(d => d.Date == tomorrow))
            {
                selectedDay = tomorrow;
            }
            if (selectedDay == null && schedule.Count > 0)
            {
                selectedDay = schedule[0].Date;
            }
            ViewBag.SelectedDay = selectedDay;

            ViewBag.Status = "schedule";
            ViewBag.ScheduleCount = CountScheduleOrders(schedule);
            ViewBag.WaitingCount = await _customerOrderService.CountWaitingOrdersAsync(customerId);
            return View(schedule);
        }

        private int CountScheduleOrders(List<PickupDayDto> schedule)
        {
            int count = 0;
            foreach (var day in schedule)
            {
                foreach (var market in day.Markets)
                {
                    foreach (var slot in market.Slots)
                    {
                        count = count + slot.Orders.Count;
                    }
                }
            }
            return count;
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var order = await _customerOrderService.GetOrderDetailAsync(GetCustomerId(), id);
            if (order == null)
            {
                return NotFound();
            }

            ViewBag.IsFavorite = await _favoriteService.IsFavoriteAsync(GetCustomerId(), order.Stall!.FarmerId);

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            string error = await _customerOrderService.CancelOrderAsync(GetCustomerId(), id, reason ?? "");

            if (error != "")
            {
                TempData["Error"] = error;
            }
            else
            {
                TempData["Success"] = "Your order has been cancelled.";
            }

            return RedirectToAction("Detail", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reorder(int id)
        {
            var result = await _cartService.ReorderAsync(GetCustomerId(), id);

            if (result.Error != "")
            {
                TempData["Error"] = result.Error;
                if (result.OrderCode == "")
                {
                    return RedirectToAction("Index");
                }
                return RedirectToAction("Detail", new { id = id });
            }

            if (result.AddedCount == 0)
            {
                TempData["Error"] = "None of the products from this order can be bought right now:\n" + string.Join("\n", result.Notices);
                return RedirectToAction("Detail", new { id = id });
            }

            TempData["Success"] = "Added " + result.AddedCount + " product(s) from order " + result.OrderCode + " to your basket.";
            if (result.Notices.Count > 0)
            {
                TempData["ReorderNotices"] = string.Join("\n", result.Notices);
            }
            return RedirectToAction("Index", "Cart", new { marketId = result.MarketId });
        }

        private int GetCustomerId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
