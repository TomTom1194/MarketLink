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
        private readonly IProductNameChecker _nameChecker;
        private readonly IProductUnitService _units;

        public FarmerProductsController(IFarmerProductService products, IFarmerStallManagementService stalls,
            IProductNameChecker nameChecker, IProductUnitService units)
        {
            _products = products;
            _stalls = stalls;
            _nameChecker = nameChecker;
            _units = units;
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

            // 1) Picked a template: name, category and unit come from the template (always a match)
            if (model.TemplateId != null)
            {
                var template = await _products.GetTemplateAsync(farmerId.Value, model.TemplateId.Value);
                if (template == null)
                {
                    ModelState.AddModelError(nameof(model.TemplateId), "This product template is not available for your categories.");
                }
                else
                {
                    model.ProductName = template.ProductName;
                    model.CategoryId = template.CategoryId;
                    model.Unit = template.Unit;
                    ModelState.Remove(nameof(model.ProductName));
                    ModelState.Remove(nameof(model.CategoryId));
                    ModelState.Remove(nameof(model.Unit));
                }
            }

            // The unit must be one of the units allowed in the chosen category
            if (ModelState.IsValid && !await _units.IsAllowedAsync(model.CategoryId, model.Unit))
                ModelState.AddModelError(nameof(model.Unit), "This unit cannot be used for products in this category.");

            // 2) Typed by hand: ask AI whether the name and unit fit the category. If not, nothing is saved.
            string aiNote = "";
            if (ModelState.IsValid && model.TemplateId == null)
            {
                var check = await CheckNameWithAiAsync(farmerId.Value, model.ProductName, model.CategoryId, model.Unit);
                if (check.Matches == false)
                    ModelState.AddModelError(nameof(model.ProductName), check.Reason);
                else if (check.Matches == null)
                    aiNote = " (The AI name check was skipped: " + check.Reason + ")";
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var product = await _products.CreateProductAsync(farmerId.Value, model);
                    TempData["Success"] = "Product added. Enter its price and quantity to start selling." + aiNote;
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
            await LoadOptionsAsync(farmerId.Value, product.CategoryId, null, product.Unit, product);
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
            // The unit must fit the category. An old product may keep its unit while the category stays the same.
            bool keepsOldUnit = (model.Unit ?? "").Trim() == product.Unit && model.CategoryId == product.CategoryId;
            if (ModelState.IsValid && !keepsOldUnit && !await _units.IsAllowedAsync(model.CategoryId, model.Unit))
                ModelState.AddModelError(nameof(model.Unit), "This unit cannot be used for products in this category.");

            // Name, category or unit changed: ask AI again whether they fit. If not, nothing is saved.
            bool detailsChanged = model.ProductName.Trim() != product.ProductName
                || model.CategoryId != product.CategoryId
                || (model.Unit ?? "").Trim() != product.Unit;
            if (ModelState.IsValid && detailsChanged)
            {
                var check = await CheckNameWithAiAsync(farmerId.Value, model.ProductName, model.CategoryId, model.Unit);
                if (check.Matches == false)
                    ModelState.AddModelError(nameof(model.ProductName), check.Reason);
            }

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
            await LoadOptionsAsync(farmerId.Value, model.CategoryId, null, model.Unit, product);
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
            // Back to the product list
            return RedirectToAction(nameof(Index));
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

        // Ask Gemini whether a product name typed by hand (and its unit) fits the chosen category
        private async Task<ProductNameCheckResult> CheckNameWithAiAsync(int farmerId, string productName, int categoryId, string unit)
        {
            var category = (await _products.GetCategoriesAsync(farmerId, categoryId)).FirstOrDefault(c => c.CategoryId == categoryId);
            if (category == null)
            {
                return new ProductNameCheckResult { Matches = null, Reason = "unknown category" };
            }

            var result = await _nameChecker.CheckAsync(productName.Trim(), category.CategoryName, unit.Trim());
            if (result.Matches == false)
            {
                result.Reason = $"\"{productName.Trim()}\" sold per {unit.Trim()} does not fit {category.CategoryName}. {result.Reason} " +
                                "Please pick a product from the templates, or change the category or unit.";
            }
            return result;
        }

        // Data for the "Price & stock" section of the Edit page
        private async Task LoadPriceStockAsync(int farmerId, Product product)
        {
            ViewBag.Product = product;
            ViewBag.StockHistory = await _products.GetStockHistoryAsync(farmerId, product.ProductId);
            ViewBag.Stall = (await _stalls.GetStallsAsync(farmerId)).FirstOrDefault();
            ViewBag.ExpiryOptions = await GetExpirySelectAsync(product.ExpId);
        }

        // editing: the product being edited (its current unit stays selectable in its current category)
        private async Task LoadOptionsAsync(int farmerId, int? categoryId = null, int? expId = null, string? unit = null, Product? editing = null)
        {
            // Only the categories this farmer registered for
            var categories = await _products.GetCategoriesAsync(farmerId, editing?.CategoryId ?? categoryId);
            ViewBag.Categories = new SelectList(categories, "CategoryId", "CategoryName", categoryId);
            ViewBag.ExpiryOptions = await GetExpirySelectAsync(expId);
            ViewBag.Templates = await _products.GetTemplatesAsync(farmerId);

            // Units allowed in each category: { categoryId: [units] }. The page script swaps the unit list when the category changes.
            var unitsByCategory = await _units.GetUnitsByCategoryAsync(categories.Select(c => c.CategoryId));
            if (editing != null && unitsByCategory.TryGetValue(editing.CategoryId, out var oldCategoryUnits)
                && !oldCategoryUnits.Contains(editing.Unit))
            {
                oldCategoryUnits.Add(editing.Unit);
            }
            ViewBag.UnitsByCategory = unitsByCategory;

            // Units of the selected category (empty until a category is chosen)
            var units = categoryId != null && unitsByCategory.TryGetValue(categoryId.Value, out var list) ? list : new List<string>();
            ViewBag.Units = new SelectList(units, unit);
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
