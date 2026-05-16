using Microsoft.EntityFrameworkCore;
using Crime_Reporting_and_Tracking_System.Data;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. SERVICES REGISTRATION (Must be BEFORE builder.Build())
// ==========================================

builder.Services.AddControllersWithViews();

builder.Services.AddSession();

// ---- DB CONTEXT CONFIGURATION (Sahi Jagah) ----
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// ----------------------------------------------

var app = builder.Build();

// ==========================================
// 2. MIDDLEWARE PIPELINE (Must be AFTER builder.Build())
// ==========================================

// Error Handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Note: Session ko authorization se pehle rakhna behtar hota hai
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();