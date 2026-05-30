using Google.Protobuf.Collections;
using NUnit.Framework;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;
using WinAuto = System.Windows.Automation;

namespace UiAutomationGRPC.Server.Tests
{
    /// <summary>
    /// Tests for <see cref="AutomationMapper"/> pure-logic methods.
    /// None of these tests require a live UI Automation session — they only create
    /// condition/property objects that are initialised from static fields.
    /// </summary>
    [TestFixture]
    public class AutomationMapperTests
    {
        // ────────────────────────────── MapScope ──────────────────────────────

        [TestCase(TreeScope.Children, WinAuto.TreeScope.Children)]
        [TestCase(TreeScope.Descendants, WinAuto.TreeScope.Descendants)]
        [TestCase(TreeScope.Subtree, WinAuto.TreeScope.Subtree)]
        [TestCase(TreeScope.Parent, WinAuto.TreeScope.Parent)]
        [TestCase(TreeScope.Ancestors, WinAuto.TreeScope.Ancestors)]
        [TestCase(TreeScope.Element, WinAuto.TreeScope.Element)]
        public void MapScope_MapsEachProtoValueToMatchingUiaScope(
            TreeScope proto, WinAuto.TreeScope expected)
        {
            Assert.That(AutomationMapper.MapScope(proto), Is.EqualTo(expected));
        }

        [Test]
        public void MapScope_UnknownValue_FallsBackToChildren()
        {
            Assert.That(AutomationMapper.MapScope((TreeScope)99),
                Is.EqualTo(WinAuto.TreeScope.Children));
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
                Is.SameAs(WinAuto.AutomationElement.NameProperty));
        }

        [TestCase("automationid")]
        [TestCase("AutomationId")]
        [TestCase("AUTOMATIONID")]
        public void LookupProperty_AutomationId_ReturnsAutomationIdProperty(string input)
        {
            Assert.That(AutomationMapper.LookupProperty(input),
                Is.SameAs(WinAuto.AutomationElement.AutomationIdProperty));
        }

        [TestCase("classname")]
        [TestCase("ClassName")]
        public void LookupProperty_ClassName_ReturnsClassNameProperty(string input)
        {
            Assert.That(AutomationMapper.LookupProperty(input),
                Is.SameAs(WinAuto.AutomationElement.ClassNameProperty));
        }

        [Test]
        public void LookupProperty_ControlType_ReturnsControlTypeProperty()
        {
            Assert.That(AutomationMapper.LookupProperty("controltype"),
                Is.SameAs(WinAuto.AutomationElement.ControlTypeProperty));
        }

        [Test]
        public void LookupProperty_IsEnabled_ReturnsIsEnabledProperty()
        {
            Assert.That(AutomationMapper.LookupProperty("isenabled"),
                Is.SameAs(WinAuto.AutomationElement.IsEnabledProperty));
        }

        [Test]
        public void LookupProperty_BoundingRectangle_ReturnsBoundingRectangleProperty()
        {
            Assert.That(AutomationMapper.LookupProperty("boundingrectangle"),
                Is.SameAs(WinAuto.AutomationElement.BoundingRectangleProperty));
        }

