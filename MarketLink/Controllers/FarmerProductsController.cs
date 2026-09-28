using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using MarketLink.Models;
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
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            await LoadOptionsAsync(farmerId.Value);
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
                    TempData["Success"] = "Product added. Enter its price and quantity to start selling.";
                    return RedirectToAction(nameof(Edit), null, new { id = product.ProductId }, "stock");
                }
                catch (ValidationException exception) { ModelState.AddModelError(nameof(model.ProductName), exception.Message); }
                catch (InvalidOperationException exception) { ModelState.AddModelError("", exception.Message); }
            }
            await LoadOptionsAsync(farmerId.Value, model.CategoryId, model.ExpId, model.Unit);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            var product = await _products.GetProductAsync(farmerId.Value, id);
            if (product == null) return NotFound();
            var model = new UpdateFarmerProductDto
            {
                ProductId = id,
                CategoryId = product.CategoryId,
                ProductName = product.ProductName,
                Description = product.Description,
                Unit = product.Unit,
                Price = product.StockPrices.FirstOrDefault(sp => sp.EffectiveTo == null)?.Price
            };
            ViewBag.CurrentImageUrl = product.ImageUrl;
            await LoadOptionsAsync(farmerId.Value, product.CategoryId, null, product.Unit);
            await LoadPriceStockAsync(farmerId.Value, product);
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

                    string? stockError = await UpdatePriceAndStockAsync(farmerId.Value, product, model);
                    if (stockError != null)
                    {
                        TempData["Error"] = "Product details were saved, but the price & stock were not: " + stockError;
                        return RedirectToAction(nameof(Edit), null, new { id = model.ProductId }, "stock");
                    }

                    TempData["Success"] = "Product updated.";
                    return RedirectToAction(nameof(Index));
                }
                catch (ValidationException exception) { ModelState.AddModelError(nameof(model.ProductName), exception.Message); }
                catch (InvalidOperationException exception) { ModelState.AddModelError("", exception.Message); }
            }
            ViewBag.CurrentImageUrl = product.ImageUrl;
            await LoadOptionsAsync(farmerId.Value, model.CategoryId, null, model.Unit);
            await LoadPriceStockAsync(farmerId.Value, product);
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

        // Price & stock is now a section of the Edit page (old links still work)
        [HttpGet]
        public IActionResult Stock(int id)
        {
            return RedirectToAction(nameof(Edit), null, new { id }, "stock");
        }

        // Re-up page: only for a product that sold out or whose listing period is over
        [HttpGet]
        public async Task<IActionResult> Reup(int id)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            var product = await _products.GetProductAsync(farmerId.Value, id);
            if (product == null) return NotFound();

            var current = product.StockPrices.FirstOrDefault(sp => sp.EffectiveTo == null);
            bool expired = product.ExpiresAt <= DateTime.Now;
            bool soldOut = current != null && current.QuantityAvailable <= 0;
            if (!expired && !soldOut)
            {
                TempData["Error"] = "Re-up is only needed when the product sells out or its listing period is over. Update price & stock on this page instead.";
                return RedirectToAction(nameof(Edit), null, new { id }, "stock");
            }

            var history = await _products.GetStockHistoryAsync(farmerId.Value, id);
            ViewBag.LatestPrice = history.FirstOrDefault()?.Price;
            ViewBag.Reason = expired ? "expired" : "sold_out";
            return View(product);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reup(ReupFarmerProductDto model)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            if (!ModelState.IsValid)
            {
                TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid re-up details.";
                return RedirectToAction(nameof(Reup), new { id = model.ProductId });
            }
            try
            {
                await _products.ReupAsync(farmerId.Value, model);
                TempData["Success"] = "Product re-upped. It is on sale again.";
            }
            catch (InvalidOperationException exception)
            {
                TempData["Error"] = exception.Message;
                return RedirectToAction(nameof(Reup), new { id = model.ProductId });
            }
            return RedirectToAction(nameof(Edit), null, new { id = model.ProductId }, "stock");
        }

        private int? GetFarmerId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(id, out var farmerId) ? farmerId : null;
        }

        // Price & stock fields of the edit form. Returns an error message, or null when fine.
        //  - no price yet        -> post the first price and quantity
        //  - listing running     -> change price and / or add stock (listing period unchanged)
        //  - sold out or expired -> fields are locked; the farmer uses the Re-up page
        private async Task<string?> UpdatePriceAndStockAsync(int farmerId, Product product, UpdateFarmerProductDto model)
        {
            bool nothingEntered = model.Price == null && model.AddedQuantity == 0;
            var current = product.StockPrices.FirstOrDefault(sp => sp.EffectiveTo == null);
            bool hasHistory = (await _products.GetStockHistoryAsync(farmerId, product.ProductId)).Count > 0;

            try
            {
                if (!hasHistory)
                {
                    if (nothingEntered) return null;
                    if (model.Price == null || model.AddedQuantity <= 0) return "Enter both a price and a quantity to start selling.";
                    await _products.CreateInitialStockPriceAsync(farmerId, new CreateInitialStockPriceDto
                    {
                        ProductId = product.ProductId,
                        Price = model.Price.Value,
                        QuantityIn = model.AddedQuantity
                    });
                    return null;
                }

                if (current == null) return null;
                bool priceChanged = model.Price != null && model.Price.Value != current.Price;
                if (!priceChanged && model.AddedQuantity == 0) return null;

                await _products.ChangePriceAsync(farmerId, new ChangeFarmerProductPriceDto
                {
                    ProductId = product.ProductId,
                    NewPrice = model.Price ?? current.Price,
                    AddedQuantity = model.AddedQuantity
                });
                return null;
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
        }

        // Data for the "Price & stock" section of the Edit page
        private async Task LoadPriceStockAsync(int farmerId, Product product)
        {
            ViewBag.Product = product;
            ViewBag.StockHistory = await _products.GetStockHistoryAsync(farmerId, product.ProductId);
            ViewBag.Stall = (await _stalls.GetStallsAsync(farmerId)).FirstOrDefault();
            ViewBag.ExpiryOptions = await GetExpirySelectAsync(product.ExpId);
        }

        private async Task LoadOptionsAsync(int farmerId, int? categoryId = null, int? expId = null, string? unit = null)
        {
            // Only the categories this farmer registered for
            ViewBag.Categories = new SelectList(await _products.GetCategoriesAsync(farmerId, categoryId), "CategoryId", "CategoryName", categoryId);
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
    }
}
