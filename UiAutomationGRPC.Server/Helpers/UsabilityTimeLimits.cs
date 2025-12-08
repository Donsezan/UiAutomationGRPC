using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UiAutomationGRPC.Server.Helpers
{
    public class UsabilityTimeLimits
    {
        public const int ApplicationLoadLimit = 180;

        public const int PageLoadLimit = 3000;

        public const int KeyboardReadiness = 300;

        public const int AnimationTimeLimit = 30000;
    }

}
