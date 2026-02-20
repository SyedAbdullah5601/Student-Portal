using Microsoft.EntityFrameworkCore;
using StudentPortal.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. Optimized Controller & JSON Settings
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Maintains PascalCase to match your CandidateViewModel exactly
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

        // Makes Enums (like CrudAction) readable as strings in JSON if needed
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 2. Database Context
builder.Services.AddDbContext<StudentLoginContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost\\SQLEXPRESS;Database=StudentLogin;Trusted_Connection=True;TrustServerCertificate=True;"));

// 3. Session & Cache Optimization
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // IMPORTANT
});

// 4. Access Configuration in DI (Required for SendOtpEmail)
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// CRITICAL: Session must come AFTER UseRouting and BEFORE UseAuthorization
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Candidate}/{action=Login}/{id?}");

app.Run();