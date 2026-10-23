using Microsoft.EntityFrameworkCore;
using InvoiceManager.Data;
using InvoiceManager.Models;
using InvoiceManager.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o =>
{
    // Mensajes de enlace de modelo en español
    var m = o.ModelBindingMessageProvider;
    m.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
    m.SetMissingBindRequiredValueAccessor(_ => "Este campo es obligatorio.");
    m.SetAttemptedValueIsInvalidAccessor((v, _) => $"El valor «{v}» no es válido.");
    m.SetValueIsInvalidAccessor(v => $"El valor «{v}» no es válido.");
    m.SetValueMustBeANumberAccessor(_ => "Introduce un número.");
    m.SetUnknownValueIsInvalidAccessor(_ => "Valor no válido.");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(v => $"El valor «{v}» no es válido.");
});

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=invoicemanager.db"));
builder.Services.AddScoped<InvoiceNumberService>();
builder.Services.AddScoped<InvoiceStatusService>();
builder.Services.Configure<CompanyOptions>(builder.Configuration.GetSection("Company"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.UseStaticFiles();
app.UseRouting();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
