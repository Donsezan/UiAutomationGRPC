using System.Collections.Generic;
using System.Windows.Automation;

namespace UiAutomationGRPC.Client
{
    public class SelectorFluentContext : BaseSelector
    {
        private readonly SelectorModel _currentSelector;

        public SelectorFluentContext(List<SelectorModel> list, SelectorModel currentSelector)
        {
            List = list;
            _currentSelector = currentSelector;
        }

        public SelectorFluentContext ControlType(string type)
        {
            if (_currentSelector.Condition == null)
                _currentSelector.Condition = new List<Condition>();
            
            _currentSelector.Condition.Add(new PropertyCondition(AutomationElement.LocalizedControlTypeProperty, type));
            return this;
        }

        public SelectorFluentContext AutomationId(string id)
        {
            if (_currentSelector.Condition == null)
                _currentSelector.Condition = new List<Condition>();
            
            _currentSelector.Condition.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, id));
            return this;
        }

        public SelectorFluentContext NameContain(string name)
        {
            if (_currentSelector.Condition == null)
                _currentSelector.Condition = new List<Condition>();

            // Using NameProperty for "Contain" as a simplification or we could look into exact matching vs contains.
            // Existing PropertyConditions uses NameProperty.
            _currentSelector.Condition.Add(new PropertyCondition(AutomationElement.NameProperty, name));
            return this;
        }

        // Allow chaining back to new path steps
        public SelectorFluentContext Descendants()
        {
            var nextSelector = new SelectorModel
            {
                SearchType = SearchType.Descendants,
                Condition = new List<Condition>()
            };
            List.Add(nextSelector);
            return new SelectorFluentContext(List, nextSelector);
        }

       public SelectorFluentContext Descendants(PropertyConditions propertyConditions)
        {
            var nextSelector = new SelectorModel
            {
                SearchType = SearchType.Descendants,
                Condition = propertyConditions.Condition
            };
            List.Add(nextSelector);
            return new SelectorFluentContext(List, nextSelector);
        }

        public SelectorFluentContext Children()
        {
            var nextSelector = new SelectorModel
            {
                SearchType = SearchType.Children,
                Condition = new List<Condition>()
            };
            List.Add(nextSelector);
            return new SelectorFluentContext(List, nextSelector);
        }
        
    }
}
