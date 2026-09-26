using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class FarmerProfileService : IFarmerProfileService
    {
        private readonly MarketLinkDbContext _ctx;
        public FarmerProfileService(MarketLinkDbContext ctx)
        {
            _ctx = ctx;
        }
        // Lấy hồ sơ kèm phường/xã và tỉnh/thành để hiển thị lên form.
        public async Task<FarmerProfile?> GetFarmerProfileAsync(int farmerId)
        {
            return await _ctx.FarmerProfiles.Include(profile => profile.District).ThenInclude(district => district!.City).FirstOrDefaultAsync(profile => profile.FarmerId == farmerId);
        }

        // Kiểm tra hồ sơ và quan hệ tỉnh/thành - phường/xã.
        public async Task<Dictionary<string, string>> ValidateUpdateAsync(int farmerId,UpdateFarmerProfileDto model)
        {
            var errors = new Dictionary<string, string>();

            var profileExists = await _ctx.FarmerProfiles.AnyAsync(p => p.FarmerId == farmerId);

            if (!profileExists)
            {
                errors[""] = "Farmer profile not found.";
                return errors;
            }

            if (!model.CityId.HasValue ||!await _ctx.Cities.AnyAsync(c => c.CityId == model.CityId.Value))
            {
                errors["CityId"] = "The selected province or city does not exist.";
            }

            if (!model.DistrictId.HasValue)
            {
                errors["DistrictId"] = "Select a ward or commune.";
            }
            else if (model.CityId.HasValue && !await _ctx.Districts.AnyAsync(d =>d.DistrictId == model.DistrictId.Value && d.CityId == model.CityId.Value))
            {
                errors["DistrictId"] = "The ward or commune does not belong to the selected province or city.";
            }

            return errors;
        }

        // Chỉ cập nhật thông tin hồ sơ farmer; không sửa trạng thái duyệt.
        public async Task UpdateProfileAsync(int farmerId,UpdateFarmerProfileDto model)
        {
            var profile = await _ctx.FarmerProfiles.FirstOrDefaultAsync(i => i.FarmerId == farmerId);

            if (profile == null)
            {
                throw new InvalidOperationException("Farmer profile not found.");
            }

            if (!model.DistrictId.HasValue)
            {
                throw new InvalidOperationException("Select a ward or commune.");
            }

            profile.BrandName = model.BrandName.Trim();
            profile.ContactPerson = model.ContactPerson.Trim();
            profile.Address = model.Address.Trim();
            profile.DistrictId = model.DistrictId.Value;
            profile.Description = model.Description?.Trim();

            await _ctx.SaveChangesAsync();
        }
    }
}
