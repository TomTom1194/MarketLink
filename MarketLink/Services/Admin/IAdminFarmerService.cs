using MarketLink.Dtos.Admin;

namespace MarketLink.Services.Admin
{
    public interface IAdminFarmerService
    {
        // status: pending | approved | rejected | suspended, or null for all
        Task<List<FarmerRowDto>> GetFarmersAsync(string? status, string? search);

        // e.g. { "pending": 3, "approved": 12 }
        Task<Dictionary<string, int>> CountByStatusAsync();

        // Full application of one farmer, with all stalls. null = not found.
        Task<FarmerApplicationDetailDto?> GetDetailAsync(int farmerId);

        // pending / rejected -> approved: creates a password and emails it with the login link.
        // suspended -> approved: the farmer can sell again with their old password.
        Task<FarmerApprovalResult> ApproveAsync(int farmerId, int adminId, string loginUrl);

        // pending -> rejected (also emails the farmer). false = not allowed.
        Task<bool> RejectAsync(int farmerId);

        // approved -> suspended. false = not allowed.
        Task<bool> SuspendAsync(int farmerId);
    }
}
