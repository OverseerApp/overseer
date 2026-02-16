using System.CommandLine;
using LiteDB;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using Overseer.Server;
using Overseer.Server.Api;
using Overseer.Server.Data;
using Overseer.Server.Hubs;
using Overseer.Server.Infrastructure;
using Overseer.Server.Machines;
using Overseer.Server.Mappers;
using Overseer.Server.Models;
using Overseer.Server.Updates;

if (!UpdateManager.Update())
{
  throw new Exception("The Overseer database update process failed. Please check the service logs for more details.");
}

Mappers.RegisterMappers();

using var context = new LiteDataContext();
var values = context.ValueStore();
var settings = values.GetOrPut(() => new ApplicationSettings());
var portOption = new Option<int?>("--port") { Description = "The local port Overseer will listen on." };
var intervalOption = new Option<int?>("--interval") { Description = "How often Overseer will poll for updates." };
var command = new RootCommand("Overseer CLI Options...");
var parseResults = command.Parse(args);
settings.LocalPort = parseResults.GetValue(portOption) ?? ApplicationSettings.DefaultPort;
settings.Interval = parseResults.GetValue(intervalOption) ?? ApplicationSettings.DefaultInterval;

var builder = WebApplication.CreateBuilder(args);

// Configure logging to output to console for Docker
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddOutputCache(options =>
{
  options.AddPolicy(
    "RefreshCache",
    policy =>
    {
      policy.AddPolicy<RefreshCachePolicy>();
      policy.Expire(TimeSpan.FromHours(1));
    }
  );

  options.AddPolicy(
    "ColdCache",
    policy =>
    {
      policy.Expire(TimeSpan.FromDays(30));
    }
  );
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

var isDev = builder.Environment.IsDevelopment();
if (isDev)
{
  builder.Services.AddCors(options =>
  {
    options.AddPolicy(
      "DevCorsPolicy",
      policy =>
      {
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod().AllowCredentials();
      }
    );
  });
}

builder.Services.AddOverseerDependencies(context);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).UseOverseerAuthentication(isDev);

builder
  .Services.AddAuthorizationBuilder()
  .AddPolicy(
    AccessLevel.Readonly.ToString(),
    policy => policy.RequireRole(AccessLevel.Readonly.ToString(), AccessLevel.User.ToString(), AccessLevel.Administrator.ToString())
  )
  .AddPolicy(AccessLevel.User.ToString(), policy => policy.RequireRole(AccessLevel.User.ToString(), AccessLevel.Administrator.ToString()))
  .AddPolicy(AccessLevel.Administrator.ToString(), policy => policy.RequireRole(AccessLevel.Administrator.ToString()));
var app = builder.Build();

XmlConfigurator.Configure(new FileInfo(Path.Combine(app.Environment.ContentRootPath, "log4net.config")));

app.UseWebSockets();

if (isDev)
{
  app.UseCors("DevCorsPolicy");
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.HandleOverseerExceptions();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();
app.MapOverseerApi();
app.MapHub<StatusHub>("/push/status").RequireAuthorization();
app.MapHub<NotificationHub>("/push/notifications").RequireAuthorization();

var url = $"http://*:{settings.LocalPort}";
if (!isDev)
{
  app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "browser")) });

  app.UseSpa(spa =>
  {
    spa.Options.SourcePath = "/browser";
    spa.Options.DefaultPageStaticFileOptions = new StaticFileOptions
    {
      FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "browser")),
    };
  });
}

var monitoringService = app.Services.GetRequiredService<IMonitoringService>();
monitoringService.StartMonitoring();

app.Run(url);
