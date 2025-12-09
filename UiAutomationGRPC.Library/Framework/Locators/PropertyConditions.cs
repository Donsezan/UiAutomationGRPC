using System.Collections.Generic;
using UiAutomation;

namespace UiAutomationGRPC.Library.Locators
{
    public class PropertyConditions
    {
        public List<Condition> Condition = new List<Condition>();

        public PropertyConditions FrameworkIdProperty(string param)
        {
            AddProperty("FrameworkId", param);
            return this;
        }

        public PropertyConditions ClassNameProperty(string param)
        {
            AddProperty("ClassName", param);
            return this;
        }

        public PropertyConditions AutomationIdProperty(string param)
        {
            AddProperty("AutomationId", param);
            return this;
        }

        public PropertyConditions NameProperty(string param)
        {
            AddProperty("Name", param);
            return this;
        }

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
        public void AddCondition(Condition condition)
        {
            Condition.Add(condition);
        }
    }
}