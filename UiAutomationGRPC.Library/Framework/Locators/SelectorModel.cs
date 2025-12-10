using System.Collections.Generic;
using UiAutomation;

namespace UiAutomationGRPC.Library.Locators
{
    public class SelectorModel
    {
        public object AdditionalSearchProperty { get; set; }
        internal List<Condition> Condition { get; set; }
        public SearchType? SearchType { get; set; }
    }
}