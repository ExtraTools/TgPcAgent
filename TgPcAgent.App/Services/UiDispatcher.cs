namespace TgPcAgent.App.Services;

public sealed class UiDispatcher
{
    private readonly SynchronizationContext _synchronizationContext;

    public UiDispatcher(SynchronizationContext synchronizationContext)
    {
        _synchronizationContext = synchronizationContext;
    }

    public Task InvokeAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _synchronizationContext.Post(_ =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, null);

        return completion.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        _synchronizationContext.Post(_ =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, null);

        return completion.Task;
    }
}
