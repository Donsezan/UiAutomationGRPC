namespace UiAutomationGRPC.Library.Helpers;

/// <summary>
/// Provides virtual mouse operations via gRPC.
/// Matches server-side VirtualMouse naming conventions.
/// </summary>
public sealed class VirtualMouse
{
    private readonly UiAutomationDriver _driver;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualMouse"/> class.
    /// </summary>
    /// <param name="driver">The UI Automation driver.</param>
    public VirtualMouse(UiAutomationDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    #region Cursor Movement

    /// <summary>
    /// Moves the cursor by a relative delta.
    /// </summary>
    public async Task<(bool Success, string Message)> MoveAsync(int xDelta, int yDelta)
        => await _driver.PerformActionAsync("", UiAutomation.ActionType.Move, xDelta.ToString(), yDelta.ToString());

    /// <summary>
    /// Moves the cursor to an element's clickable point.
    /// </summary>
    public async Task<(bool Success, string Message)> MoveToAsync(string runtimeId)
        => await _driver.PerformActionAsync(runtimeId, UiAutomation.ActionType.MoveTo);

    #endregion

    #region Left Click Operations

    /// <summary>
    /// Performs a left click at the current cursor position.
    /// </summary>
    public async Task<(bool Success, string Message)> LeftClickAsync()
        => await _driver.PerformActionAsync("", UiAutomation.ActionType.LeftClick);

    /// <summary>
    /// Performs a left click on an element.
    /// </summary>
    public async Task<(bool Success, string Message)> LeftClickAsync(string runtimeId)
        => await _driver.PerformActionAsync(runtimeId, UiAutomation.ActionType.LeftClick);

    /// <summary>
    /// Presses the left mouse button down.
    /// </summary>
    public async Task<(bool Success, string Message)> LeftDownAsync()
        => await _driver.PerformActionAsync("", UiAutomation.ActionType.LeftDown);

    /// <summary>
    /// Releases the left mouse button.
    /// </summary>
    public async Task<(bool Success, string Message)> LeftUpAsync()
        => await _driver.PerformActionAsync("", UiAutomation.ActionType.LeftUp);

    #endregion

    #region Right Click Operations

    /// <summary>
    /// Performs a right click at the current cursor position.
    /// </summary>
    public async Task<(bool Success, string Message)> RightClickAsync()
        => await _driver.PerformActionAsync("", UiAutomation.ActionType.RightClick);

    /// <summary>
    /// Performs a right click on an element.
    /// </summary>
    public async Task<(bool Success, string Message)> RightClickAsync(string runtimeId)
        => await _driver.PerformActionAsync(runtimeId, UiAutomation.ActionType.RightClick);

    /// <summary>
    /// Presses the right mouse button down.
    /// </summary>
    public async Task<(bool Success, string Message)> RightDownAsync()
        => await _driver.PerformActionAsync("", UiAutomation.ActionType.RightDown);

    /// <summary>
    /// Releases the right mouse button.
    /// </summary>
    public async Task<(bool Success, string Message)> RightUpAsync()
        => await _driver.PerformActionAsync("", UiAutomation.ActionType.RightUp);

    #endregion

    #region Double Click and Middle Click

    /// <summary>
    /// Performs a double left click at the current cursor position.
    /// </summary>
    public async Task<(bool Success, string Message)> DoubleClickAsync()
        => await _driver.PerformActionAsync("", UiAutomation.ActionType.DoubleClick);

    /// <summary>
    /// Performs a double left click on an element.
    /// </summary>
    public async Task<(bool Success, string Message)> DoubleClickAsync(string runtimeId)
        => await _driver.PerformActionAsync(runtimeId, UiAutomation.ActionType.DoubleClick);

    /// <summary>
    /// Performs a middle mouse button click.
    /// </summary>
    public async Task<(bool Success, string Message)> MiddleClickAsync()
        => await _driver.PerformActionAsync("", UiAutomation.ActionType.MouseMiddleClick);

    #endregion

    #region Scroll

    /// <summary>
    /// Scrolls the mouse wheel.
    /// Positive value = forward (away from user), negative = backward (toward user).
    /// </summary>
    public async Task<(bool Success, string Message)> ScrollAsync(int wheelDelta)
        => await _driver.PerformActionAsync("", UiAutomation.ActionType.MousWeelScroll, wheelDelta.ToString());

    /// <summary>
    /// Scrolls by number of steps (each step is 120 wheel units).
    /// </summary>
    public async Task<(bool Success, string Message)> ScrollStepsAsync(int steps)
        => await ScrollAsync(steps * 120);

    #endregion
}
