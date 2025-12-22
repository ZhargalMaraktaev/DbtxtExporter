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

builder.Services.AddDbContext<NktDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NKT")));
builder.Services.AddDbContext<NotPakDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("NOT")));
builder.Services.AddDbContext<Tpa140DbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("TPA140")));
builder.Services.AddDbContext<UostIzmLinDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("TPA140")));
builder.Services.AddDbContext<UrzBilDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("TPA140")));
builder.Services.AddDbContext<DelayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("N0T")));

builder.Services.AddHostedService<ExportHostedService>();

var host = builder.Build();
await host.RunAsync();
