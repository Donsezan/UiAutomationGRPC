using System.Collections.Generic;
using System.Windows.Automation;

namespace UiAutomationGRPC.Client
{
    public abstract class BaseSelector
    {
        protected List<SelectorModel> List = new List<SelectorModel>();
        
        public List<SelectorModel> GetSelectors()
        {
            return List;
        }

    }
}