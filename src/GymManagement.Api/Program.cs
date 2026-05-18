using GymManagement.Application;
using GymManagement.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services
        .AddApplication()
        .AddInfrastructure();
}

WebApplication app = builder.Build();
{
    app.UseExceptionHandler();

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
