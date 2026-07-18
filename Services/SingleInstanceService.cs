namespace BabyToys.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\BabyToys.SingleInstance";
    private readonly Mutex _mutex = new(initiallyOwned: false, MutexName);
    private bool _ownsMutex;

    public bool TryAcquire()
    {
        if (_ownsMutex)
        {
            return true;
        }

        try
        {
            _ownsMutex = _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        return _ownsMutex;
    }

    public void Dispose()
    {
        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _mutex.Dispose();
    }
}
