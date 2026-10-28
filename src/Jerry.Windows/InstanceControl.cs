using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;

namespace Jerry.Windows;

public static class InstanceControl
{
    public const string InstanceMutexName = @"Local\Jerry.CursorStudio";
    public const string ShowEventName = @"Local\Jerry.CursorStudio.Show";
    public const string MaintenanceMutexName = @"Local\Jerry.CursorStudio.Maintenance";
    internal static string PipeName => "Jerry.CursorStudio.Control." + WindowsIdentity.GetCurrent().User!.Value + "." + Process.GetCurrentProcess().SessionId;

    public static MutexLease? TryClaimInstance() => MutexLease.TryAcquire(InstanceMutexName);
    public static MutexLease? TryClaimMaintenance() => MutexLease.TryAcquire(MaintenanceMutexName);

    public static bool IsRunning()
    {
        using var lease = TryClaimInstance();
        return lease == null;
    }

    /// <returns>0 after exit/no instance, 10 for active work, 11 for a legacy/unreachable instance, 12 for timeout/error.</returns>
    public static async Task<int> PrepareUpdateAsync()
    {
        if (!IsRunning()) return 0;
        Process? process = null;
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await pipe.ConnectAsync(2000, deadline.Token);
            var pidBytes = new byte[4];
            await pipe.ReadExactlyAsync(pidBytes, deadline.Token);
            process = Process.GetProcessById(BitConverter.ToInt32(pidBytes));
            await pipe.WriteAsync(new byte[] { 1 }, deadline.Token);
            var response = new byte[1];
            await pipe.ReadExactlyAsync(response, deadline.Token);
            if (response[0] != 0) { process.Dispose(); return 10; }
        }
        catch (Exception error) when (error is IOException or TimeoutException or OperationCanceledException or ArgumentException)
        {
            // Shutdown can dispose the server before its last reply reaches us.
            if (process == null) return IsRunning() ? 11 : 0;
        }
        try
        {
            if (process != null)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await process.WaitForExitAsync(deadline.Token);
            }
            return IsRunning() ? 12 : 0;
        }
        catch (OperationCanceledException) { return 12; }
        finally { process?.Dispose(); }
    }
}

public sealed class MutexLease : IDisposable
{
    private readonly Mutex mutex;
    private MutexLease(Mutex mutex) => this.mutex = mutex;
    public static MutexLease? TryAcquire(string name)
    {
        var mutex = new Mutex(false, name);
        try
        {
            try { if (mutex.WaitOne(0)) return new(mutex); }
            catch (AbandonedMutexException) { return new(mutex); }
            mutex.Dispose(); return null;
        }
        catch { mutex.Dispose(); throw; }
    }
    public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
}

public sealed class InstanceControlServer : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly Task completion;
    private readonly Func<Task<bool>> requestExit;
    public InstanceControlServer(Func<Task<bool>> requestExit)
    {
        this.requestExit = requestExit;
        completion = Task.Run(ListenAsync);
    }
    private async Task ListenAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(InstanceControl.PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                await pipe.WriteAsync(BitConverter.GetBytes(Environment.ProcessId), deadline.Token);
                var request = new byte[1];
                await pipe.ReadExactlyAsync(request, deadline.Token);
                var accepted = request[0] == 1 && await requestExit();
                await pipe.WriteAsync(new byte[] { accepted ? (byte)0 : (byte)10 }, deadline.Token);
            }
            catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException)
            {
                if (stop.IsCancellationRequested) break;
            }
        }
    }
    public void Dispose()
    {
        stop.Cancel();
        _ = completion.ContinueWith(_ => stop.Dispose(), TaskScheduler.Default);
    }
}
