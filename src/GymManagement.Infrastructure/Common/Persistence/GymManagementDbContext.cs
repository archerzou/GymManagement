using GymManagement.Application.Common.Interfaces;
using GymManagement.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Infrastructure.Common.Persistence;
public class GymManagementDbContext(DbContextOptions options) : DbContext(options), IUnitOfWork
{
    public DbSet<Subscription> Subscriptions { get; set; } = null!;

    public async Task CommitChangesAsync()
    {
        await SaveChangesAsync();
    }
}
