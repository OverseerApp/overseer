namespace Overseer.Server.Api
{
  public static class OverseerApi
  {
    public static WebApplication MapOverseerApi(this WebApplication app)
    {
      app.MapGroup("/api")
        .MapAuthenticationApi()
        .MapConfigurationApi()
        .MapControlApi()
        .MapLoggingApi()
        .MapMachineApi()
        .MapUsersApi()
        .MapNotificationApi()
        .MapUpdateApi()
        .MapPluginsApi();

      return app;
    }
  }
}
