using GymManagement.Application.Common.Interfaces;
using GymManagement.Domain.Subscriptions;
using GymManagement.Infrastructure.Common.Persistence;

namespace GymManagement.Infrastructure.Subscriptions.Persistence;
public class SubscriptionsRepository(GymManagementDbContext dbContext) : ISubscriptionsRepository
{
    public async Task AddSubscriptionAsync(Subscription subscription)
    {
        await dbContext.Subscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();
    }

    public async Task<Subscription?> GetByIdAsync(Guid subscriptionId)
    {
        return await dbContext.Subscriptions.FindAsync(subscriptionId);
    }
}
