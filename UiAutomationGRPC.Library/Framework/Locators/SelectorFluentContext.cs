using System.Collections.Generic;
using UiAutomation;

namespace UiAutomationGRPC.Library.Locators
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
            AddProperty("LocalizedControlType", type);
            return this;
        }

        public SelectorFluentContext AutomationId(string id)
        {
            AddProperty("AutomationId", id);
            return this;
        }

        public SelectorFluentContext NameContain(string name)
        {
            AddProperty("Name", name);
            return this;
        }

        private void AddProperty(string name, string value)
        {
            if (_currentSelector.Condition == null)
                _currentSelector.Condition = new List<Condition>();
            
            var propCondition = new PropertyCondition
            {
                PropertyName = name,
                PropertyValue = value,
                PropertyType = PropertyType.String
            };
            
            _currentSelector.Condition.Add(new Condition { PropertyCondition = propCondition });
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
