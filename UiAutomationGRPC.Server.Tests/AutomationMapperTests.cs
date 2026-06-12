using FlaUI.Core.Definitions;
using Google.Protobuf.Collections;
using NUnit.Framework;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;
using FlaConditions = FlaUI.Core.Conditions;

namespace UiAutomationGRPC.Server.Tests
{
    /// <summary>
    /// Tests for <see cref="AutomationMapper"/> pure-logic methods.
    /// None of these tests require a live UI Automation session — FlaUI condition and
    /// identifier objects are plain managed objects (the UIA3 COM session is only touched
    /// when a condition is actually used in a Find call).
    /// </summary>
    [TestFixture]
    public class AutomationMapperTests
    {
        // ────────────────────────────── MapScope ──────────────────────────────

        [TestCase(UiAutomation.TreeScope.Children, FlaUI.Core.Definitions.TreeScope.Children)]
        [TestCase(UiAutomation.TreeScope.Descendants, FlaUI.Core.Definitions.TreeScope.Descendants)]
        [TestCase(UiAutomation.TreeScope.Subtree, FlaUI.Core.Definitions.TreeScope.Subtree)]
        [TestCase(UiAutomation.TreeScope.Parent, FlaUI.Core.Definitions.TreeScope.Parent)]
        [TestCase(UiAutomation.TreeScope.Ancestors, FlaUI.Core.Definitions.TreeScope.Ancestors)]
        [TestCase(UiAutomation.TreeScope.Element, FlaUI.Core.Definitions.TreeScope.Element)]
        public void MapScope_MapsEachProtoValueToMatchingUiaScope(
            UiAutomation.TreeScope proto, FlaUI.Core.Definitions.TreeScope expected)
        {
            Assert.That(AutomationMapper.MapScope(proto), Is.EqualTo(expected));
        }

        [Test]
        public void MapScope_UnknownValue_FallsBackToChildren()
        {
            Assert.That(AutomationMapper.MapScope((UiAutomation.TreeScope)99),
                Is.EqualTo(FlaUI.Core.Definitions.TreeScope.Children));
        }

        // ────────────────────────────── ParseValue ──────────────────────────────

        [Test]
        public void ParseValue_IntType_ReturnsBoxedInt()
        {
            var result = AutomationMapper.ParseValue("42", PropertyType.Int);
            Assert.That(result, Is.EqualTo(42));
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        [TestCase("True", true)]
        [TestCase("False", false)]
        public void ParseValue_BoolType_ReturnsBoxedBool(string input, bool expected)
        {
            var result = AutomationMapper.ParseValue(input, PropertyType.Bool);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("Notepad")]
        [TestCase("")]
        [TestCase("42")]
        [TestCase("ControlType.Window")]
        public void ParseValue_StringType_ReturnsStringUnchanged(string input)
        {
            Assert.That(AutomationMapper.ParseValue(input, PropertyType.String), Is.EqualTo(input));
        }

        // ────────────────────────────── LookupProperty ──────────────────────────────

        [TestCase("name")]
        [TestCase("Name")]
        [TestCase("NAME")]
        public void LookupProperty_Name_ReturnsNameProperty(string input)
        {
            Assert.That(AutomationMapper.LookupProperty(input),
                Is.EqualTo(UiaRuntime.Properties.Element.Name));
        }

        [TestCase("automationid")]
        [TestCase("AutomationId")]
        [TestCase("AUTOMATIONID")]
        public void LookupProperty_AutomationId_ReturnsAutomationIdProperty(string input)
        {
            Assert.That(AutomationMapper.LookupProperty(input),
                Is.EqualTo(UiaRuntime.Properties.Element.AutomationId));
        }

        [TestCase("classname")]
        [TestCase("ClassName")]
        public void LookupProperty_ClassName_ReturnsClassNameProperty(string input)
        {
            Assert.That(AutomationMapper.LookupProperty(input),
                Is.EqualTo(UiaRuntime.Properties.Element.ClassName));
        }

        [Test]
        public void LookupProperty_ControlType_ReturnsControlTypeProperty()
        {
            Assert.That(AutomationMapper.LookupProperty("controltype"),
                Is.EqualTo(UiaRuntime.Properties.Element.ControlType));
        }

        [Test]
        public void LookupProperty_IsEnabled_ReturnsIsEnabledProperty()
        {
            Assert.That(AutomationMapper.LookupProperty("isenabled"),
                Is.EqualTo(UiaRuntime.Properties.Element.IsEnabled));
        }

        [Test]
        public void LookupProperty_BoundingRectangle_ReturnsBoundingRectangleProperty()
        {
            Assert.That(AutomationMapper.LookupProperty("boundingrectangle"),
                Is.EqualTo(UiaRuntime.Properties.Element.BoundingRectangle));
        }

        [Test]
        public void LookupProperty_UnknownName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => AutomationMapper.LookupProperty("doesnotexist"));
        }

