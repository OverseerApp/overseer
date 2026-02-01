using Newtonsoft.Json.Linq;
using Overseer.Server.Machines;
using Overseer.Server.Models;

namespace Overseer.Server.Api
{
  public static class MachineApi
  {
    public class MachineBindingModel(Machine machine)
    {
      public Machine Machine { get; set; } = machine;

      public static async ValueTask<MachineBindingModel?> BindAsync(HttpContext context)
      {
        using var reader = new StreamReader(context.Request.Body);
        var machineJson = await reader.ReadToEndAsync();
        var jObject = JObject.Parse(machineJson);
        string machineTypeName = jObject["machineType"]?.Value<string>() ?? "Unknown";

        return jObject.ToObject(Machine.GetMachineType(machineTypeName)) is not Machine machine
          ? throw new Exception("Unable to parse machine")
          : new MachineBindingModel(machine);
      }
    }

    public static RouteGroupBuilder MapMachineApi(this RouteGroupBuilder builder)
    {
      var group = builder.MapGroup("/machines").WithTags("Machines");
      group.RequireAuthorization();

      group.MapGet("/", (IMachineManager machines) => Results.Ok(machines.GetMachines()));

      group.MapGet("/{id}", (int id, IMachineManager machines) => Results.Ok(machines.GetMachine(id)));

      group
        .MapPost("/", async (MachineBindingModel model, IMachineManager machines) => Results.Ok(await machines.CreateMachine(model.Machine)))
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group
        .MapPut("/", async (MachineBindingModel model, IMachineManager machines) => Results.Ok(await machines.UpdateMachine(model.Machine)))
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group
        .MapDelete("/{id}", (int id, IMachineManager machines) => Results.Ok(machines.DeleteMachine(id)))
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group
        .MapPost(
          "/sort",
          (List<int> order, IMachineManager machines) =>
          {
            machines.SortMachines(order);
            return Results.Ok();
          }
        )
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group.MapGet("/types", (IMachineManager machines) => Results.Ok(machines.GetMachineTypes()));

      return builder;
    }
  }
}
