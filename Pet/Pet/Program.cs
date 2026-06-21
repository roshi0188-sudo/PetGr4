using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Hubs;
using PetSocial.Models;
using PetSocial.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.PostConfigure<OpenAiOptions>(options =>
{
    options.ApiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
});
builder.Services.AddHttpClient<IPetAiService, OpenAiPetService>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com");
    client.Timeout = TimeSpan.FromSeconds(45);
});

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
    dbContext.Database.ExecuteSqlRaw(@"
        IF COL_LENGTH('dbo.Posts', 'IsRemovedByAi') IS NULL
            ALTER TABLE [dbo].[Posts] ADD [IsRemovedByAi] bit NOT NULL CONSTRAINT [DF_Posts_IsRemovedByAi] DEFAULT CAST(0 AS bit);

        IF COL_LENGTH('dbo.Posts', 'ViolationReason') IS NULL
            ALTER TABLE [dbo].[Posts] ADD [ViolationReason] nvarchar(500) NULL;

        IF COL_LENGTH('dbo.Posts', 'RemovedAt') IS NULL
            ALTER TABLE [dbo].[Posts] ADD [RemovedAt] datetime2 NULL;
    ");
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
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");



app.MapControllerRoute(
    name: "post",
    pattern: "Post/{action=Community}/{id?}", 
    defaults: new { controller = "Post" });

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
