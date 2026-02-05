namespace UiAutomationGRPC.Library.Helpers;

/// <summary>
/// Time limits for UI automation usability.
/// </summary>
public static class UsabilityTimeLimits
{
    /// <summary>
    /// Application load wait limit in milliseconds.
    /// </summary>
    public const int ApplicationLoadLimit = 180;

    /// <summary>
    /// Page load wait limit in milliseconds.
    /// </summary>
    public const int PageLoadLimit = 3000;

    /// <summary>
    /// Keyboard readiness delay in milliseconds.
    /// </summary>
    public const int KeyboardReadiness = 300;

    /// <summary>
    /// Animation time limit in milliseconds.
    /// </summary>
    public const int AnimationTimeLimit = 30000;
}
