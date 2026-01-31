using Microsoft.AspNetCore.OutputCaching;

namespace Overseer.Server.Infrastructure;

public class RefreshCachePolicy : IOutputCachePolicy
{
  public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
  {
    var refreshRequested = context.HttpContext.Request.Query.ContainsKey("refresh");
    context.EnableOutputCaching = true;
    context.AllowCacheLookup = !refreshRequested;
    context.AllowCacheStorage = true;

    return ValueTask.CompletedTask;
  }

  public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

  public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
