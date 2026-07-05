using ErrorOr;
using GymManagement.Application.Common.Interfaces;
using GymManagement.Domain.Admins;
using GymManagement.Domain.Subscriptions;
using MediatR;

namespace GymManagement.Application.Subscriptions.Commands.DeleteSubscription;

public class DeleteSubscriptionCommandHandler(
    IAdminsRepository adminsRepository,
    ISubscriptionsRepository subscriptionsRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteSubscriptionCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(DeleteSubscriptionCommand command, CancellationToken cancellationToken)
    {
        Subscription? subscription = await subscriptionsRepository.GetByIdAsync(command.SubscriptionId);

        if (subscription is null)
        {
            return Error.NotFound(description: "Subscription not found");
        }

        Admin? admin = await adminsRepository.GetByIdAsync(subscription.AdminId);

        if (admin is null)
        {
            return Error.Unexpected(description: "Admin not found");
        }

        admin.DeleteSubscription(command.SubscriptionId);

        await adminsRepository.UpdateAsync(admin);
        await unitOfWork.CommitChangesAsync();

        return Result.Deleted;
    }
}

