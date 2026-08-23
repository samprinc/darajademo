using DarajaDemo.Data;
using DarajaDemo.Hubs;
using DarajaDemo.Models.Config;
using DarajaDemo.Services.Implementations;
using DarajaDemo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuration Binding
builder.Services.Configure<DarajaSettings>(builder.Configuration.GetSection("Daraja"));

// 2. Database Setup (PostgreSQL)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Caching & Real-time
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

// 4. HTTP Clients & Services
builder.Services.AddHttpClient("Daraja");
builder.Services.AddScoped<IMpesaService, MpesaService>();

// 5. API Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 6. CORS Policy for Frontend/POS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Map SignalR Hub
app.MapHub<PaymentHub>("/hubs/payment");

app.Run();