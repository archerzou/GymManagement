using GymManagement.Application.Common.Interfaces;
using GymManagement.Domain.Admins.Events;
using GymManagement.Domain.Gyms;
using MediatR;

namespace GymManagement.Application.Gyms.Events;

public class SubscriptionDeletedEventHandler(
    IGymsRepository gymsRepository,
    IUnitOfWork unitOfWork)
    : INotificationHandler<SubscriptionDeletedEvent>
{
    public async Task Handle(SubscriptionDeletedEvent notification, CancellationToken cancellationToken)
    {
        List<Gym> gyms = await gymsRepository.ListBySubscriptionIdAsync(notification.SubscriptionId);

        await gymsRepository.RemoveRangeAsync(gyms);
        await unitOfWork.CommitChangesAsync();
    }
}
