using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DbtxtExporter.Data;
using DbtxtExporter.Services;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.Configure<ExportSettings>(
    builder.Configuration.GetSection("Export"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NKT")));


builder.Services.AddHostedService<ExportHostedService>();

var host = builder.Build();
await host.RunAsync();
