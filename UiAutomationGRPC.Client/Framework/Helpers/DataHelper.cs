using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace UiAutomationGRPC.Client.Framework.Helpers
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

        public static string GetCurrentDay() //This method returns day in format "1,10,29"
        {
            var day = DateTime.Today.Day.ToString();
            return day;
        }

        public static string GetCurrentYear() //This method returns day in format "1,10,29"
        {
            var year = DateTime.Today.Year.ToString();
            return year;
        }

        public static string GetMonthAndYear() //This method returns date in format 1216
        {
            var systime = new Systemtime();
            GetSystemTime(ref systime);
            var monthUshort = systime.wMonth;
            var month = monthUshort.ToString();
            if (monthUshort <= 9)
            {
                month = "0" + month; // Change format of month
            }
            var year = systime.wYear.ToString();
            var monthAndYear = month + year.Substring(2, 2);
            return monthAndYear;
        }
               
        private static void ShowCurrentTime()
        {
            // Call the native GetSystemTime method
            // with the defined structure.
            Systemtime sysTime = new Systemtime();
            GetSystemTime(ref sysTime);

            // Show the current time.           
            Console.WriteLine("Current Time: " +
                              sysTime.wHour + ":"
                              + sysTime.wMinute);
            Console.WriteLine("Current Date: " +
                              sysTime.wDay + ":"
                              + sysTime.wMonth + ":"
                              + sysTime.wYear);
        }
    }
}
