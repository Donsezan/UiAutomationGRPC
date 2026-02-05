using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace UiAutomationGRPC.Library.Helpers;

/// <summary>
/// Helper class for data and system operations.
/// </summary>
public static class DataHelper
{
    /// <summary>
    /// Gets the name of the current method or a caller frame method.
    /// </summary>
    /// <param name="frame">Stack frame index.</param>
    /// <returns>Space-separated method name.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GetCurrentMethodName(int frame = 3)
    {
        var st = new StackTrace();
        var sf = st.GetFrame(frame);
        var methodName = sf?.GetMethod()?.Name ?? "Unknown";
        return string.Concat(methodName.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart();
    }

    /// <summary>
    /// Sets a user-level environment variable.
    /// </summary>
    /// <param name="variableName">Name of the variable.</param>
    /// <param name="variableValue">Value of the variable.</param>
    public static void SetSystemGlobalVariable(string variableName, string variableValue)
    {
        try
        {
            Environment.SetEnvironmentVariable(variableName, variableValue, EnvironmentVariableTarget.User);
            Console.WriteLine($"System global variable {GetSystemGlobalVariable(variableName)}");
        }
        catch
        {
            Console.WriteLine("Variable not created");
        }
    }

    /// <summary>
    /// Gets a user-level environment variable.
    /// </summary>
    /// <param name="variableName">Name of the variable.</param>
    /// <returns>Value of the variable or null if not found.</returns>
    public static string? GetSystemGlobalVariable(string variableName)
    {
        try
        {
            var variableValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User);
            Console.WriteLine($"Variable {variableValue} found");
            return variableValue;
        }
        catch
        {
            Console.WriteLine("Variable not found");
            return null;
        }
    }

    #region System Time P/Invoke (Legacy)

    [DllImport("kernel32.dll")]
    public static extern void GetSystemTime(ref Systemtime lpSystemTime);

    [DllImport("kernel32.dll")]
    private static extern uint SetSystemTime(ref Systemtime lpSystemTime);

    [StructLayout(LayoutKind.Sequential)]
    public struct Systemtime
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    #endregion
}