        [Test]
        public void LookupProperty_UnknownName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => AutomationMapper.LookupProperty("doesnotexist"));
        }

        // ────────────────────────────── MapCondition ──────────────────────────────

        [Test]
        public void MapCondition_Null_ReturnsTrueCondition()
        {
            Assert.That(AutomationMapper.MapCondition(null!),
                Is.SameAs(WinAuto.Condition.TrueCondition));
        }

        [Test]
        public void MapCondition_EmptyProto_ReturnsTrueCondition()
        {
            Assert.That(AutomationMapper.MapCondition(new Condition()),
                Is.SameAs(WinAuto.Condition.TrueCondition));
        }

        [Test]
        public void MapCondition_TrueCondition_ReturnsTrueCondition()
        {
            var proto = new Condition { TrueCondition = true };
            Assert.That(AutomationMapper.MapCondition(proto),
                Is.SameAs(WinAuto.Condition.TrueCondition));
        }

        [Test]
        public void MapCondition_PropertyConditionByName_ReturnsPropertyCondition()
        {
            var proto = new Condition
            {
                PropertyCondition = new PropertyCondition
                {
                    PropertyName = "name",
                    PropertyValue = "Notepad",
                    PropertyType = PropertyType.String
                }
            };
            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<WinAuto.PropertyCondition>());
        }

        [Test]
        public void MapCondition_PropertyConditionByAutomationId_ReturnsPropertyCondition()
        {
            var proto = new Condition
            {
                PropertyCondition = new PropertyCondition
                {
                    PropertyName = "automationid",
                    PropertyValue = "btn_ok",
                    PropertyType = PropertyType.String
                }
            };
            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<WinAuto.PropertyCondition>());
        }

        [Test]
        public void MapCondition_PropertyConditionBoolValue_ReturnsPropertyCondition()
        {
            var proto = new Condition
            {
                PropertyCondition = new PropertyCondition
                {
                    PropertyName = "isenabled",
                    PropertyValue = "true",
                    PropertyType = PropertyType.Bool
                }
            };
            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<WinAuto.PropertyCondition>());
        }

        [Test]
        public void MapCondition_AndCondition_ReturnsAndCondition()
        {
            var boolCond = new BoolCondition();
            boolCond.Conditions.Add(new Condition { TrueCondition = true });
            boolCond.Conditions.Add(new Condition { TrueCondition = true });
            var proto = new Condition { AndCondition = boolCond };

            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<WinAuto.AndCondition>());
        }

        [Test]
        public void MapCondition_OrCondition_ReturnsOrCondition()
        {
            var boolCond = new BoolCondition();
            boolCond.Conditions.Add(new Condition { TrueCondition = true });
            boolCond.Conditions.Add(new Condition { TrueCondition = true });
            var proto = new Condition { OrCondition = boolCond };

            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<WinAuto.OrCondition>());
        }

        [Test]
        public void MapCondition_NotCondition_ReturnsNotCondition()
        {
            var proto = new Condition
            {
                NotCondition = new Condition { TrueCondition = true }
            };

            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<WinAuto.NotCondition>());
        }

        [Test]
        public void MapCondition_NestedAndInsideNot_ReturnsNotCondition()
        {
            var boolCond = new BoolCondition();
            boolCond.Conditions.Add(new Condition { TrueCondition = true });
            boolCond.Conditions.Add(new Condition { TrueCondition = true });

            var proto = new Condition
            {
                NotCondition = new Condition { AndCondition = boolCond }
            };

            Assert.That(AutomationMapper.MapCondition(proto),
                Is.TypeOf<WinAuto.NotCondition>());
        }

        // ────────────────────────────── MapConditionList ──────────────────────────────

        [Test]
        public void MapConditionList_Empty_ReturnsEmptyList()
        {
            var field = new RepeatedField<Condition>();
            Assert.That(AutomationMapper.MapConditionList(field), Is.Empty);
        }

        [Test]
        public void MapConditionList_TwoTrueConditions_ReturnsTwoEntries()
        {
            var field = new RepeatedField<Condition>
            {
                new Condition { TrueCondition = true },
                new Condition { TrueCondition = true }
            };
            Assert.That(AutomationMapper.MapConditionList(field), Has.Count.EqualTo(2));
        }

        [Test]
        public void MapConditionList_MixedTypes_MapsEachCorrectly()
        {
            var boolCond = new BoolCondition();
            boolCond.Conditions.Add(new Condition { TrueCondition = true });
            boolCond.Conditions.Add(new Condition { TrueCondition = true });

            var field = new RepeatedField<Condition>
            {
                new Condition { TrueCondition = true },
                new Condition { AndCondition = boolCond },
                new Condition { NotCondition = new Condition { TrueCondition = true } }
            };

            var result = AutomationMapper.MapConditionList(field);

            Assert.Multiple(() =>
            {
                Assert.That(result, Has.Count.EqualTo(3));
                Assert.That(result[0], Is.SameAs(WinAuto.Condition.TrueCondition));
                Assert.That(result[1], Is.TypeOf<WinAuto.AndCondition>());
                Assert.That(result[2], Is.TypeOf<WinAuto.NotCondition>());
            });
        }
    }
}
