using System.Collections.Concurrent;

namespace Overseer.Server.Services;

public interface IRateLimitingService
{
  bool IsRateLimited(string key);
  void RecordAttempt(string key);
  void Reset(string key);
}

public class RateLimitingService : IRateLimitingService
{
  private readonly ConcurrentDictionary<string, RateLimitEntry> _attempts = new();
  private readonly int _maxAttempts;
  private readonly TimeSpan _windowDuration;
  private readonly TimeSpan _lockoutDuration;

  public RateLimitingService(int maxAttempts = 5, int windowSeconds = 60, int lockoutSeconds = 300)
  {
    _maxAttempts = maxAttempts;
    _windowDuration = TimeSpan.FromSeconds(windowSeconds);
    _lockoutDuration = TimeSpan.FromSeconds(lockoutSeconds);
  }

  public bool IsRateLimited(string key)
  {
    if (!_attempts.TryGetValue(key, out var entry))
      return false;

    // Check if locked out
    if (entry.LockedUntil.HasValue && entry.LockedUntil.Value > DateTime.UtcNow)
      return true;

    // Clean up expired lockout
    if (entry.LockedUntil.HasValue && entry.LockedUntil.Value <= DateTime.UtcNow)
    {
      _attempts.TryRemove(key, out _);
      return false;
    }

    return false;
  }

  public void RecordAttempt(string key)
  {
    var now = DateTime.UtcNow;
    var entry = _attempts.GetOrAdd(key, _ => new RateLimitEntry());

    lock (entry)
    {
      // Remove old attempts outside the window
      while (entry.Attempts.Count > 0 && entry.Attempts.Peek() < now - _windowDuration)
      {
        entry.Attempts.Dequeue();
      }

      entry.Attempts.Enqueue(now);

      // Check if we need to lock out
      if (entry.Attempts.Count >= _maxAttempts)
      {
        entry.LockedUntil = now + _lockoutDuration;
        entry.Attempts.Clear();
      }
    }
  }

  public void Reset(string key)
  {
    _attempts.TryRemove(key, out _);
  }

  private class RateLimitEntry
  {
    public Queue<DateTime> Attempts { get; } = new();
    public DateTime? LockedUntil { get; set; }
  }
}
