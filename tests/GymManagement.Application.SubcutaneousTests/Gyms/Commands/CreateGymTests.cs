using ErrorOr;
using FluentAssertions;
using GymManagement.Application.Gyms.Commands.CreateGym;
using GymManagement.Application.SubcutaneousTests.Common;
using GymManagement.Application.Subscriptions.Commands.CreateSubscription;
using GymManagement.Domain.Gyms;
using GymManagement.Domain.Subscriptions;
using MediatR;
using TestCommon.Gyms;

namespace GymManagement.Application.SubcutaneousTests.Gyms.Commands;

[Collection(MediatorFactoryCollection.CollectionName)]
public class CreateGymTests(MediatorFactory mediatorFactory)
{
    private readonly IMediator _mediator = mediatorFactory.CreateMediator();

    [Fact]
    public async Task CreateGym_WhenValidCommand_ShouldCreateGym()
    {
        // Arrange
        Subscription subscription = await CreateSubscription();

        // Create a valid CreateGymCommand
        CreateGymCommand createGymCommand = GymCommandFactory.CreateCreateGymCommand(subscriptionId: subscription.Id);

        // Act
        ErrorOr<Gym> createGymResult = await _mediator.Send(createGymCommand);

        // Assert
        createGymResult.IsError.Should().BeFalse();
        createGymResult.Value.SubscriptionId.Should().Be(subscription.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(200)]
    public async Task CreateGym_WhenCommandContainsInvalidData_ShouldReturnValidationError(int gymNameLength)
    {
        // Arrange
        string gymName = new('a', gymNameLength);
        CreateGymCommand createGymCommand = GymCommandFactory.CreateCreateGymCommand(name: gymName);

        // Act
        ErrorOr<Gym> result = await _mediator.Send(createGymCommand);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Name");
    }

    private async Task<Subscription> CreateSubscription()
    {
        //  1. Create a CreateSubscriptionCommand
        CreateSubscriptionCommand createSubscriptionCommand = SubscriptionCommandFactory.CreateCreateSubscriptionCommand();

        //  2. Sending it to MediatR
        ErrorOr<Subscription> result = await _mediator.Send(createSubscriptionCommand);

        //  3. Making sure it was created successfully
        result.IsError.Should().BeFalse();
        return result.Value;
    }
}
