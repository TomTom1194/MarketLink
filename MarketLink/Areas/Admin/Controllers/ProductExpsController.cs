using MarketLink.Dtos.Admin;
using MarketLink.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class ProductExpsController : Controller
    {
        private readonly IAdminProductExpService _expService;

        public ProductExpsController(IAdminProductExpService expService)
        {
            _expService = expService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.ProductCounts = await _expService.CountProductsAsync();
            var periods = await _expService.GetAllAsync();
            return View(periods);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new ProductExpFormDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductExpFormDto model)
        {
            await AddServiceErrors(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await _expService.CreateAsync(model);
            TempData["Success"] = $"Display period \"{model.ExpName}\" was created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _expService.GetFormAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductExpFormDto model)
        {
            model.ExpId = id;
            await AddServiceErrors(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!await _expService.UpdateAsync(model))
            {
                return NotFound();
            }

            TempData["Success"] = $"Display period \"{model.ExpName}\" was updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (await _expService.DeleteAsync(id))
                TempData["Success"] = "The display period was deleted.";
            else
                TempData["Error"] = "This display period is used by products, so it cannot be deleted.";

            return RedirectToAction(nameof(Index));
        }

        private async Task AddServiceErrors(ProductExpFormDto model)
        {
            var errors = await _expService.ValidateAsync(model);
            foreach (var error in errors)
            {
                ModelState.AddModelError(error.Key, error.Value);
            }
        }
    }
}
