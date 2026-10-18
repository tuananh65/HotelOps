using HotelOps.Api.Data;
using HotelOps.Api.Services.Assets;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<HotelOpsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("HotelOpsDb")));

// Add services to the container.
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IMaintenanceWorkService, MaintenanceWorkService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
