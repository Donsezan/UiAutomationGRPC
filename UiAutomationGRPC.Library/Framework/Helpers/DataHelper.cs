using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace UiAutomationGRPC.Library.Helpers
{
    public class DataHelper
    {
        public static void InitData()
        {
       
        }       

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetCurrentMethodName(int frame = 3)
        {
            var st = new StackTrace();
            var sf = st.GetFrame(frame);
            var val = sf.GetMethod().Name;
            val = string.Concat(val.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
            return val;
        }
              
        public static void SetSystemGlobalVariable(string variableName, string variableValue)
        {
            try
            {
                Environment.SetEnvironmentVariable(variableName, variableValue, EnvironmentVariableTarget.User);
                Console.WriteLine("System global variable " + GetSystemGlobalVariable(variableName));
            }
            catch
            {
                Console.WriteLine("Variable not created");
            }
            // PwerShell scripts 
            // Get-ChildItem Env:
            // Remove-Item Env:\Testname
        }

     
        public static string GetSystemGlobalVariable(string variableName)
        {
            string variableValue = null;
            try
            {
                variableValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User);
                Console.WriteLine("Variable " + variableValue + " created");
            }
            catch
            {
                Console.WriteLine("Variable not created");
            }
            return variableValue;
        }

        [DllImport("kernel32.dll")]
        public static extern void GetSystemTime(ref Systemtime lpSystemTime);

        [DllImport("kernel32.dll")]
        private static extern uint SetSystemTime(ref Systemtime lpSystemTime);

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
    }
}
