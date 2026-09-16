using System.Collections.Concurrent;

namespace MyGamingMap.API.Services;

public class RateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _clientRequests = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _usernameRequests = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _usernameLocks = new();

    // Within a 1 min rolling window, allow 6 requests per client and 1 request per username
    private readonly TimeSpan _window = TimeSpan.FromMinutes(1);
    private const int ClientLimit = 999;
    private const int UsernameLimit = 999;

    public bool TryAcquireClient(string clientKey)
    {
        return TryAcquire(
            _clientRequests,
            clientKey,
            ClientLimit
        );
    }

    public bool TryAcquireUsername(string username)
    {
        return TryAcquire(
            _usernameRequests,
            username,
            UsernameLimit
        );
    }

    private bool TryAcquire(
        ConcurrentDictionary<string, Queue<DateTime>> requests,
        string key,
        int limit)
    {
        var now = DateTime.UtcNow;

        var queue = requests.GetOrAdd(
            key,
            _ => new Queue<DateTime>()
        );

        lock (queue)
        {
            // Remove requests outside the one-minute window
            while (queue.Count > 0 &&
                   now - queue.Peek() >= _window)
            {
                queue.Dequeue();
            }

            // Rate limit reached
            if (queue.Count >= limit)
            {
                return false;
            }

            // Record this request
            queue.Enqueue(now);

            return true;
        }
    }

    public async Task<bool> TryAcquireUsernameLockAsync(string username)
    {
        var semaphore = _usernameLocks.GetOrAdd(
            username,
            _ => new SemaphoreSlim(1, 1)
        );

        return await semaphore.WaitAsync(0);
    }

    public void ReleaseUsernameLock(string username)
    {
        if (_usernameLocks.TryGetValue(username, out var semaphore))
        {
            semaphore.Release();
        }
    }
}