using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Helpers;

namespace UiAutomationGRPC.Client.Calc.Pages;

public abstract class BasePageObject<T> where T : BasePageObject<T>
{
    public UiAutomationDriver _driver;
    protected VirtualKeyboard Keyboard { get; }
    protected VirtualMouse Mouse { get; }

    /// <summary>
    /// Internal pipeline of queued async actions.
    /// Action methods append to this chain and return <c>this</c> synchronously,
    /// enabling fluent chaining. The pipeline is flushed (awaited and reset)
    /// when a value-returning or page-transitioning method is called.
    /// </summary>
    private Task _pipeline = Task.CompletedTask;
    
    protected BasePageObject(UiAutomationDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        Keyboard = new VirtualKeyboard(driver);
        Mouse = new VirtualMouse(driver);
    }

    /// <summary>
    /// Appends an async action to the internal pipeline.
    /// The action will execute sequentially after all previously enqueued actions.
    /// </summary>
    protected void Enqueue(Func<Task> action)
    {
        var previous = _pipeline;
        _pipeline = InternalContinueAsync(previous, action);
    }

    private static async Task InternalContinueAsync(Task previous, Func<Task> next)
    {
        await previous.ConfigureAwait(false);
        await next().ConfigureAwait(false);
    }

    /// <summary>
    /// Drains the pipeline (awaits all enqueued actions), then executes the
    /// result-producing <paramref name="resultAction"/>.
    /// Use in every terminal method (value-returning or page-transitioning)
    /// to ensure queued actions complete before reading state.
    /// </summary>
    protected async Task<TResult> ResolveAsync<TResult>(Func<Task<TResult>> resultAction)
    {
        var pending = _pipeline;
        _pipeline = Task.CompletedTask;
        await pending.ConfigureAwait(false);
        return await resultAction().ConfigureAwait(false);
    }

}

