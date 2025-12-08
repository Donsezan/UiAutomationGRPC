using System.Collections.Generic;
using System.Windows.Automation;

namespace UiAutomationGRPC.Client
{
    public class SelectorModel
    {
        public object AdditionalSearchProperty { get; set; }
        public List<Condition> Condition { get; set; }
        public SearchType? SearchType { get; set; }
    }
}