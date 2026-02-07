using Grpc.Core;
using System.Reflection;
using System.Windows.Automation;
using UiAutomation;
using PropertyCondition = System.Windows.Automation.PropertyCondition;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Static helper for mapping proto types to Windows Automation types.
    /// </summary>
    public static class AutomationMapper
    {
        /// <summary>
        /// Maps a proto Condition to a Windows Automation Condition.
        /// </summary>
        public static System.Windows.Automation.Condition MapCondition(UiAutomation.Condition protoCondition)
        {
            if (protoCondition == null) return System.Windows.Automation.Condition.TrueCondition;

            switch (protoCondition.ConditionTypeCase)
            {
                case UiAutomation.Condition.ConditionTypeOneofCase.TrueCondition:
                    return System.Windows.Automation.Condition.TrueCondition;
                
                case UiAutomation.Condition.ConditionTypeOneofCase.PropertyCondition:
                    var pc = protoCondition.PropertyCondition;
                    AutomationProperty prop = LookupProperty(pc.PropertyName);
                    object val = ParseValue(pc.PropertyValue, pc.PropertyType);
                    return new PropertyCondition(prop, val);

                case UiAutomation.Condition.ConditionTypeOneofCase.AndCondition:
                    var subsAnd = MapConditionList(protoCondition.AndCondition.Conditions);
                    return new AndCondition(subsAnd.ToArray());

                case UiAutomation.Condition.ConditionTypeOneofCase.OrCondition:
                    var subsOr = MapConditionList(protoCondition.OrCondition.Conditions);
                    return new OrCondition(subsOr.ToArray());

                case UiAutomation.Condition.ConditionTypeOneofCase.NotCondition:
                    return new NotCondition(MapCondition(protoCondition.NotCondition));

                default:
                    return System.Windows.Automation.Condition.TrueCondition;
            }
        }

        /// <summary>
        /// Maps a list of proto Conditions to Windows Automation Conditions.
        /// </summary>
        public static List<System.Windows.Automation.Condition> MapConditionList(Google.Protobuf.Collections.RepeatedField<UiAutomation.Condition> conditions)
        {
            var list = new List<System.Windows.Automation.Condition>();
            foreach (var c in conditions) list.Add(MapCondition(c));
            return list;
        }

        /// <summary>
        /// Parses a string value to the appropriate type.
        /// </summary>
        public static object ParseValue(string value, PropertyType type)
        {
            switch (type)
            {
                case PropertyType.Int: return int.Parse(value);
                case PropertyType.Bool: return bool.Parse(value);
                default: return value;
            }
        }

        /// <summary>
        /// Looks up an AutomationProperty by name.
        /// </summary>
        public static AutomationProperty LookupProperty(string name)
        {
            switch (name.ToLower())
            {
                case "name": return AutomationElement.NameProperty;
                case "automationid": return AutomationElement.AutomationIdProperty;
                case "classname": return AutomationElement.ClassNameProperty;
                case "controltype": return AutomationElement.ControlTypeProperty;
                case "isenabled": return AutomationElement.IsEnabledProperty;
                case "boundingrectangle": return AutomationElement.BoundingRectangleProperty;
                default: throw new ArgumentException($"Unknown property: {name}");
            }
        }

        /// <summary>
        /// Maps a proto TreeScope to Windows Automation TreeScope.
        /// </summary>
        public static System.Windows.Automation.TreeScope MapScope(UiAutomation.TreeScope scope)
        {
            switch (scope)
            {
                case UiAutomation.TreeScope.Children: return System.Windows.Automation.TreeScope.Children;
                case UiAutomation.TreeScope.Descendants: return System.Windows.Automation.TreeScope.Descendants;
                case UiAutomation.TreeScope.Subtree: return System.Windows.Automation.TreeScope.Subtree;
                case UiAutomation.TreeScope.Parent: return System.Windows.Automation.TreeScope.Parent;
                case UiAutomation.TreeScope.Ancestors: return System.Windows.Automation.TreeScope.Ancestors;
                case UiAutomation.TreeScope.Element: return System.Windows.Automation.TreeScope.Element;
                default: return System.Windows.Automation.TreeScope.Children;
            }
        }

        /// <summary>
        /// Gets a pattern from an element.
        /// </summary>
        public static T GetPattern<T>(AutomationElement element, AutomationPattern pattern) where T : BasePattern
        {
            if (element.TryGetCurrentPattern(pattern, out object pObj))
            {
                return (T)pObj;
            }
            throw new InvalidOperationException($"Element does not support pattern {pattern.ProgrammaticName}");
        }

        /// <summary>
        /// Maps an AutomationElement to an ElementResponse.
        /// </summary>
        public static ElementResponse MapToResponse(AutomationElement element)
        {
            try
            {
                string runtimeId = ElementCache.CacheElement(element);

                return new ElementResponse
                {
                    Name = element.Current.Name ?? "",
                    AutomationId = element.Current.AutomationId ?? "",
                    ClassName = element.Current.ClassName ?? "",
                    ControlType = element.Current.ControlType.ProgrammaticName,
                    RuntimeId = runtimeId
                };
            }
            catch (ElementNotAvailableException)
            {
                return new ElementResponse { Name = "ElementNotAvailable" };
            }
        }
    }
}
