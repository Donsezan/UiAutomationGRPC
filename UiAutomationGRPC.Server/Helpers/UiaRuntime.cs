using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Process-wide UIA3 automation session (FlaUI). One COM <c>CUIAutomation8</c> instance is
    /// shared by every handler: creating it per call is expensive and UIA3 client objects are
    /// thread-safe for MTA use, which matches the single MTA "UIA-Worker" thread all UIA RPCs
    /// run on (see <see cref="UiaExecutor"/>).
    /// </summary>
    public static class UiaRuntime
    {
        private static readonly Lazy<UIA3Automation> _automation =
            new(() => new UIA3Automation(), LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly Lazy<AutomationElement> _desktop =
            new(() => Automation.GetDesktop(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static UIA3Automation Automation => _automation.Value;

        /// <summary>The desktop root element (equivalent of UIA2's <c>AutomationElement.RootElement</c>).</summary>
        public static AutomationElement Desktop => _desktop.Value;

        /// <summary>Shorthand for the property-id library of the shared automation instance.</summary>
        public static IPropertyLibrary Properties => Automation.PropertyLibrary;

        private const int UIA_E_ELEMENTNOTAVAILABLE = unchecked((int)0x80040201);

        /// <summary>
        /// True when an exception means "the element handle went stale" (window closed, control
        /// recycled). FlaUI usually surfaces this as its own ElementNotAvailableException, but raw
        /// COM errors can leak through paths FlaUI does not wrap.
        /// </summary>
        public static bool IsStaleElement(Exception ex) =>
            ex is FlaUI.Core.Exceptions.ElementNotAvailableException
            || (ex is COMException com && com.HResult == UIA_E_ELEMENTNOTAVAILABLE);

        /// <summary>
        /// Builds the string RuntimeId handle used across RPCs (comma-joined UIA runtime id).
        /// Throws when the element is already unavailable — callers treat that as stale.
        /// </summary>
        public static string RuntimeIdOf(AutomationElement element) =>
            string.Join(",", element.Properties.RuntimeId.Value ?? Array.Empty<int>());
    }
}
