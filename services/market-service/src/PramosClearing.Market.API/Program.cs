using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PramosClearing.MarketService.Application.Commands;
using PramosClearing.MarketService.Infrastructure;
using PramosClearing.MarketService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<ConnectionStringsOptions>(
    builder.Configuration.GetSection("ConnectionStrings"));

builder.Services.AddDbContext<MarketDbContext>((sp, options) =>
{
    var connectionStrings = sp.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value;
    options.UseSqlServer(connectionStrings.DefaultConnection);
});

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CreateStockCommand>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<MarketDbContext>().Database.Migrate();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
