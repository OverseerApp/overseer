using System.Buffers;
using System.Text;
using System.Text.Json;
using MQTTnet;
using Overseer.Server.Channels;
using Overseer.Server.Machines.BambuLabs.Models;
using Overseer.Server.Models;
using Timer = System.Timers.Timer;

namespace Overseer.Server.Machines.BambuLabs;

public class BambuMachineProvider(BambuMachine machine, IMachineStatusChannel machineStatusChannel) : MachineProvider<BambuMachine>
{
  private const string MqttUsername = "bblp";
  private const int MqttPort = 8883;
  private const string DefaultSequenceId = "0";
  private const string CertificateIssuerName = "BBL Technologies Co., Ltd";
  private const int MinutesToSeconds = 60;
  private const int BedHeaterIndex = 0;
  private const int NozzleHeaterIndex = 1;
  private const int ExtruderIndex = 0;
  private const string BedToolName = "bed";

  private IMqttClient? _mqttClient;
  Timer? _timer;
  DateTime? _lastStatusUpdate;

  private readonly Lock _lockObject = new();
  private volatile bool _isConnecting = false;
  private int _connectionAttempts = 0;
  private readonly int _maxConnectionAttempts = 5;
  private DateTime? _lastConnectionAttempt;
  private readonly TimeSpan _backoffResetTime = TimeSpan.FromMinutes(5);

  public override BambuMachine Machine
  {
    get => machine;
    protected set => machine = value;
  }

  private async Task SendPrintCommand(object command)
  {
    IMqttClient? client = null;
    lock (_lockObject)
    {
      client = _mqttClient;
    }

    if (client == null || !client.IsConnected)
      return;

    try
    {
      await client.PublishAsync(
        new MqttApplicationMessageBuilder()
          .WithTopic($"device/{Machine.Serial}/request")
          .WithPayload(JsonSerializer.SerializeToUtf8Bytes(new { print = command }))
          .Build()
      );
    }
    catch (Exception ex)
    {
      Log.Warn("Failed to send print command", ex);
    }
  }

  public override async Task ExecuteGcode(string command)
  {
    await SendPrintCommand(
      new
      {
        sequence_id = DefaultSequenceId,
        command = "gcode_line",
        param = command,
      }
    );
  }

  public override async Task ResumeJob()
  {
    await SendPrintCommand(new { sequence_id = DefaultSequenceId, command = "resume" });
  }

  public override async Task PauseJob()
  {
    await SendPrintCommand(new { sequence_id = DefaultSequenceId, command = "pause" });
  }

  public override async Task CancelJob()
  {
    await SendPrintCommand(new { sequence_id = DefaultSequenceId, command = "stop" });
  }

  public override Task LoadConfiguration(Machine machine)
  {
    Machine = (BambuMachine)machine;
    // this url seems to be x1 series only, and it's not accessible from a browser
    // and will require some kind of proxy to access it. So this will need to be provided
    // by the user.
    // Machine.WebCamUrl = $"rtsps://{Machine.Url}:322/streaming/live/1";

    // not supporting ams in overseer
    // ***all bambu printers have a single bed, hot end, and extruder ***
    // the previous comment is no longer true, but I don't have any other bambu printers to test with
    // and don't plan on getting any others, so this will have to do for now.
    Machine.Tools =
    [
      new(MachineToolType.Heater, BedHeaterIndex, BedToolName),
      new(MachineToolType.Heater, NozzleHeaterIndex),
      new(MachineToolType.Extruder, ExtruderIndex),
    ];

    return Task.CompletedTask;
  }

  public override void Start(int interval)
  {
    // Reset connection attempts when starting
    _connectionAttempts = 0;

    Task.Run(Connect);

    _timer?.Dispose();
    _timer = new(interval);
    _timer.Elapsed += async (sender, args) =>
    {
      bool shouldReconnect = false;
      lock (_lockObject)
      {
        shouldReconnect = _mqttClient == null || !_mqttClient.IsConnected;
      }

      if (shouldReconnect && !_isConnecting)
      {
        await DisconnectAndCleanup();
        await ConnectWithBackoff();
        return;
      }

      // since it's mqtt updates are pushed, but if we haven't received an update in the last interval, request one
      if (_lastStatusUpdate.HasValue && DateTime.UtcNow.Subtract(_lastStatusUpdate.Value).TotalSeconds < interval)
        return;

      lock (_lockObject)
      {
        if (_mqttClient != null && _mqttClient.IsConnected)
        {
          Task.Run(async () =>
          {
            try
            {
              await _mqttClient.PublishAsync(
                new MqttApplicationMessageBuilder()
                  .WithTopic($"device/{Machine.Serial}/request")
                  .WithPayload(JsonSerializer.SerializeToUtf8Bytes(new { pushing = new { command = "pushall" } }))
                  .Build()
              );
            }
            catch (Exception ex)
            {
              Log.Warn("Failed to send pushall command", ex);
            }
          });
        }
      }
    };
    _timer.Start();
  }

