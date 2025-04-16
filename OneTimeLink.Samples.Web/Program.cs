using Microsoft.EntityFrameworkCore;
using OneTimeLink.EntityFrameworkCore.Extensions;
using OneTimeLink.Core.Configurations;
using OneTimeLink.EntityFrameworkCore.Extensions;
using OneTimeLink.Samples.Web;
using OneTimeLink.Samples.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure OneTimeLink with Entity Framework Core
builder.Services.AddLinkWithEfCore<ApplicationDbContext>(
    options => {
        options.DefaultExpiration = TimeSpan.FromHours(24);
        options.TokenLength = 32;
    },
    dbOptions => dbOptions.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// Add email sender (mock implementation for demo)
builder.Services.AddScoped<IEmailSender, MockEmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

app.Run();