        // ────────────────────────────── MapControlTypeValue ──────────────────────────────
        // Control-type condition values arrive in three shapes; all must land on the enum.

        [TestCase("Window", ControlType.Window)]
        [TestCase("button", ControlType.Button)]
        [TestCase("ControlType.Window", ControlType.Window)]
        [TestCase("ControlType.Edit", ControlType.Edit)]
        public void MapControlTypeValue_StringForms_MapToEnum(string input, ControlType expected)
        {
            Assert.That(AutomationMapper.MapControlTypeValue(input), Is.EqualTo(expected));
        }

        [Test]
        public void MapControlTypeValue_NativeUiaId_MapsToEnum()
        {
            // 50032 = UIA_WindowControlTypeId, 50004 = UIA_EditControlTypeId
            Assert.Multiple(() =>
            {
                Assert.That(AutomationMapper.MapControlTypeValue(50032), Is.EqualTo(ControlType.Window));
                Assert.That(AutomationMapper.MapControlTypeValue(50004), Is.EqualTo(ControlType.Edit));
            });
        }

        [Test]
        public void MapControlTypeValue_NativeUiaIdAsString_MapsToEnum()
        {
            Assert.That(AutomationMapper.MapControlTypeValue("50032"), Is.EqualTo(ControlType.Window));
        }

        [Test]
        public void MapControlTypeValue_UnknownString_ReturnsValueUnchanged()
        {
            Assert.That(AutomationMapper.MapControlTypeValue("NoSuchControlType"),
                Is.EqualTo("NoSuchControlType"));
        }

        // ────────────────────────────── ControlTypeName ──────────────────────────────

        [Test]
        public void ControlTypeName_KeepsUia2ProgrammaticNameFormat()
        {
            Assert.That(AutomationMapper.ControlTypeName(ControlType.Button),
                Is.EqualTo("ControlType.Button"));
        }

        // ────────────────────────────── MapCondition ──────────────────────────────

        [Test]
        public void MapCondition_Null_ReturnsTrueCondition()
        {
            Assert.That(AutomationMapper.MapCondition(null!),
                Is.SameAs(FlaConditions.TrueCondition.Default));
        }

        [Test]
        public void MapCondition_EmptyProto_ReturnsTrueCondition()
        {
            Assert.That(AutomationMapper.MapCondition(new UiAutomation.Condition()),
                Is.SameAs(FlaConditions.TrueCondition.Default));
        }

        [Test]
        public void MapCondition_TrueCondition_ReturnsTrueCondition()
        {
            var proto = new UiAutomation.Condition { TrueCondition = true };
            Assert.That(AutomationMapper.MapCondition(proto),
                Is.SameAs(FlaConditions.TrueCondition.Default));
        }

