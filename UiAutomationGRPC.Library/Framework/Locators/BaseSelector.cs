using System.Collections.Generic;

namespace UiAutomationGRPC.Library.Locators
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