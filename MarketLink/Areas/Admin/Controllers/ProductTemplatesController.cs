using MarketLink.Dtos.Admin;
using MarketLink.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MarketLink.Areas.Admin.Controllers
{
    // Ready-made products farmers can pick when they add a product
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class ProductTemplatesController : Controller
    {
        private readonly IAdminProductTemplateService _templateService;

        public ProductTemplatesController(IAdminProductTemplateService templateService)
        {
            _templateService = templateService;
        }

        // GET: /Admin/ProductTemplates?categoryId=3
        public async Task<IActionResult> Index(int? categoryId)
        {
            await LoadPageAsync(categoryId, new ProductTemplateFormDto { CategoryId = categoryId ?? 0 });
            return View(new ProductTemplateFormDto { CategoryId = categoryId ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductTemplateFormDto model)
        {
            // First the simple checks on the form ([Required]...), then the checks against the database
            if (ModelState.IsValid)
            {
                foreach (var error in await _templateService.ValidateAsync(model))
                {
                    ModelState.AddModelError(error.Key, error.Value);
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadPageAsync(null, model);
                return View("Index", model);
            }

            await _templateService.CreateAsync(model);
            TempData["Success"] = $"Template \"{model.ProductName.Trim()}\" was added.";
            return RedirectToAction(nameof(Index), new { categoryId = model.CategoryId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id, int? categoryId)
        {
            if (!await _templateService.ToggleAsync(id)) return NotFound();
            return RedirectToAction(nameof(Index), new { categoryId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int? categoryId)
        {
            if (!await _templateService.DeleteAsync(id)) return NotFound();
            TempData["Success"] = "The template was deleted.";
            return RedirectToAction(nameof(Index), new { categoryId });
        }

        private async Task LoadPageAsync(int? categoryId, ProductTemplateFormDto model)
        {
            var categories = await _templateService.GetCategoriesAsync();
            ViewBag.FilterCategoryId = categoryId;
            ViewBag.FilterCategories = new SelectList(categories, "CategoryId", "CategoryName", categoryId);
            ViewBag.Categories = new SelectList(categories, "CategoryId", "CategoryName", model.CategoryId);
            // Units follow the chosen category (the page script swaps them when the category changes)
            var unitsByCategory = await _templateService.GetUnitsByCategoryAsync(categories.Select(c => c.CategoryId));
            ViewBag.UnitsByCategory = unitsByCategory;
            var units = unitsByCategory.TryGetValue(model.CategoryId, out var list) ? list : new List<string>();
            ViewBag.Units = new SelectList(units, model.Unit);
            ViewBag.Templates = await _templateService.GetTemplatesAsync(categoryId);
        }
    }
}
