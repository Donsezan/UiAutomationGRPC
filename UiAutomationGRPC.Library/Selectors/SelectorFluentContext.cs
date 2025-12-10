using System.Collections.Generic;
using UiAutomation;

namespace UiAutomationGRPC.Library.Selectors
{
    /// <summary>
    /// Context for fluent selector building.
    /// </summary>
    public class SelectorFluentContext : BaseSelector
    {
        private readonly SelectorModel _currentSelector;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectorFluentContext"/> class.
        /// </summary>
        /// <param name="list">The list of selector models.</param>
        /// <param name="currentSelector">The current selector model being built.</param>
        public SelectorFluentContext(List<SelectorModel> list, SelectorModel currentSelector)
        {
            List = list;
            _currentSelector = currentSelector;
        }

        /// <summary>
        /// Adds a ControlType condition.
        /// </summary>
        /// <param name="type">The ControlType value.</param>
        /// <returns>The current instance.</returns>
        public SelectorFluentContext ControlType(string type)
        {
            AddProperty("LocalizedControlType", type);
            return this;
        }

        /// <summary>
        /// Adds an AutomationId condition.
        /// </summary>
        /// <param name="id">The AutomationId value.</param>
        /// <returns>The current instance.</returns>
        public SelectorFluentContext AutomationId(string id)
        {
            AddProperty("AutomationId", id);
            return this;
        }

        /// <summary>
        /// Adds a Name condition (contains match).
        /// </summary>
        /// <param name="name">The Name value to check for containment.</param>
        /// <returns>The current instance.</returns>
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

        /// <summary>
        /// Starts a new selector for Descendants.
        /// </summary>
        /// <returns>A new context for the descendant selector.</returns>
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

        /// <summary>
        /// Starts a new selector for Descendants with initial conditions.
        /// </summary>
        /// <param name="propertyConditions">The conditions.</param>
        /// <returns>A new context for the descendant selector.</returns>
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

        /// <summary>
        /// Starts a new selector for Children.
        /// </summary>
        /// <returns>A new context for the child selector.</returns>
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
