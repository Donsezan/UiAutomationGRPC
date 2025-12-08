using System;
using System.Windows.Automation;
using UiAutomation;

namespace UiAutomationGRPC.Client.Framework.Helpers
{
    public static class ConditionConverter
    {
        public static UiAutomation.Condition Convert(System.Windows.Automation.Condition condition)
        {
            if (condition is System.Windows.Automation.PropertyCondition propCond)
            {
                return new UiAutomation.Condition
                {
                    PropertyCondition = new UiAutomation.PropertyCondition
                    {
                        PropertyName = propCond.Property.ProgrammaticName.Replace("AutomationElementIdentifiers.", "").Replace("Property", ""),
                        PropertyValue = propCond.Value.ToString(),
                        PropertyType = GetPropertyType(propCond.Value)
                    }
                };
            }
            // Add specialized support for And/Or/Not if needed, mostly PropertyCondition is used locally.
            // For now, let's assume simple PropertyConditions as seen in the codebase.
            
            throw new NotImplementedException($"Condition type {condition.GetType().Name} is not supported yet.");
        }

        private static UiAutomation.PropertyType GetPropertyType(object value)
        {
            if (value is bool) return UiAutomation.PropertyType.Bool;
            if (value is int) return UiAutomation.PropertyType.Int;
            return UiAutomation.PropertyType.String;
        }
    }
}
