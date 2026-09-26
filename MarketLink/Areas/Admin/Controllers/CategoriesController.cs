using MarketLink.Dtos.Admin;
using MarketLink.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MarketLink.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class CategoriesController : Controller
    {
        private readonly IAdminCategoryService _categoryService;

        public CategoriesController(IAdminCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _categoryService.GetCategoriesAsync();
            return View(categories);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CategoryFormDto();
            await LoadParents(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryFormDto model)
        {
            await AddServiceErrors(model);

            if (!ModelState.IsValid)
            {
                await LoadParents(model);
                return View(model);
            }

            await _categoryService.CreateAsync(model);
            TempData["Success"] = $"Category \"{model.CategoryName}\" was created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _categoryService.GetFormAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            await LoadParents(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryFormDto model)
        {
            model.CategoryId = id;
            await AddServiceErrors(model);

            if (!ModelState.IsValid)
            {
                await LoadParents(model);
                return View(model);
            }

            if (!await _categoryService.UpdateAsync(model))
            {
                return NotFound();
            }

            TempData["Success"] = $"Category \"{model.CategoryName}\" was updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: show / hide a category
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            if (!await _categoryService.ToggleActiveAsync(id))
            {
                return NotFound();
            }

            TempData["Success"] = "Category visibility was updated.";
            return RedirectToAction(nameof(Index));
        }

        private async Task AddServiceErrors(CategoryFormDto model)
        {
            var errors = await _categoryService.ValidateAsync(model);
            foreach (var error in errors)
            {
                ModelState.AddModelError(error.Key, error.Value);
            }
        }

        private async Task LoadParents(CategoryFormDto model)
        {
            var parents = await _categoryService.GetParentOptionsAsync(model.CategoryId);
            ViewBag.Parents = new SelectList(parents, "CategoryId", "CategoryName", model.ParentId);
        }
    }
}
