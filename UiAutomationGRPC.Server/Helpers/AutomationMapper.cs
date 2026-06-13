using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Identifiers;
using FlaUI.UIA3.Converters;
using UiAutomation;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Static helper for mapping proto types to FlaUI (UIA3) automation types.
    /// </summary>
    public static class AutomationMapper
    {
        /// <summary>
        /// Maps a proto Condition to a FlaUI condition.
        /// </summary>
        public static ConditionBase MapCondition(UiAutomation.Condition protoCondition)
        {
            if (protoCondition == null) return TrueCondition.Default;

            switch (protoCondition.ConditionTypeCase)
            {
                case UiAutomation.Condition.ConditionTypeOneofCase.TrueCondition:
                    return TrueCondition.Default;

                case UiAutomation.Condition.ConditionTypeOneofCase.PropertyCondition:
                    var pc = protoCondition.PropertyCondition;
                    PropertyId prop = LookupProperty(pc.PropertyName);
                    object val = ParseValue(pc.PropertyValue, pc.PropertyType);
                    // ControlType needs its own value space: clients send enum names
                    // ("Window"), programmatic names ("ControlType.Window") or native UIA ids
                    // (50032) — FlaUI conditions want the ControlType enum.
                    if (Equals(prop, UiaRuntime.Properties.Element.ControlType))
                        val = MapControlTypeValue(val);
                    return new FlaUI.Core.Conditions.PropertyCondition(prop, val);

                case UiAutomation.Condition.ConditionTypeOneofCase.AndCondition:
                    var subsAnd = MapConditionList(protoCondition.AndCondition.Conditions);
                    return new AndCondition(subsAnd.ToArray());

                case UiAutomation.Condition.ConditionTypeOneofCase.OrCondition:
                    var subsOr = MapConditionList(protoCondition.OrCondition.Conditions);
                    return new OrCondition(subsOr.ToArray());

                case UiAutomation.Condition.ConditionTypeOneofCase.NotCondition:
                    return new NotCondition(MapCondition(protoCondition.NotCondition));

                default:
                    return TrueCondition.Default;
            }
        }

        /// <summary>
        /// Maps a list of proto Conditions to FlaUI conditions.
        /// </summary>
        public static List<ConditionBase> MapConditionList(Google.Protobuf.Collections.RepeatedField<UiAutomation.Condition> conditions)
        {
            var list = new List<ConditionBase>();
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
        /// Normalizes a control-type condition value to the FlaUI <see cref="ControlType"/> enum.
        /// Accepts the enum name ("Button"), the UIA2-style programmatic name ("ControlType.Button"),
        /// or a numeric id — native UIA ids (50000+) and raw enum values are both understood.
        /// Unrecognized values are returned unchanged so the provider gets a chance to match them.
        /// </summary>
        public static object MapControlTypeValue(object value)
        {
            switch (value)
            {
                case ControlType ct:
                    return ct;

                case int id when id >= 50000:
                    try { return ControlTypeConverter.ToControlType(id); }
                    catch { return value; }

                case int enumValue when Enum.IsDefined(typeof(ControlType), enumValue):
                    return (ControlType)enumValue;

                case string s:
                    string name = s.StartsWith("ControlType.", StringComparison.OrdinalIgnoreCase)
                        ? s.Substring("ControlType.".Length)
                        : s;
                    // Enum.TryParse accepts numeric strings without validating them — guard with
                    // IsDefined so native ids ("50032") fall through to the numeric mapping below.
                    if (Enum.TryParse<ControlType>(name, ignoreCase: true, out var parsed)
                        && Enum.IsDefined(parsed))
                        return parsed;
                    if (int.TryParse(name, out var numeric))
                        return MapControlTypeValue(numeric);
                    return value;

                default:
                    return value;
            }
        }

        /// <summary>
        /// Looks up a PropertyId by name.
        /// </summary>
        public static PropertyId LookupProperty(string name)
        {
            var lib = UiaRuntime.Properties;
            switch (name.ToLower())
            {
                case "name": return lib.Element.Name;
                case "automationid": return lib.Element.AutomationId;
                case "classname": return lib.Element.ClassName;
                case "controltype": return lib.Element.ControlType;
                case "isenabled": return lib.Element.IsEnabled;
                case "boundingrectangle": return lib.Element.BoundingRectangle;
                // Text content of edit/document controls (ValuePattern). Lets GetProperty("Value")
                // read back what an app displays — the read half of SET_VALUE.
                case "value": return lib.Value.Value;
                default: throw new ArgumentException($"Unknown property: {name}");
            }
        }

        /// <summary>
        /// Maps a proto TreeScope to the FlaUI TreeScope.
        /// </summary>
        public static FlaUI.Core.Definitions.TreeScope MapScope(UiAutomation.TreeScope scope)
        {
            switch (scope)
            {
                case UiAutomation.TreeScope.Children: return FlaUI.Core.Definitions.TreeScope.Children;
                case UiAutomation.TreeScope.Descendants: return FlaUI.Core.Definitions.TreeScope.Descendants;
                case UiAutomation.TreeScope.Subtree: return FlaUI.Core.Definitions.TreeScope.Subtree;
                case UiAutomation.TreeScope.Parent: return FlaUI.Core.Definitions.TreeScope.Parent;
                case UiAutomation.TreeScope.Ancestors: return FlaUI.Core.Definitions.TreeScope.Ancestors;
                case UiAutomation.TreeScope.Element: return FlaUI.Core.Definitions.TreeScope.Element;
                default: return FlaUI.Core.Definitions.TreeScope.Children;
            }
        }

        /// <summary>
        /// The UIA2-compatible programmatic name ("ControlType.Button") clients and the skill docs
        /// already rely on — kept stable across the UIA3 migration.
        /// </summary>
        public static string ControlTypeName(ControlType controlType) => "ControlType." + controlType;

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
                    Name = element.Properties.Name.ValueOrDefault ?? "",
                    AutomationId = element.Properties.AutomationId.ValueOrDefault ?? "",
                    ClassName = element.Properties.ClassName.ValueOrDefault ?? "",
                    ControlType = ControlTypeName(element.Properties.ControlType.ValueOrDefault),
                    RuntimeId = runtimeId,
                    Success = true,
                    Message = "Element found."
                };
            }
            catch (Exception ex) when (UiaRuntime.IsStaleElement(ex))
            {
                return new ElementResponse { Success = false, Message = "Element is no longer available." };
            }
        }
    }
}
