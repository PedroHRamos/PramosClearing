using Microsoft.EntityFrameworkCore;
using PramosClearing.UserService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<UserDbContext>().Database.Migrate();

app.UseAuthorization();

app.MapControllers();

app.Run();
