using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Models;
using PetSocial.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 1. Cáº¥u hĂ¬nh DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSession();

// 2. Cáº¥u hĂ¬nh Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(options => {
    // TĂ¹y chá»‰nh policy password táº¡i Ä‘Ă¢y
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();


// Ghi Ä‘Ă¨ Password Hasher máº·c Ä‘á»‹nh báº±ng BCrypt
builder.Services.AddScoped<IPasswordHasher<AppUser>, BCryptPasswordHasher>();

// ThĂªm cáº¥u hĂ¬nh nĂ y Ä‘á»ƒ há»‡ thá»‘ng khĂ´ng Ă©p buá»™c Cookie pháº£i qua HTTPS khi cháº¡y á»Ÿ localhost
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// 3. Cáº¥u hĂ¬nh Cookie (Thay tháº¿ cho Session Auth truyá»n thá»‘ng)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "PetSocialAuthCookie";
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ThĂªm Session thĂ´ng thÆ°á»ng náº¿u cáº§n lÆ°u trá»¯ data táº¡m thá»i
builder.Services.AddSession();
builder.Services.AddControllersWithViews();


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


// 1. ThĂªm Route cho Area Admin
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Splash}/{id?}");

app.Run();
