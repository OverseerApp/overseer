using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Overseer.Server.Channels;
using Overseer.Server.Machines.Elegoo.Models;
using Overseer.Server.Models;

namespace Overseer.Server.Machines.Elegoo;

public class ElegooMachineProvider(ElegooMachine machine, IMachineStatusChannel machineStatusChannel)
  : WebSocketMachineProvider<ElegooMachine>(machineStatusChannel)
{
  const int PING_COMMAND = 0;
  const int PAUSE_COMMAND = 129;
  const int RESUME_COMMAND = 131;
  const int CANCEL_COMMAND = 130;
  const int PRINTER_COMMANDS = 403;
  const int ENABLE_CAMERA_COMMAND = 386;

  public override ElegooMachine Machine { get; protected set; } = machine;

  public override Task SetToolTemperature(int heaterIndex, int targetTemperature)
  {
    return SendCommand(new { TempTargetNozzle = targetTemperature });
  }

  public override Task SetBedTemperature(int targetTemperature)
  {
    return SendCommand(new { TempTargetHotbed = targetTemperature });
  }

  public override Task ExecuteGcode(string command)
  {
    // may not be supported
    return Task.CompletedTask;
  }

  public override async Task ResumeJob()
  {
    await SendCommand(RESUME_COMMAND);
  }

  public override async Task PauseJob()
  {
    await SendCommand(PAUSE_COMMAND);
  }

  public override async Task CancelJob()
  {
    await SendCommand(CANCEL_COMMAND);
  }

  public override Task LoadConfiguration(Machine machine)
  {
    Machine = (ElegooMachine)machine;
    if (string.IsNullOrWhiteSpace(Machine.IpAddress))
    {
      throw new OverseerException("invalid_request");
    }

    Machine.Url = new UriBuilder(Machine.IpAddress) { Path = "/", Scheme = "http" }.Uri.ToString();

    // fixed camera and orientation
    Machine.WebCamUrl = new UriBuilder(Machine.IpAddress)
    {
      Port = 3031,
      Path = "/video",
      Scheme = "http",
    }.Uri.ToString();

    Machine.WebCamOrientation = WebCamOrientation.Default;

    // one bed, one hot end, and one extruder
    // Currently only supports the Centauri machine, but may work with other single extruder Elegoo machines
    Machine.Tools = [new(MachineToolType.Heater, 0, "bed"), new(MachineToolType.Heater, 1), new(MachineToolType.Extruder, 0)];

    // Set WebSocketUri for the base class
    Machine.WebSocketUri = new UriBuilder(Machine.IpAddress)
    {
      Port = 3030,
      Path = "/websocket",
      Scheme = "ws",
    }.Uri;

    return Task.CompletedTask;
  }

  protected override async Task OnConnected()
  {
    await SendCommand(PING_COMMAND);
    await SendCommand(ENABLE_CAMERA_COMMAND, new { Enable = 1 });
  }

  protected override async Task HandleIncomingMessage(string messageText)
  {
    if (string.IsNullOrWhiteSpace(messageText))
      return;

    Message? message = null;
    try
    {
      message = JsonSerializer.Deserialize<Message>(messageText);
    }
    catch (Exception ex)
    {
      // Log the error and continue
      Log.Error($"Failed to deserialize message: {messageText}", ex);
    }

    if (message != null)
    {
      if (message.Status == null)
      {
        // this is a command response, not a status update
        return;
      }

      var status = new MachineStatus
      {
        MachineId = Machine.Id,
        State = message.Status?.PrintInfo?.Status switch
        {
          (int)ElegooMachineState.Printing => MachineState.Operational,
          (int)ElegooMachineState.Resuming => MachineState.Operational,
          (int)ElegooMachineState.Paused => MachineState.Paused,
          (int)ElegooMachineState.Pausing => MachineState.Paused,
          _ => MachineState.Idle,
        },
        Temperatures = new()
        {
          {
            0,
            new TemperatureStatus
            {
              HeaterIndex = 0,
              Actual = message.Status?.TempOfHotbed ?? 0,
              Target = message.Status?.TempTargetHotbed ?? 0,
            }
          },
          {
            1,
            new TemperatureStatus
            {
              HeaterIndex = 1,
              Actual = message.Status?.TempOfNozzle ?? 0,
              Target = message.Status?.TempTargetNozzle ?? 0,
            }
          },
        },
      };

      if (status.State != MachineState.Idle)
      {
        // if it's not idle, we can get the progress and estimated time remaining
        status.Progress = message.Status?.PrintInfo?.Progress ?? 0;
        var totalTicks = message.Status?.PrintInfo?.TotalTicks ?? 0;
        var currentTicks = message.Status?.PrintInfo?.CurrentTicks ?? 0;
        status.EstimatedTimeRemaining = (int)(totalTicks - currentTicks);
        status.ElapsedJobTime = (int)(message.Status?.PrintInfo?.CurrentTicks ?? 0);
      }

      lock (_statusLock)
      {
        if (_lastStatus is not null && _lastStatus.Equals(status))
          return;

        _lastStatusUpdate = DateTime.UtcNow;
        _lastStatus = status;
      }

      await MachineStatusChannel.WriteAsync(status, _cancellationTokenSource?.Token ?? default);
    }
  }

  Task SendCommand(object data)
  {
    return SendCommand(PRINTER_COMMANDS, data);
  }

  Task SendCommand(int command)
  {
    var message = new
    {
      Id = string.Empty,
      Data = new
      {
        Cmd = command,
        Data = new object[] { },
        RequestID = Guid.NewGuid().ToString("N"),
        MainboardID = string.Empty,
        TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        From = 1,
      },
    };
    return SendMessage(message);
  }

  Task SendCommand(int command, object data)
  {
    var message = new
    {
      Id = string.Empty,
      Data = new
      {
        Cmd = command,
        Data = data,
        RequestID = Guid.NewGuid().ToString("N"),
        MainboardID = string.Empty,
        TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        From = 1,
      },
    };
    return SendMessage(message);
  }

  async Task SendMessage(object message)
  {
    if (_webSocket == null)
      return;

    if (_cancellationTokenSource is null || _cancellationTokenSource.IsCancellationRequested)
    {
      return;
    }

    try
    {
      var messageText = JsonSerializer.Serialize(message);
      var buffer = Encoding.UTF8.GetBytes(messageText);
      await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
    }
    catch (Exception ex)
    {
      Log.Error($"Failed to send message: {message}", ex);
    }
  }
}
