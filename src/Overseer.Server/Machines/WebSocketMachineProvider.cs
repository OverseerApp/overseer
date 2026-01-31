using System.Net.WebSockets;
using System.Text;
using Overseer.Server.Channels;
using Overseer.Server.Models;
using Timer = System.Timers.Timer;

namespace Overseer.Server.Machines
{
  public abstract class WebSocketMachineProvider<TMachine>(IMachineStatusChannel machineStatusChannel) : MachineProvider<TMachine>
    where TMachine : Machine, IWebSocketMachine, new()
  {
    protected Timer? _timer;
    protected ClientWebSocket? _webSocket;
    protected CancellationTokenSource? _cancellationTokenSource;
    protected DateTime? _lastStatusUpdate;
    protected MachineStatus? _lastStatus = null;
    protected object _statusLock = new();

    protected IMachineStatusChannel MachineStatusChannel => machineStatusChannel;

    public override void Start(int interval)
    {
      Task.Run(Connect, _cancellationTokenSource?.Token ?? default);

      _timer?.Dispose();
      _timer = new(interval);
      _timer.Elapsed += async (sender, args) =>
      {
        bool shouldSend = false;
        MachineStatus? statusToSend = null;
        lock (_statusLock)
        {
          if (_lastStatus != null && _lastStatusUpdate.HasValue && DateTime.UtcNow.Subtract(_lastStatusUpdate.Value).TotalMilliseconds <= interval)
          {
            shouldSend = true;
            statusToSend = _lastStatus;
          }
        }

        if (shouldSend && statusToSend != null)
        {
          await machineStatusChannel.WriteAsync(statusToSend, _cancellationTokenSource?.Token ?? default);
        }

        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
          _webSocket?.Dispose();
          _webSocket = null;
          await Connect();
        }
      };
      _timer.Start();
    }

    public override void Stop()
    {
      _timer?.Stop();
      _timer?.Dispose();
      _timer = null;
      _cancellationTokenSource?.Cancel();
    }

    protected virtual Task<Uri> GetWebSocketUri()
    {
      if (Machine.WebSocketUri is null)
        throw new InvalidOperationException("WebSocket URI is not set for the machine.");

      return Task.FromResult(Machine.WebSocketUri);
    }

    protected virtual async Task Connect()
    {
      if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        return;

      _cancellationTokenSource?.Cancel();
      _cancellationTokenSource = new CancellationTokenSource();

      _webSocket = new ClientWebSocket();
      try
      {
        var webSocketUri = await GetWebSocketUri();
        await _webSocket.ConnectAsync(webSocketUri, _cancellationTokenSource.Token);
        await OnConnected();
        await ReceiveLoop();
      }
      catch (Exception ex)
      {
        _webSocket?.Dispose();
        _webSocket = null;
        Log.Error("WebSocket connection failed", ex);
      }
    }

    protected virtual Task OnConnected()
    {
      // Override this method to handle actions after the WebSocket connection is established.
      return Task.CompletedTask;
    }

    protected abstract Task HandleIncomingMessage(string messageText);

    private async Task ReceiveLoop()
    {
      while (_cancellationTokenSource?.IsCancellationRequested == false)
      {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
          break;

        var buffer = new byte[4096];
        using var ms = new MemoryStream();
        WebSocketReceiveResult receiveResult;
        do
        {
          receiveResult = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
          if (receiveResult.MessageType == WebSocketMessageType.Close)
            break;
          ms.Write(buffer, 0, receiveResult.Count);
        } while (!receiveResult.EndOfMessage);

        var messageText = Encoding.UTF8.GetString(ms.ToArray());
        await HandleIncomingMessage(messageText);
      }
    }

    protected override void OnDisposing()
    {
      _timer?.Dispose();
      _webSocket?.Dispose();
      _cancellationTokenSource?.Cancel();
      _cancellationTokenSource?.Dispose();
    }
  }
}
