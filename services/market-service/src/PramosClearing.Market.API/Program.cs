using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PramosClearing.MarketService.Application.Commands;
using PramosClearing.MarketService.Domain.Repositories;
using PramosClearing.MarketService.Infrastructure;
using PramosClearing.MarketService.Infrastructure.Persistence;
using PramosClearing.MarketService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<ConnectionStringsOptions>(
    builder.Configuration.GetSection("ConnectionStrings"));

builder.Services.AddDbContext<MarketDbContext>((sp, options) =>
{
    var connectionStrings = sp.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value;
    options.UseSqlServer(connectionStrings.DefaultConnection);
});

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CreateStockCommand>());

builder.Services.AddScoped<IStockRepository, StockRepository>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<MarketDbContext>().Database.Migrate();

app.UseSwagger();
app.UseSwaggerUI();


app.UseAuthorization();

app.MapControllers();

app.Run();
