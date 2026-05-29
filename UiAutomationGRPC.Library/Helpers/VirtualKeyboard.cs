namespace UiAutomationGRPC.Library.Helpers;

/// <summary>
/// Provides virtual keyboard operations via gRPC.
/// Matches server-side VirtualKeyboard naming conventions.
/// </summary>
public sealed class VirtualKeyboard
{
    private readonly UiAutomationDriver _driver;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualKeyboard"/> class.
    /// </summary>
    /// <param name="driver">The UI Automation driver.</param>
    public VirtualKeyboard(UiAutomationDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    /// <summary>
    /// Sends keys and waits for them to be processed.
    /// </summary>
    public async Task<(bool Success, string Message)> SendWaitAsync(string keys)
        => await _driver.SendKeysAsync(keys, wait: true);

    /// <summary>
    /// Sends keys without waiting for them to be processed.
    /// </summary>
    public async Task<(bool Success, string Message)> SendAsync(string keys)
        => await _driver.SendKeysAsync(keys, wait: false);

    /// <summary>
    /// Sends keys with optional wait behavior.
    /// </summary>
    public async Task<(bool Success, string Message)> SendAsync(string keys, bool wait)
        => await _driver.SendKeysAsync(keys, wait);

    /// <summary>
    /// Focuses the element identified by <paramref name="runtimeId"/> and then sends keys to it,
    /// waiting for processing. Use this to direct input at a specific control rather than whatever
    /// currently holds focus.
    /// </summary>
    public async Task<(bool Success, string Message)> SendToElementAsync(string runtimeId, string keys, bool wait = true)
        => await _driver.SendKeysAsync(keys, wait, runtimeId);

    /// <summary>
    /// Sends a key with a delay for keyboard readiness.
    /// </summary>
    public async Task<(bool Success, string Message)> SendKeyAsync(string buttonKey)
    {
        await Task.Delay(UsabilityTimeLimits.KeyboardReadiness);
        return await SendWaitAsync(buttonKey);
    }

    /// <summary>
    /// Sends a key multiple times with delays between each press.
    /// </summary>
    public async Task<(bool Success, string Message)> SendKeyAsync(string buttonKey, int count)
    {
        (bool Success, string Message) result = (true, "OK");

        for (var i = 0; i < count; i++)
        {
            result = await SendKeyAsync(buttonKey);
            if (!result.Success)
                return result;
        }

        return result;
    }
}
