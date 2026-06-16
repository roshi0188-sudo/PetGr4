using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Hubs;
using PetSocial.Models;
using PetSocial.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 1. Cáº¥u hĂ¬nh DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSession();


builder.Services.AddIdentity<AppUser, IdentityRole>(options => {

    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();



builder.Services.AddScoped<IPasswordHasher<AppUser>, BCryptPasswordHasher>();


builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});


builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "PetSocialAuthCookie";
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddSignalR();


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "pet",
    pattern: "Pet/{action=Index}/{id?}",
    defaults: new { controller = "Pet" });

app.MapControllerRoute(
    name: "post",
    pattern: "Post/{action=Community}/{id?}", 
    defaults: new { controller = "Post" });
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Splash}/{id?}");

app.MapHub<ChatHub>("/chatHub");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    // Gọi file class vừa tạo và chạy hàm Seed
    await PetSocial.Data.RoleSeeder.SeedRolesAndUsersAsync(services);
}


app.Run();