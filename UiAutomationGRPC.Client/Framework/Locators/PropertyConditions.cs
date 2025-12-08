using System.Collections.Generic;
using System.Windows.Automation;
using System.Windows.Automation;

namespace UiAutomationGRPC.Client
{
    public class PropertyConditions
    {
        public List<Condition> Condition = new List<Condition>();

        public PropertyConditions FrameworkIdProperty(string param)
        {
            AddProperty(new PropertyCondition(AutomationElement.FrameworkIdProperty, param));
            return this;
        }

        public PropertyConditions ClassNameProperty(string param)
        {
            AddProperty(new PropertyCondition(AutomationElement.ClassNameProperty, param));
            return this;
        }

        public PropertyConditions AutomationIdProperty(string param)
        {
            AddProperty(new PropertyCondition(AutomationElement.AutomationIdProperty, param));
            return this;
        }

        public PropertyConditions NameProperty(string param)
        {
            AddProperty(new PropertyCondition(AutomationElement.NameProperty, param));
            return this;
        }

        public PropertyConditions LocalizedControlTypeProperty(string param)
        {
            AddProperty(new PropertyCondition(AutomationElement.LocalizedControlTypeProperty, param));
            return this;
        }

        private void AddProperty(Condition property)
        {
            Condition.Add(property);
        }
    }
}