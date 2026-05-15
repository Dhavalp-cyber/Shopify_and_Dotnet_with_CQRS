using Microsoft.EntityFrameworkCore;
using ShopifyProductSync;
using ShopifyProductSync.Configuration;
using ShopifyProductSync.Data;
using ShopifyProductSync.Services;

var builder = WebApplication.CreateBuilder(args);


// 1. ADD CONTROLLERS
builder.Services.AddControllers();


// 2. ADD SWAGGER (API documentation and testing UI)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Shopify Product Sync API",
        Version = "v1",
        Description = "API for syncing products between Shopify and local database using ShopifySharp."
    });
});

// 3. DATABASE
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 4. HTTP CLIENT
builder.Services.AddHttpClient("ShopifyGraphQL");

// 5. APPLICATION SERVICES (existing)
builder.Services.AddScoped<ShopifyProductService>();
builder.Services.AddScoped<ShopifyGraphQLService>();
builder.Services.AddScoped<ProductDbService>();
builder.Services.AddScoped<ShopifyWebhookService>();

// 5a. INVENTORY SERVICE (new)
builder.Services.AddScoped<ShopifyInventoryService>();

// 5b. FULFILLMENT SERVICE (new)
builder.Services.Configure<ShopifySettings>(
    builder.Configuration.GetSection(ShopifySettings.SectionName));
builder.Services.AddScoped<ShopifyFulfillmentService>();

// 6. LOGGING
builder.Services.AddLogging();

// 7. CQRS — MediatR + FluentValidation + Pipeline Behaviors
builder.Services.AddApplicationServices();


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Shopify Product Sync API v1");
    options.RoutePrefix = string.Empty; // Swagger opens at root URL: https://localhost:PORT/
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Apply migrations at startup — non-fatal if DB is unavailable
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not apply database migrations at startup. " +
            "The app will still run — DB-dependent endpoints will fail until a connection is available.");
    }
}

app.Run();
