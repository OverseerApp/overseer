using System.CommandLine;
using log4net.Config;
using Microsoft.Extensions.FileProviders;
using Overseer.Server;
using Overseer.Server.Api;
using Overseer.Server.Data;
using Overseer.Server.Hubs;
using Overseer.Server.Models;
using Overseer.Server.Updates;

if (!UpdateManager.Update())
{
  throw new Exception("The Overseer database update process failed. Please check the service logs for more details.");
}

using (var context = new LiteDataContext())
{
  var values = context.ValueStore();
  var settings = values.GetOrPut(() => new ApplicationSettings());
  var portOption = new Option<int?>("--port") { Description = "The local port Overseer will listen on." };
  var intervalOption = new Option<int?>("--interval") { Description = "How often Overseer will poll for updates." };
  var command = new RootCommand("Overseer CLI Options...");
  var parseResults = command.Parse(args);
  settings.LocalPort = parseResults.GetValue(portOption) ?? ApplicationSettings.DefaultPort;
  settings.Interval = parseResults.GetValue(intervalOption) ?? ApplicationSettings.DefaultInterval;

  var builder = WebApplication.CreateBuilder(args);
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
  builder.Services.AddAuthentication(OverseerAuthenticationOptions.Setup).UseOverseerAuthentication();

  builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy("Readonly", policy => policy.RequireRole(AccessLevel.Readonly.ToString()))
    .AddPolicy("Administrator", policy => policy.RequireRole(AccessLevel.Administrator.ToString()));

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

  app.Run(url);
}
