using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Data;

public class MarketLinkDbContext : DbContext
{
    public MarketLinkDbContext(DbContextOptions<MarketLinkDbContext> options)
        : base(options)
    {}

    protected MarketLinkDbContext(){}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SQL Server rejects multiple cascade paths between users, profiles and their data.
        // Deleting a parent with dependent records must be handled explicitly by the application.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
            }
        }
    }

    
    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<CustomerProfile> CustomerProfiles { get; set; }
    public DbSet<FarmerProfile> FarmerProfiles { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<District> Districts { get; set; }
    public DbSet<Market> Markets { get; set; }
    public DbSet<Stall> Stalls { get; set; }

    
    public DbSet<ProductCategory> ProductCategories { get; set; }
    public DbSet<ProductExp> ProductExps { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<StockPrice> StockPrices { get; set; }

    
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderSnapshot> OrderSnapshots { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<FavoriteFarmer> FavoriteFarmers { get; set; }
    public DbSet<FarmerReview> FarmerReviews { get; set; }
    public DbSet<OrderDispute> OrderDisputes { get; set; }
    public DbSet<UserWarning> UserWarnings { get; set; }
}
