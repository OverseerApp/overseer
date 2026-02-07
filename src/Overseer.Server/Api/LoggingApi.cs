using log4net;
using log4net.Appender;
using Overseer.Server.Models;
using LogLevel = Overseer.Server.Models.LogLevel;

namespace Overseer.Server.Api
{
  public static class LoggingApi
  {
    static readonly ILog Log = LogManager.GetLogger(typeof(LoggingApi));

    public static RouteGroupBuilder MapLoggingApi(this RouteGroupBuilder builder)
    {
      var group = builder.MapGroup("/logging").WithTags("Logging");
      group.RequireAuthorization();

      group.MapGet(
        "/",
        () =>
        {
          var fileAppender = LogManager.GetAllRepositories().SelectMany(r => r.GetAppenders()).OfType<FileAppender>().FirstOrDefault();

          if (fileAppender == null)
            return Results.Ok();
          if (string.IsNullOrWhiteSpace(fileAppender.File))
            return Results.Ok();
          if (!File.Exists(fileAppender.File))
            return Results.Ok();

          // copy the file and then read because the logger keeps the file open
          var tempPath = Path.GetTempFileName();
          File.Copy(fileAppender.File, tempPath, true);
          var logFileContent = File.ReadAllText(tempPath);
          File.Delete(tempPath);

          return Results.Ok(new { content = logFileContent });
        }
      );

      group
        .MapPost(
          "/",
          (LogEntry logEntry) =>
          {
            switch (logEntry.Level)
            {
              case LogLevel.INFO:
                Log.Info(logEntry.ToString());
                break;
              case LogLevel.WARN:
                Log.Warn(logEntry.ToString());
                break;
              case LogLevel.ERROR:
              case LogLevel.FATAL:
                Log.Error(logEntry.ToString());
                break;
              default:
                Log.Debug(logEntry.ToString());
                break;
            }

            return Results.Ok();
          }
        )
        .AllowAnonymous();

      return builder;
    }
  }
}
