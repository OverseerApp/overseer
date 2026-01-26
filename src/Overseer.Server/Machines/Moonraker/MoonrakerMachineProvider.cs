using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json.Linq;
using Overseer.Server.Channels;
using Overseer.Server.Machines.Moonraker.Models;
using Overseer.Server.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Overseer.Server.Machines.Moonraker;

public class MoonrakerMachineProvider(MoonrakerMachine machine, IMachineStatusChannel machineStatusChannel, IHttpClientFactory httpClientFactory)
  : WebSocketMachineProvider<MoonrakerMachine>(machineStatusChannel)
{
  private long? _connectionId;
  private int _messageId = 0;

  public override MoonrakerMachine Machine { get; protected set; } = machine;

  public override Task ExecuteGcode(string command)
  {
    throw new NotImplementedException();
  }

  public override async Task LoadConfiguration(Machine machine)
  {
    Machine = (MoonrakerMachine)machine;
    if (string.IsNullOrWhiteSpace(Machine.IpAddress))
    {
      throw new OverseerException("invalid_request");
    }
    Machine.Url = new UriBuilder(Machine.IpAddress)
    {
      Path = "/",
      Scheme = "http",
      Port = 80,
    }.Uri.ToString();

    Machine.WebSocketUri = new UriBuilder(Machine.Url) { Scheme = "ws", Path = "websocket" }.Uri;

    // Get printer object list
    var tools = new List<MachineTool>();
    var client = httpClientFactory.CreateClient();
    var objectsUriBuilder = new UriBuilder(Machine.Url) { Path = "printer/objects/list" };
    var printerObjectList = await client.GetFromJsonAsync<PrinterObjectListResponse>(objectsUriBuilder.Uri);
    var extruderObjects = printerObjectList?.Result?.Objects.Where(x => x.StartsWith("extruder"));
    if (extruderObjects != null)
    {
      foreach (var x in extruderObjects)
      {
        var index = x == "extruder" ? 0 : int.Parse(x.Replace("extruder", string.Empty));
        tools.Add(new MachineTool(MachineToolType.Extruder, index));
      }
    }

    var heatersUriBuilder = new UriBuilder(Machine.Url) { Path = "printer/objects/query", Query = "heaters" };
    var heaters = await client.GetFromJsonAsync<HeatersResponse>(heatersUriBuilder.Uri);
    var availableHeaters = heaters?.Result?.Status?.Heaters?.AvailableHeaters;
    if (availableHeaters is not null)
    {
      foreach (var heater in availableHeaters)
      {
        var index = availableHeaters.IndexOf(heater);
        if (heater == "heater_bed")
        {
          tools.Add(new MachineTool(MachineToolType.Heater, index, "bed"));
        }
        else
        {
          tools.Add(new MachineTool(MachineToolType.Heater, index));
        }
      }
    }

    if (tools.Count != 0)
    {
      Machine.Tools = tools;
    }

    // get camera list
    var camerasUriBuilder = new UriBuilder(Machine.Url) { Path = "server/webcams/list" };
    var cameras = await client.GetFromJsonAsync<CameraListResponse>(camerasUriBuilder.Uri);
    if (cameras?.Result?.Webcams?.Count > 0)
    {
      // if this is an update, and a webcam in the list is already selected, keep it
      if (!cameras.Result.Webcams.Any(x => Machine.WebCamUrl?.EndsWith(x.StreamUrl ?? string.Empty) == true))
      {
        var firstCamera = cameras.Result.Webcams.First();
        machine.WebCamUrl = new Uri(new Uri(Machine.Url), firstCamera.StreamUrl).ToString();
        machine.WebCamOrientation = WebCamOrientation.Default;
      }

      // always update the list of available cameras.
      // multiple cameras are supported, if the first one isn't the main camera, allow the user to select another one
      Machine.AvailableWebCams = cameras.Result.Webcams.ToDictionary(x => x.Name!, x => new Uri(new Uri(Machine.Url), x.StreamUrl).ToString());
    }
  }

  protected override async Task OnConnected()
  {
    await SendMessage(
      "server.connection.identify",
      new
      {
        client_name = "Overseer",
        version = GetType().Assembly.GetName().Version?.ToString() ?? "2.0.0",
        url = "https://github.com/overseerapp/overseer",
        type = "other",
      }
    );
    await SendMessage(
      "printer.objects.subscribe",
      new
      {
        objects = new
        {
          print_stats = (object?)null,
          pause_resume = (object?)null,
          display_status = (object?)null,
          heaters = (object?)null,
          heater_bed = (object?)null,
          system_stats = (object?)null,
          toolhead = (object?)null,
          extruder = (object?)null,
        },
      }
    );
  }

  private async Task SendMessage(string method, object @params)
  {
    if (_webSocket == null)
      return;
    var message = new
    {
      jsonrpc = "2.0",
      id = ++_messageId,
      method,
      @params,
    };
    var json = JsonSerializer.Serialize(message);
    var buffer = Encoding.UTF8.GetBytes(json);
    await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cancellationTokenSource?.Token ?? default);
  }

  protected override async Task HandleIncomingMessage(string messageText)
  {
    if (string.IsNullOrWhiteSpace(messageText))
      return;

    var message = JObject.Parse(messageText);
    if (message == null)
      return;

    if (message["result"]?["connection_id"] != null)
    {
      _connectionId = message["result"]!["connection_id"]!.Value<long>();
    }
    else
    {
      if (message["result"]?["status"] != null)
      {
        await ProcessStatusUpdate((JObject)message["result"]!["status"]!);
      }
      else if (message["params"] is JArray arr && arr.Count > 0)
      {
        await ProcessStatusUpdate((JObject)arr[0]);
      }
    }
  }

  private async Task ProcessStatusUpdate(JObject update)
  {
    MachineState? nextState = null;
    var printStats = update["print_stats"];
    if (printStats != null && printStats["state"] != null)
    {
      var stateStr = printStats["state"]!.ToString();
      nextState = stateStr switch
      {
        "printing" => MachineState.Operational,
        "paused" => MachineState.Paused,
        _ => MachineState.Idle,
      };
    }

    var nextStatus = new MachineStatus { MachineId = Machine.Id, State = nextState ?? _lastStatus?.State ?? MachineState.Idle };

    var bedTool = Machine.Tools.FirstOrDefault(x => x.ToolType == MachineToolType.Heater && x.Name == "bed");
    var currentBedTemp = update["heater_bed"];
    if (bedTool is not null && currentBedTemp != null)
    {
      var lastBedTemp = _lastStatus?.Temperatures.GetValueOrDefault(bedTool.Index);
      nextStatus.Temperatures[bedTool.Index] = new TemperatureStatus
      {
        HeaterIndex = bedTool.Index,
        Actual = currentBedTemp["temperature"]?.Value<float>() ?? lastBedTemp?.Actual ?? 0,
        Target = currentBedTemp["target"]?.Value<float>() ?? lastBedTemp?.Target ?? 0,
      };
    }
    else if (bedTool is not null && _lastStatus?.Temperatures.TryGetValue(bedTool.Index, out var temperature) == true)
    {
      nextStatus.Temperatures[bedTool.Index] = temperature;
    }
    else
    {
      return;
    }

    var heaterTools = Machine.Tools.Where(x => x.ToolType == MachineToolType.Heater && x.Name != "bed").ToList();
    for (int i = 0; i < heaterTools.Count; i++)
    {
      var heaterTool = heaterTools.ElementAt(i);
      var suffix = i == 0 ? string.Empty : i.ToString();
      var extruderKey = $"extruder{suffix}";
      var currentExtruderTemp = update[extruderKey];
      if (currentExtruderTemp != null)
      {
        var lastExtruderTemp = _lastStatus?.Temperatures.GetValueOrDefault(heaterTool.Index);
        nextStatus.Temperatures[heaterTool.Index] = new TemperatureStatus
        {
          HeaterIndex = heaterTool.Index,
          Actual = currentExtruderTemp["temperature"]?.Value<float>() ?? lastExtruderTemp?.Actual ?? 0,
          Target = currentExtruderTemp["target"]?.Value<float>() ?? lastExtruderTemp?.Target ?? 0,
        };
      }
      else if (_lastStatus?.Temperatures.TryGetValue(heaterTool.Index, out var temperature2) == true)
      {
        nextStatus.Temperatures[heaterTool.Index] = temperature2;
      }
      else
      {
        return;
      }
    }

    if (nextStatus.State != MachineState.Idle)
    {
      nextStatus.ElapsedJobTime = printStats?["total_duration"]?.Value<int>() ?? _lastStatus?.ElapsedJobTime ?? 0;

      if (update["display_status"]?["progress"] != null)
      {
        nextStatus.Progress = update["display_status"]!["progress"]!.Value<float>() * 100;
      }
      else
      {
        nextStatus.Progress = _lastStatus?.Progress ?? 0;
      }

      if (nextStatus.Progress > 0)
      {
        nextStatus.EstimatedTimeRemaining = (int)((nextStatus.ElapsedJobTime / (nextStatus.Progress / 100.0f)) - nextStatus.ElapsedJobTime);
      }
      else
      {
        nextStatus.EstimatedTimeRemaining = _lastStatus?.EstimatedTimeRemaining ?? 0;
      }
    }

    lock (_statusLock)
    {
      if (_lastStatus is not null && _lastStatus.Equals(nextStatus))
        return;

      _lastStatus = nextStatus;
      _lastStatusUpdate = DateTime.UtcNow;
    }

    await MachineStatusChannel.WriteAsync(nextStatus, _cancellationTokenSource?.Token ?? default);
  }
}
