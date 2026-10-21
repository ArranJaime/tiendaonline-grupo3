using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Amazon.S3;
using Amazon.SecretsManager;
using Amazon.CloudWatch;
using StackExchange.Redis;
using TiendaOnline.Data;
using TiendaOnline.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

var db = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Postgres; apunte a PgBouncer 6432 en AWS.");

// En AWS la contraseña de RDS se lee de Secrets Manager (AWS SDK); en tu PC se usa appsettings.json.
db = DatabaseSecret.BuildConnectionString(builder.Configuration, db);
builder.Services.AddDbContext<ShopDbContext>(o => o.UseNpgsql(db));

var redis = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redis)) {
    var multiplexer = ConnectionMultiplexer.Connect(redis);
    builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redis);
    builder.Services.AddDataProtection()
        .SetApplicationName("URPOnline")
        .PersistKeysToStackExchangeRedis(multiplexer, "URPOnline-DataProtection-Keys");
} else if (builder.Environment.IsDevelopment()) {
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddDataProtection().SetApplicationName("URPOnline");
} else {
    throw new InvalidOperationException("Redis es obligatorio fuera de Development para compartir sesiones entre EC2.");
}

builder.Services.AddSession(o => {
    o.Cookie.Name = ".URPOnline.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<BusinessEvents>();
builder.Services.AddScoped<BusinessMetrics>();
builder.Services.AddHttpClient<MercadoPagoService>();
builder.Services.AddHostedService<ActiveCartsReportingService>();

// Búsqueda semántica: modelo local ONNX + pgvector en PostgreSQL.
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddScoped<ProductVectorStore>();
builder.Services.AddHostedService<ProductEmbeddingIndexer>();

builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonSecretsManager>();
builder.Services.AddAWSService<IAmazonCloudWatch>();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownNetworks = { },
    KnownProxies = { }
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

if (app.Configuration.GetValue<bool>("InitializeDatabase")) {
    using var scope = app.Services.CreateScope();
    // Migraciones EF Core versionadas: Si existe ConnectionStrings:PostgresDirect (puerto 5432 directo a RDS), usarlo para evitar
    // que pg_advisory_lock falle a través de PgBouncer en modo transaction (Indicador 2a).
    var directConn = app.Configuration.GetConnectionString("PostgresDirect");
    var migDb = string.IsNullOrWhiteSpace(directConn)
        ? scope.ServiceProvider.GetRequiredService<ShopDbContext>()
        : new ShopDbContext(new DbContextOptionsBuilder<ShopDbContext>()
            .UseNpgsql(DatabaseSecret.BuildConnectionString(app.Configuration, directConn))
            .Options);

    await migDb.Database.OpenConnectionAsync();
    try {
        await migDb.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(727001)");
        await migDb.Database.MigrateAsync();
        await SeedData.LoadAsync(migDb);
    } finally {
        await migDb.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(727001)");
        await migDb.Database.CloseConnectionAsync();
        if (!string.IsNullOrWhiteSpace(directConn)) await migDb.DisposeAsync();
    }
}

app.Run();