        [Test]
        public void MapCondition_PropertyConditionByName_ReturnsPropertyCondition()
        {
            var proto = new UiAutomation.Condition
            {
                PropertyCondition = new UiAutomation.PropertyCondition
                {
                    PropertyName = "name",
                    PropertyValue = "Notepad",
                    PropertyType = PropertyType.String
                }
            };
            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<FlaConditions.PropertyCondition>());
        }

        [Test]
        public void MapCondition_PropertyConditionByAutomationId_ReturnsPropertyCondition()
        {
            var proto = new UiAutomation.Condition
            {
                PropertyCondition = new UiAutomation.PropertyCondition
                {
                    PropertyName = "automationid",
                    PropertyValue = "btn_ok",
                    PropertyType = PropertyType.String
                }
            };
            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<FlaConditions.PropertyCondition>());
        }

        [Test]
        public void MapCondition_PropertyConditionBoolValue_ReturnsPropertyCondition()
        {
            var proto = new UiAutomation.Condition
            {
                PropertyCondition = new UiAutomation.PropertyCondition
                {
                    PropertyName = "isenabled",
                    PropertyValue = "true",
                    PropertyType = PropertyType.Bool
                }
            };
            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<FlaConditions.PropertyCondition>());
        }

        [Test]
        public void MapCondition_ControlTypeCondition_NormalizesValueToEnum()
        {
            var proto = new UiAutomation.Condition
            {
                PropertyCondition = new UiAutomation.PropertyCondition
                {
                    PropertyName = "controltype",
                    PropertyValue = "ControlType.Window",
                    PropertyType = PropertyType.String
                }
            };

            var mapped = AutomationMapper.MapCondition(proto);

            Assert.That(mapped, Is.TypeOf<FlaConditions.PropertyCondition>());
            Assert.That(((FlaConditions.PropertyCondition)mapped).Value,
                Is.EqualTo(ControlType.Window));
        }

        [Test]
        public void MapCondition_AndCondition_ReturnsAndCondition()
        {
            var boolCond = new BoolCondition();
            boolCond.Conditions.Add(new UiAutomation.Condition { TrueCondition = true });
            boolCond.Conditions.Add(new UiAutomation.Condition { TrueCondition = true });
            var proto = new UiAutomation.Condition { AndCondition = boolCond };

            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<FlaConditions.AndCondition>());
        }

        [Test]
        public void MapCondition_OrCondition_ReturnsOrCondition()
        {
            var boolCond = new BoolCondition();
            boolCond.Conditions.Add(new UiAutomation.Condition { TrueCondition = true });
            boolCond.Conditions.Add(new UiAutomation.Condition { TrueCondition = true });
            var proto = new UiAutomation.Condition { OrCondition = boolCond };

            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<FlaConditions.OrCondition>());
        }

        [Test]
        public void MapCondition_NotCondition_ReturnsNotCondition()
        {
            var proto = new UiAutomation.Condition
            {
                NotCondition = new UiAutomation.Condition { TrueCondition = true }
            };

            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<FlaConditions.NotCondition>());
        }

        [Test]
        public void MapCondition_NestedAndInsideNot_ReturnsNotCondition()
        {
            var boolCond = new BoolCondition();
            boolCond.Conditions.Add(new UiAutomation.Condition { TrueCondition = true });
            boolCond.Conditions.Add(new UiAutomation.Condition { TrueCondition = true });

            var proto = new UiAutomation.Condition
            {
                NotCondition = new UiAutomation.Condition { AndCondition = boolCond }
            };

            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<FlaConditions.NotCondition>());
        }

        // ────────────────────────────── MapConditionList ──────────────────────────────

        [Test]
        public void MapConditionList_Empty_ReturnsEmptyList()
        {
            var field = new RepeatedField<UiAutomation.Condition>();
            Assert.That(AutomationMapper.MapConditionList(field), Is.Empty);
        }

        [Test]
        public void MapConditionList_TwoTrueConditions_ReturnsTwoEntries()
        {
            var field = new RepeatedField<UiAutomation.Condition>
            {
                new UiAutomation.Condition { TrueCondition = true },
                new UiAutomation.Condition { TrueCondition = true }
            };
            Assert.That(AutomationMapper.MapConditionList(field), Has.Count.EqualTo(2));
        }

        [Test]
        public void MapConditionList_MixedTypes_MapsEachCorrectly()
        {
            var boolCond = new BoolCondition();
            boolCond.Conditions.Add(new UiAutomation.Condition { TrueCondition = true });
            boolCond.Conditions.Add(new UiAutomation.Condition { TrueCondition = true });

            var field = new RepeatedField<UiAutomation.Condition>
            {
                new UiAutomation.Condition { TrueCondition = true },
                new UiAutomation.Condition { AndCondition = boolCond },
                new UiAutomation.Condition { NotCondition = new UiAutomation.Condition { TrueCondition = true } }
            };

            var result = AutomationMapper.MapConditionList(field);

            Assert.Multiple(() =>
            {
                Assert.That(result, Has.Count.EqualTo(3));
                Assert.That(result[0], Is.SameAs(FlaConditions.TrueCondition.Default));
                Assert.That(result[1], Is.TypeOf<FlaConditions.AndCondition>());
                Assert.That(result[2], Is.TypeOf<FlaConditions.NotCondition>());
            });
        }
    }
}
