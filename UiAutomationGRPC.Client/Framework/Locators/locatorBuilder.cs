using System.Collections.Generic;
using System.Windows.Automation;

namespace UiAutomationGRPC.Client
{
    public abstract class BaseSelector
    {
        protected List<SelectorModel> List = new List<SelectorModel>();
        /// <summary>
        /// This method creates AutomationElement should be at the end
        /// </summary>
        /// <returns>Returns AutomationElement</returns>
        public AutomationElement Build(bool hasRoot)
        {
            var building = new LocatorBuilder(List);
            return building.Build(hasRoot);
        }
    }
}