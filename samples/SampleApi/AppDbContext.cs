using Microsoft.EntityFrameworkCore;

namespace SampleApi;

public class AppDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseNpgsql("Host=localhost;Database=sample");
}

public class Order
{
    public int Id { get; set; }
    public string Notes { get; set; }
}
