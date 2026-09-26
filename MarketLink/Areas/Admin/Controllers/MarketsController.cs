using MarketLink.Dtos.Admin;
using MarketLink.Models;
using MarketLink.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MarketLink.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class MarketsController : Controller
    {
        private readonly IAdminMarketService _marketService;

        public MarketsController(IAdminMarketService marketService)
        {
            _marketService = marketService;
        }

        // GET: /Admin/Markets?search=...&cityId=1
        public async Task<IActionResult> Index(string? search, int? cityId)
        {
            ViewBag.Search = search;
            ViewBag.Cities = new SelectList(await _marketService.GetCitiesAsync(), "CityId", "CityName", cityId);

            var markets = await _marketService.GetMarketsAsync(search, cityId);
            return View(markets);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new MarketFormDto
            {
                OpenTime = new TimeSpan(5, 0, 0),
                CloseTime = new TimeSpan(11, 0, 0)
            };

            await LoadDropdowns(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MarketFormDto model)
        {
            await AddServiceErrors(model);

            if (!ModelState.IsValid)
            {
                await LoadDropdowns(model);
                return View(model);
            }

            await _marketService.CreateAsync(model);
            TempData["Success"] = $"Market \"{model.MarketName}\" was created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _marketService.GetFormAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            await LoadDropdowns(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MarketFormDto model)
        {
            model.MarketId = id;
            await AddServiceErrors(model);

            if (!ModelState.IsValid)
            {
                await LoadDropdowns(model);
                return View(model);
            }

            if (!await _marketService.UpdateAsync(model))
            {
                return NotFound();
            }

            TempData["Success"] = $"Market \"{model.MarketName}\" was updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: show / hide a market
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            if (!await _marketService.ToggleActiveAsync(id))
            {
                return NotFound();
            }

            TempData["Success"] = "Market visibility was updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Markets/Districts?cityId=1  (used by the District dropdown)
        [HttpGet]
        public async Task<IActionResult> Districts(int cityId)
        {
            var districts = await _marketService.GetDistrictsAsync(cityId);
            return Json(districts.Select(d => new { d.DistrictId, d.DistrictName }));
        }

        private async Task AddServiceErrors(MarketFormDto model)
        {
            var errors = await _marketService.ValidateAsync(model);
            foreach (var error in errors)
            {
                ModelState.AddModelError(error.Key, error.Value);
            }
        }

        private async Task LoadDropdowns(MarketFormDto model)
        {
            ViewBag.Cities = new SelectList(await _marketService.GetCitiesAsync(), "CityId", "CityName", model.CityId);

            var districts = model.CityId == null
                ? new List<District>()
                : await _marketService.GetDistrictsAsync(model.CityId.Value);
            ViewBag.Districts = new SelectList(districts, "DistrictId", "DistrictName", model.DistrictId);
        }
    }
}
