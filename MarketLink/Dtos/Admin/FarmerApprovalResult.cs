namespace MarketLink.Dtos.Admin
{
    // What happened when the admin approved a farmer
    public class FarmerApprovalResult
    {
        // false = farmer not found or already approved
        public bool Success { get; set; }

        // true = a suspended farmer was allowed to sell again (old password kept, no email)
        public bool Reactivated { get; set; }

        public string Email { get; set; } = "";

        // true = the login link and password were emailed
        public bool EmailSent { get; set; }

        // Only filled when the email could not be sent, so the admin can pass the password on
        public string? TemporaryPassword { get; set; }
        public string? EmailError { get; set; }
    }
}
