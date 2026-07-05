using GymManagement.Application;
using GymManagement.Infrastructure;
using GymManagement.Infrastructure.Common.Middleware;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services.AddHttpContextAccessor();
    builder.Services
        .AddApplication()
        .AddInfrastructure();
}

WebApplication app = builder.Build();
{
    app.UseExceptionHandler();
    app.AddInfrastructureMiddleware();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
