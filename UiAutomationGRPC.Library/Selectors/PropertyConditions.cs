using System.Collections.Generic;
using UiAutomation;

namespace UiAutomationGRPC.Library.Selectors
{
    /// <summary>
    /// Builder for property conditions.
    /// </summary>
    public class PropertyConditions
    {
        internal List<Condition> Condition = new List<Condition>();

        /// <summary>
        /// Adds a FrameworkId condition.
        /// </summary>
        /// <param name="param">The FrameworkId value.</param>
        /// <returns>The current instance.</returns>
        public PropertyConditions FrameworkIdProperty(string param)
        {
            AddProperty("FrameworkId", param);
            return this;
        }

        /// <summary>
        /// Adds a ClassName condition.
        /// </summary>
        /// <param name="param">The ClassName value.</param>
        /// <returns>The current instance.</returns>
        public PropertyConditions ClassNameProperty(string param)
        {
            AddProperty("ClassName", param);
            return this;
        }

        /// <summary>
        /// Adds an AutomationId condition.
        /// </summary>
        /// <param name="param">The AutomationId value.</param>
        /// <returns>The current instance.</returns>
        public PropertyConditions AutomationIdProperty(string param)
        {
            AddProperty("AutomationId", param);
            return this;
        }

        /// <summary>
        /// Adds a Name condition.
        /// </summary>
        /// <param name="param">The Name value.</param>
        /// <returns>The current instance.</returns>
        public PropertyConditions NameProperty(string param)
        {
            AddProperty("Name", param);
            return this;
        }

        /// <summary>
        /// Adds a LocalizedControlType condition.
        /// </summary>
        /// <param name="param">The LocalizedControlType value.</param>
        /// <returns>The current instance.</returns>
        public PropertyConditions LocalizedControlTypeProperty(string param)
        {
            AddProperty("LocalizedControlType", param);
            return this;
        }

        private void AddProperty(string propertyName, string propertyValue)
        {
            var propCondition = new PropertyCondition
            {
                PropertyName = propertyName,
                PropertyValue = propertyValue,
                PropertyType = PropertyType.String // Defaulting to String for now as per previous usage
            };
            
            Condition.Add(new Condition { PropertyCondition = propCondition });
        }
        
        // Helper to manually add a constructed Condition if needed
        internal void AddCondition(Condition condition)
        {
            Condition.Add(condition);
        }
    }
}