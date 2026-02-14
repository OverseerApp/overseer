using System.Net;
using System.Reflection;
using System.Security.Claims;
using log4net;
using Microsoft.AspNetCore.Diagnostics;
using Octokit;
using Overseer.Server.Channels;
using Overseer.Server.Data;
using Overseer.Server.Integration.Automation;
using Overseer.Server.Integration.Common;
using Overseer.Server.Integration.Machines;
using Overseer.Server.Machines;
using Overseer.Server.Models;
using Overseer.Server.Plugins;
using Overseer.Server.Services;
using Overseer.Server.System;
using Overseer.Server.Users;
using Machine = Overseer.Server.Integration.Machines.Machine;

namespace Overseer.Server;

public static class Extensions
{
  static readonly ILog Log = LogManager.GetLogger("Overseer.Server");

  public static void DoNotAwait(this Task _) { }

  /// <summary>
  /// Will return all non-abstract class types that are assignable to the base type
  /// that exist within the assembly for that type
  /// </summary>
  public static IReadOnlyList<Type> GetAssignableTypes(this Type baseType)
  {
    IEnumerable<Type> types;

    try
    {
      types = baseType.Assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
      types = ex.Types.Where(t => t != null)!;
    }

    return [.. types.Where(type => baseType.IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)];
  }

  public static void AddOrReplace<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
  {
    if (!dictionary.TryAdd(key, value))
    {
      dictionary[key] = value;
    }
  }

  public static IServiceCollection AddOverseerDependencies(this IServiceCollection services, IDataContext context)
  {
    services.AddHttpClient();
    services.AddSingleton(context);
    services.AddSingleton<Func<Machine, IMachineProvider>>(provider =>
      machine =>
      {
        var machineProviderType = MachineProviderManager.GetProviderType(machine);
        var machineProvider =
          (IMachineProvider)ActivatorUtilities.CreateInstance(provider, machineProviderType, machine)
          ?? throw new Exception("Unable to create provider");

        return machineProvider;
      }
    );
    services.AddSingleton<Func<Machine, MachineJob, JobSentinel>>(provider =>
      (machine, job) =>
      {
        // This will need to change because it could be possible that the user has multiple failure detection analyzer
        // plugins installed, if that is the case, then we  need to create multiple sentinels per job.
        var failureDetectionAnalyzer = provider.GetRequiredService<IFailureDetectionAnalyzer>();
        var jobFailureChannel = provider.GetRequiredService<IJobFailureChannel>();
        var configurationManager = provider.GetRequiredService<Settings.IConfigurationManager>();
        return new JobSentinel(machine, job, failureDetectionAnalyzer, configurationManager, jobFailureChannel);
      }
    );
    services.AddTransient<IAuthenticationManager, Users.AuthenticationManager>();
    services.AddTransient<IAuthorizationManager, AuthorizationManager>();
    services.AddSingleton<IRateLimitingService, RateLimitingService>();
    services.AddTransient<Settings.IConfigurationManager, Settings.ConfigurationManager>();
    services.AddTransient<IUserManager, UserManager>();
    services.AddTransient<IMachineManager, MachineManager>();
    services.AddTransient<IControlManager, ControlManager>();
    services.AddTransient<ISystemManager, SystemManager>();
    services.AddTransient<IGitHubClient>((_) => new GitHubClient(new ProductHeaderValue("OverseerApp")));
    services.AddTransient<IPluginManager, PluginManager>();

    services.AddSingleton<IMonitoringService, MonitoringService>();
    services.AddSingleton<MachineProviderManager>();
    services.AddSingleton<IMachineStatusChannel, MachineStatusChannel>();
    services.AddSingleton<IRestartMonitoringChannel, RestartMonitoringChannel>();
    services.AddSingleton<ICertificateExceptionChannel, CertificateExceptionChannel>();
    services.AddSingleton<INotificationChannel, NotificationChannel>();
    services.AddSingleton<IJobFailureChannel, JobFailureChannel>();

    services.AddHostedService<MachineStatusUpdateService>();
    services.AddHostedService<CertificateExceptionService>();
    services.AddHostedService<RestartMonitoringService>();
    services.AddHostedService<MachineJobService>();
    services.AddHostedService<NotificationService>();
    services.AddHostedService<JobSentinelService>();
    services.AddHostedService<JobFailureService>();

    var pluginConfigurations = PluginDiscoveryService.DiscoverPlugins().ToList();
    foreach (var pluginConfiguration in pluginConfigurations)
    {
      pluginConfiguration.ConfigureServices(services);
    }

    return services;
  }

  public static WebApplication HandleOverseerExceptions(this WebApplication app)
  {
    app.UseExceptionHandler(builder =>
    {
      builder.Run(async context =>
      {
        var exception = context.Features.Get<IExceptionHandlerFeature>()!.Error;
        if (exception != null)
        {
          Log.Error("Server Error", exception);
          if (exception.InnerException != null)
          {
            exception = exception.InnerException;
          }

          object exceptionModel = exception is OverseerException oEx
            ? new
            {
              message = oEx.Message,
              oEx.Properties,
              exceptionType = "overseer",
            }
            : new { message = exception.Message };

          context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
          context.Response.ContentType = "application/json";
          await context.Response.WriteAsJsonAsync(exceptionModel);
        }
      });
    });

    return app;
  }

  public static UserDisplay GetUser(this ClaimsPrincipal principal, IUserManager userManager)
  {
    var isAuthenticated = principal?.Identity?.IsAuthenticated ?? false;
    if (!isAuthenticated)
    {
      throw new OverseerException("unauthenticated");
    }

    var userIdClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
    {
      throw new OverseerException("invalid_user");
    }

    var user = userManager.GetUser(userId);
    if (user == null)
    {
      throw new OverseerException("user_not_found");
    }

    return user;
  }
}