  async Task Connect()
  {
    lock (_lockObject)
    {
      if (_mqttClient != null || _isConnecting)
        return;
      _isConnecting = true;
      _lastConnectionAttempt = DateTime.UtcNow;
    }

    IMqttClient? newClient = null;
    try
    {
      var mqttFactory = new MqttClientFactory();
      newClient = mqttFactory.CreateMqttClient();
      var mqttClientOptions = new MqttClientOptionsBuilder()
        .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311)
        .WithCredentials(MqttUsername, Machine.AccessCode)
        .WithTlsOptions(
          (options) => options.UseTls().WithCertificateValidationHandler((args) => args.Certificate.Issuer.Contains("BBL Technologies Co., Ltd"))
        )
        .WithTcpServer(Machine.Url, MqttPort)
        .WithCleanSession()
        .Build();

      newClient.ApplicationMessageReceivedAsync += OnMessage;
      newClient.DisconnectedAsync += OnDisconnected;

      var result = await newClient.ConnectAsync(mqttClientOptions);
      if (result.ResultCode != MqttClientConnectResultCode.Success)
      {
        Log.Error($"Failed to connect to MQTT server: {result.ResultCode}");
        newClient?.Dispose();
        return;
      }

      await newClient.SubscribeAsync($"device/{Machine.Serial}/report");

      lock (_lockObject)
      {
        _mqttClient = newClient;
        _connectionAttempts = 0; // Reset on successful connection
      }

      Log.Info("Successfully connected to Bambu Labs printer");
    }
    catch (Exception ex)
    {
      Log.Error("Error connecting to Bambu Labs printer", ex);
      newClient?.Dispose();
    }
    finally
    {
      lock (_lockObject)
      {
        _isConnecting = false;
      }
    }
  }

  async Task ConnectWithBackoff()
  {
    // Reset connection attempts if enough time has passed since last attempt
    if (_lastConnectionAttempt.HasValue && DateTime.UtcNow - _lastConnectionAttempt.Value > _backoffResetTime)
    {
      Log.Info("Resetting connection attempts after backoff period");
      _connectionAttempts = 0;
    }

    if (_connectionAttempts >= _maxConnectionAttempts)
    {
      Log.Warn($"Max connection attempts ({_maxConnectionAttempts}) reached. Waiting before retry.");
      await Task.Delay(TimeSpan.FromMinutes(2));
      _connectionAttempts = 0; // Reset after long wait
      await Connect();
      return;
    }

    var delay = Math.Min(1000 * Math.Pow(2, _connectionAttempts), 30000); // Exponential backoff, max 30 seconds
    if (_connectionAttempts > 0)
    {
      Log.Info($"Waiting {delay}ms before connection attempt {_connectionAttempts + 1}");
      await Task.Delay(TimeSpan.FromMilliseconds(delay));
    }

    _connectionAttempts++;
    await Connect();
  }

  async Task OnDisconnected(MqttClientDisconnectedEventArgs args)
  {
    Log.Warn($"MQTT client disconnected: {args.Reason}");
    // The timer will handle reconnection
  }

  async Task OnMessage(MqttApplicationMessageReceivedEventArgs args)
  {
    try
    {
      var payload = args.ApplicationMessage.Payload;
      var json = Encoding.UTF8.GetString(payload.ToArray()).TrimEnd('\0');

      var message = JsonSerializer.Deserialize<Message>(json);
      if (message?.Print?.GCodeState == null)
      {
        return;
      }

      var status = new MachineStatus
      {
        MachineId = Machine.Id,
        State = message.Print.GCodeState switch
        {
          "RUNNING" => MachineState.Operational,
          "PREPARING" => MachineState.Operational,
          "PAUSE" => MachineState.Paused,
          "FINISH" => MachineState.Idle,
          "FAILED" => MachineState.Idle, // Consider adding error state if available
          _ => MachineState.Idle,
        },
      };

      if (status.State != MachineState.Idle)
      {
        status.Progress = message.Print.Progress;
        status.EstimatedTimeRemaining = message.Print.RemainingTime * MinutesToSeconds;
        // only the remaining time is available, so we calculate the elapsed time
        var total = (int)(status.EstimatedTimeRemaining / (1 - status.Progress / 100));
        status.ElapsedJobTime = total - status.EstimatedTimeRemaining;
      }

      status.Temperatures = new()
      {
        {
          BedHeaterIndex,
          new TemperatureStatus
          {
            HeaterIndex = BedHeaterIndex,
            Actual = message.Print.BedTemperature,
            Target = message.Print.BedTargetTemperature,
          }
        },
        {
          NozzleHeaterIndex,
          new TemperatureStatus
          {
            HeaterIndex = NozzleHeaterIndex,
            Actual = message.Print.NozzleTemperature,
            Target = message.Print.NozzleTargetTemperature,
          }
        },
      };

      await machineStatusChannel.WriteAsync(status);
      _lastStatusUpdate = DateTime.UtcNow;
    }
    catch (JsonException ex)
    {
      Log.Warn("Failed to deserialize MQTT message", ex);
    }
    catch (Exception ex)
    {
      Log.Error("Error processing MQTT message", ex);
    }
  }

  async Task DisconnectAndCleanup()
  {
    IMqttClient? clientToDispose = null;
    lock (_lockObject)
    {
      clientToDispose = _mqttClient;
      _mqttClient = null;
    }

    if (clientToDispose != null)
    {
      try
      {
        if (clientToDispose.IsConnected)
        {
          await clientToDispose.DisconnectAsync();
        }
      }
      catch (Exception ex)
      {
        Log.Warn("Error during MQTT disconnect", ex);
      }
      finally
      {
        try
        {
          clientToDispose.Dispose();
        }
        catch (Exception ex)
        {
          Log.Warn("Error disposing MQTT client", ex);
        }
      }
    }
  }

  public override void Stop()
  {
    _timer?.Stop();
    _timer?.Dispose();
    _timer = null;

    Task.Run(async () =>
    {
      await DisconnectAndCleanup();
    });
  }

  protected override void OnDisposing()
  {
    _timer?.Dispose();
    _mqttClient?.Dispose();
  }
}
