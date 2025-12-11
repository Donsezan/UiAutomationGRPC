namespace UiAutomationGRPC.Library.Helpers
{
    /// <summary>
    /// Constants for usability time limits.
    /// </summary>
    public class UsabilityTimeLimits
    {
        /// <summary>
        /// Application load limit in seconds.
        /// </summary>
        public const int ApplicationLoadLimit = 180;

        /// <summary>
        /// Page load limit in milliseconds.
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
}
