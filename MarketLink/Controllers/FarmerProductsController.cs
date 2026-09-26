using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using MarketLink.Dtos;
using MarketLink.Services;
using MarketLink.Services.Farmer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "farmer")]
    public class FarmerProductsController : Controller
    {
        private readonly IFarmerProductService _products;
        private readonly IFarmerStallManagementService _stalls;

        public FarmerProductsController(IFarmerProductService products, IFarmerStallManagementService stalls)
        {
            _products = products;
            _stalls = stalls;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            return View(await _products.GetProductsAsync(farmerId.Value));
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (GetFarmerId() == null) return Challenge();
            await LoadOptionsAsync();
            return View(new CreateFarmerProductDto());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateFarmerProductDto model)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            if (ModelState.IsValid && !(await _products.GetUnitsAsync()).Contains(model.Unit.Trim()))
                ModelState.AddModelError(nameof(model.Unit), "The selected unit does not exist or is inactive.");
            if (ModelState.IsValid)
            {
                try
                {
                    var product = await _products.CreateProductAsync(farmerId.Value, model);
                    TempData["Success"] = "Product added. Set its initial price and quantity.";
                    return RedirectToAction(nameof(Stock), new { id = product.ProductId });
                }
                catch (ValidationException exception) { ModelState.AddModelError(nameof(model.ProductName), exception.Message); }
                catch (InvalidOperationException exception) { ModelState.AddModelError("", exception.Message); }
            }
            await LoadOptionsAsync(model.CategoryId, model.ExpId, model.Unit);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            var product = await _products.GetProductAsync(farmerId.Value, id);
            if (product == null) return NotFound();
            var model = new UpdateFarmerProductDto { ProductId = id, CategoryId = product.CategoryId, ProductName = product.ProductName, Description = product.Description, Unit = product.Unit };
            ViewBag.CurrentImageUrl = product.ImageUrl;
            await LoadOptionsAsync(product.CategoryId, null, product.Unit);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdateFarmerProductDto model)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            var product = await _products.GetProductAsync(farmerId.Value, model.ProductId);
            if (product == null) return NotFound();
            if (ModelState.IsValid && !(await _products.GetUnitsAsync()).Contains(model.Unit.Trim()))
                ModelState.AddModelError(nameof(model.Unit), "The selected unit does not exist or is inactive.");
            if (ModelState.IsValid)
            {
                try
                {
                    if (!await _products.UpdateProductAsync(farmerId.Value, model)) return NotFound();
                    TempData["Success"] = "Product updated.";
                    return RedirectToAction(nameof(Index));
                }
                catch (ValidationException exception) { ModelState.AddModelError(nameof(model.ProductName), exception.Message); }
                catch (InvalidOperationException exception) { ModelState.AddModelError("", exception.Message); }
            }
            ViewBag.CurrentImageUrl = product.ImageUrl;
            await LoadOptionsAsync(model.CategoryId, null, model.Unit);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(UpdateFarmerProductStatusDto model)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            if (!ModelState.IsValid) return RedirectWithError(nameof(Index), "Invalid product status.");
            try
            {
                if (!await _products.UpdateProductStatusAsync(farmerId.Value, model)) return NotFound();
                TempData["Success"] = model.Status == "hidden" ? "Product hidden." : "Product is visible again.";
            }
            catch (InvalidOperationException exception) { TempData["Error"] = exception.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Stock(int id)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            var product = await _products.GetProductAsync(farmerId.Value, id);
            if (product == null) return NotFound();
            ViewBag.StockHistory = await _products.GetStockHistoryAsync(farmerId.Value, id);
            ViewBag.Stall = (await _stalls.GetStallsAsync(farmerId.Value)).FirstOrDefault();
            ViewBag.ExpiryOptions = await GetExpirySelectAsync(product.ExpId);
            return View(product);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PostStock(CreateInitialStockPriceDto model)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            if (!ModelState.IsValid) return RedirectToStockError(model.ProductId, ModelState[nameof(model.Price)]?.Errors.FirstOrDefault()?.ErrorMessage ?? "Invalid price or quantity.");
            try
            {
                await _products.CreateInitialStockPriceAsync(farmerId.Value, model);
                TempData["Success"] = "Initial price and quantity posted.";
            }
            catch (InvalidOperationException exception) { TempData["Error"] = exception.Message; }
            return RedirectToAction(nameof(Stock), new { id = model.ProductId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePrice(ChangeFarmerProductPriceDto model)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            if (!ModelState.IsValid) return RedirectToStockError(model.ProductId, ModelState[nameof(model.NewPrice)]?.Errors.FirstOrDefault()?.ErrorMessage ?? "Invalid new price.");
            try
            {
                await _products.ChangePriceAsync(farmerId.Value, model);
                TempData["Success"] = "Price updated.";
            }
            catch (InvalidOperationException exception) { TempData["Error"] = exception.Message; }
            return RedirectToAction(nameof(Stock), new { id = model.ProductId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reup(ReupFarmerProductDto model)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            if (!ModelState.IsValid) return RedirectToStockError(model.ProductId, "Invalid re-up expiry or quantity.");
            try
            {
                await _products.ReupAsync(farmerId.Value, model);
                TempData["Success"] = "Product re-upped.";
            }
            catch (InvalidOperationException exception) { TempData["Error"] = exception.Message; }
            return RedirectToAction(nameof(Stock), new { id = model.ProductId });
        }

        private int? GetFarmerId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(id, out var farmerId) ? farmerId : null;
        }

        private async Task LoadOptionsAsync(int? categoryId = null, int? expId = null, string? unit = null)
        {
            ViewBag.Categories = new SelectList(await _products.GetCategoriesAsync(), "CategoryId", "CategoryName", categoryId);
            ViewBag.ExpiryOptions = await GetExpirySelectAsync(expId);
            ViewBag.Units = new SelectList(await _products.GetUnitsAsync(), unit);
        }

        private async Task<SelectList> GetExpirySelectAsync(int? selectedId)
        {
            var options = await _products.GetExpiryOptionsAsync();
            var items = options.Select(option => new
            {
                option.ExpId,
                Label = option.ExpCode.Equals("SHORT", StringComparison.OrdinalIgnoreCase)
                    ? "SHORT - 24 hours"
                    : $"LONG - {option.DurationDays} days"
            });
            return new SelectList(items, "ExpId", "Label", selectedId);
        }

        private IActionResult RedirectWithError(string action, string message)
        {
            TempData["Error"] = message;
            return RedirectToAction(action);
        }

        private IActionResult RedirectToStockError(int productId, string message)
        {
            TempData["Error"] = message;
            return productId > 0 ? RedirectToAction(nameof(Stock), new { id = productId }) : RedirectToAction(nameof(Index));
        }
    }
}
