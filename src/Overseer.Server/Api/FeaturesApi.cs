using Overseer.Server.Integration.Automation;
using Overseer.Server.System;

namespace Overseer.Server.Api;

public static class FeaturesApi
{
  public static RouteGroupBuilder MapFeaturesApi(this RouteGroupBuilder builder)
  {
    var group = builder.MapGroup("/features");
    group.RequireAuthorization();

    group.MapGet("/ai-monitoring", (IEnumerable<IFailureDetectionAnalyzer> failureDetectionAnalyzers) => Results.Ok(failureDetectionAnalyzers.Any()));

    group.MapGet("/auto-update", (ISystemManager systemService) => Results.Ok(systemService.CanAutoUpdate()));

    return builder;
  }
}
