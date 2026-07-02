using ErrorOr;
using FluentValidation.Results;
using GymManagement.Domain.Gyms;
using MediatR;

namespace GymManagement.Application.Gyms.Commands.CreateGym;

public class CreateGymCommandBehavior: IPipelineBehavior<CreateGymCommand, ErrorOr<Gym>>
{
    public async Task<ErrorOr<Gym>> Handle(
        CreateGymCommand request, 
        RequestHandlerDelegate<ErrorOr<Gym>> next, 
        CancellationToken cancellationToken)
    {
        var validator = new CreateGymCommandValidator();

        ValidationResult? validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return validationResult.Errors
                .Select(error => Error.Validation(code: error.PropertyName, description: error.ErrorMessage))
                .ToList();
        }

        return await next();
    }
}
