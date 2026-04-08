using System.IO.Pipes;
using TgPcAgent.Core.Interop;

namespace TgPcAgent.App.Services;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\TgPcAgent.Singleton.v1";
    private const string PipeName = "TgPcAgent.Singleton.Pipe.v1";

    private Mutex? _mutex;
    private CancellationTokenSource? _serverCts;
    private Task? _serverTask;

    public bool TryAcquirePrimaryOwnership()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return false;
        }

        _mutex = mutex;
        return true;
    }

    public bool WaitForPrimaryOwnership(TimeSpan timeout)
    {
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < timeout)
        {
            if (TryAcquirePrimaryOwnership())
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    public void StartServer(Func<InstanceControlCommand, Task> handler, FileLogger logger)
    {
        if (_serverTask is not null)
        {
            return;
        }

        _serverCts = new CancellationTokenSource();
        _serverTask = Task.Run(() => RunServerLoopAsync(handler, logger, _serverCts.Token));
    }

    public async Task<bool> TrySendCommandAsync(InstanceControlCommand command, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);

        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync((int)timeout.TotalMilliseconds, linkedCts.Token);

            await using var writer = new StreamWriter(client)
            {
                AutoFlush = true
            };
            await writer.WriteLineAsync(InstanceControlMessageCodec.Encode(command));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_serverCts is not null)
        {
            _serverCts.Cancel();
            try
            {
                _serverTask?.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _serverCts.Dispose();
                _serverCts = null;
                _serverTask = null;
            }
        }

        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
    }

    private static async Task RunServerLoopAsync(
        Func<InstanceControlCommand, Task> handler,
        FileLogger logger,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(server);
                var payload = await reader.ReadLineAsync(cancellationToken);
                if (!InstanceControlMessageCodec.TryDecode(payload, out var command))
                {
                    logger.Info($"Ignored unknown instance-control payload '{payload}'.");
                    continue;
                }

                await handler(command);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.Error("Single-instance server loop failed.", exception);
                await Task.Delay(500, cancellationToken);
            }
        }
    }
}
