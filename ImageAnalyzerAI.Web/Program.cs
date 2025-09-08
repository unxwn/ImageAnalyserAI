using Azure.Storage.Blobs;
using ImageAnalyzerAI.Web.Services.Abstraction;
using ImageAnalyzerAI.Web.Services.Implementation;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IStorageService, AzureStorageService>();

builder.Services.AddSingleton<BlobServiceClient>(sP =>
{
    var config = sP.GetRequiredService<IConfiguration>();
    return new BlobServiceClient(config.GetValue<string>("AzureStorage:ConnectionString"));
});

var app = builder.Build();

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
