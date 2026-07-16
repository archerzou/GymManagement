using GymManagement.Api;
using GymManagement.Application;
using GymManagement.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
{
    builder.Services
        .AddPresentation()
        .AddApplication()
        .AddInfrastructure(builder.Configuration);
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
