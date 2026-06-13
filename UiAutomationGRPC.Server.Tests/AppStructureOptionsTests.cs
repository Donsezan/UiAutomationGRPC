using Newtonsoft.Json;
using NUnit.Framework;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Tests
{
    /// <summary>
    /// Tests for <see cref="AppStructureOptions"/> default values and the JSON formatting
    /// behaviour driven by those options.
    ///
    /// <para>
    /// MaxDepth / MaxNodes enforcement requires calling <c>AppStructureHandler.BuildNode</c>
    /// against a live UI tree and is covered by integration (P3) tests. The tests here
    /// focus on what can be verified without a UIA session:
    /// default option values, compact vs indented serialisation, and <see cref="AppNode"/>
    /// conditional-serialisation rules (ShouldSerialize* / DefaultValueHandling).
    /// </para>
    /// </summary>
    [TestFixture]
    public class AppStructureOptionsTests
    {
        // ────────────────────────────── AppStructureOptions defaults ──────────────────────────────

        [Test]
        public void Defaults_MaxDepth_Is40()
        {
            Assert.That(new AppStructureOptions().MaxDepth, Is.EqualTo(40));
        }

        [Test]
        public void Defaults_MaxNodes_Is2000()
        {
            Assert.That(new AppStructureOptions().MaxNodes, Is.EqualTo(2000));
        }

        [Test]
        public void Defaults_IncludeOffscreen_IsFalse()
        {
            Assert.That(new AppStructureOptions().IncludeOffscreen, Is.False);
        }

        [Test]
        public void Defaults_CompactJson_IsTrue()
        {
            Assert.That(new AppStructureOptions().CompactJson, Is.True);
        }

        // ────────────────────────────── Per-request override merge ──────────────────────────────

        [Test]
        public void MergeWith_Null_ReturnsSameInstance()
        {
            var defaults = new AppStructureOptions();
            Assert.That(defaults.MergeWith(null), Is.SameAs(defaults));
        }

        [Test]
        public void MergeWith_UnsetFields_KeepDefaults()
        {
            var defaults = new AppStructureOptions { MaxDepth = 12, MaxNodes = 345, IncludeOffscreen = true, CompactJson = false };
            var merged = defaults.MergeWith(new UiAutomation.StructureOptions());

            Assert.Multiple(() =>
            {
                Assert.That(merged.MaxDepth, Is.EqualTo(12));
                Assert.That(merged.MaxNodes, Is.EqualTo(345));
                Assert.That(merged.IncludeOffscreen, Is.True);
                Assert.That(merged.CompactJson, Is.False);
            });
        }

        [Test]
        public void MergeWith_PositiveCaps_Override()
        {
            var merged = new AppStructureOptions().MergeWith(new UiAutomation.StructureOptions { MaxDepth = 3, MaxNodes = 50 });

            Assert.Multiple(() =>
            {
                Assert.That(merged.MaxDepth, Is.EqualTo(3));
                Assert.That(merged.MaxNodes, Is.EqualTo(50));
            });
        }

        [Test]
        public void MergeWith_IncludeOffscreen_OverridesOnlyWhenSet()
        {
            var defaults = new AppStructureOptions { IncludeOffscreen = false };

            var explicitTrue = defaults.MergeWith(new UiAutomation.StructureOptions { IncludeOffscreen = true });
            var unset = defaults.MergeWith(new UiAutomation.StructureOptions { MaxDepth = 1 });

            Assert.Multiple(() =>
            {
                Assert.That(explicitTrue.IncludeOffscreen, Is.True);
                Assert.That(unset.IncludeOffscreen, Is.False);
            });
        }

        [Test]
        public void MergeWith_DoesNotMutateDefaults()
        {
            var defaults = new AppStructureOptions { MaxDepth = 40 };
            _ = defaults.MergeWith(new UiAutomation.StructureOptions { MaxDepth = 2 });

            Assert.That(defaults.MaxDepth, Is.EqualTo(40));
        }

        // ────────────────────────────── JSON compact vs indented ──────────────────────────────
        // Validates the CompactJson flag effect — handler uses Formatting.None vs Formatting.Indented.

        private static readonly JsonSerializerSettings JsonSettings =
            new() { NullValueHandling = NullValueHandling.Ignore };

        [Test]
        public void Serialize_WithFormattingNone_ProducesNoNewlines()
        {
            var node = BuildSmallTree();
            var json = JsonConvert.SerializeObject(node, Formatting.None, JsonSettings);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Not.Contain("\n"), "Compact output must not contain newlines");
                Assert.That(json, Does.Not.Contain("\r"), "Compact output must not contain carriage returns");
            });
        }

        [Test]
        public void Serialize_WithFormattingIndented_ContainsNewlines()
        {
            var node = BuildSmallTree();
            var json = JsonConvert.SerializeObject(node, Formatting.Indented, JsonSettings);

            Assert.That(json, Does.Contain(Environment.NewLine),
                "Indented output must contain newlines");
        }

        [Test]
        public void Serialize_CompactJson_IsValidJson()
        {
            var node = BuildSmallTree();
            var json = JsonConvert.SerializeObject(node, Formatting.None, JsonSettings);

            // Must round-trip cleanly — no JSON exception means well-formed output.
            Assert.DoesNotThrow(() => JsonConvert.DeserializeObject<AppNode>(json));
        }

        // ────────────────────────────── AppNode conditional serialisation ──────────────────────────────
        // ShouldSerialize* methods trim noise from the JSON sent to the LLM.

        [Test]
        public void AppNode_EmptyChildren_NotSerialised()
        {
            var node = new AppNode { UniqId = "1", ControlType = "ControlType.Window" };
            var json = JsonConvert.SerializeObject(node, JsonSettings);

            Assert.That(json, Does.Not.Contain("Children"));
        }

        [Test]
        public void AppNode_NullName_NotSerialised()
        {
            var node = new AppNode { UniqId = "1", ControlType = "ControlType.Window" };
            var json = JsonConvert.SerializeObject(node, JsonSettings);

            Assert.That(json, Does.Not.Contain("\"Name\""));
        }

        [Test]
        public void AppNode_NullAutomationId_NotSerialised()
        {
            var node = new AppNode { UniqId = "1", ControlType = "ControlType.Window" };
            var json = JsonConvert.SerializeObject(node, JsonSettings);

            Assert.That(json, Does.Not.Contain("\"UiAutomationId\""));
        }

        [Test]
        public void AppNode_NullBoundingRectangle_NotSerialised()
        {
            var node = new AppNode { UniqId = "1", ControlType = "ControlType.Window" };
            var json = JsonConvert.SerializeObject(node, JsonSettings);

            Assert.That(json, Does.Not.Contain("BoundingRectangle"));
        }

        [Test]
        public void AppNode_ChildrenTruncated_False_NotSerialised()
        {
            var node = new AppNode { UniqId = "1", ChildrenTruncated = false };
            var json = JsonConvert.SerializeObject(node);

            Assert.That(json, Does.Not.Contain("ChildrenTruncated"),
                "ChildrenTruncated=false must be omitted to reduce LLM token noise");
        }

        [Test]
        public void AppNode_ChildrenTruncated_True_IsSerialised()
        {
            var node = new AppNode { UniqId = "1", ChildrenTruncated = true };
            var json = JsonConvert.SerializeObject(node);

            Assert.That(json, Does.Contain("ChildrenTruncated"),
                "ChildrenTruncated=true must appear so the LLM knows the subtree is incomplete");
        }

        [Test]
        public void AppNode_NonEmptyChildren_AreSerialised()
        {
            var node = new AppNode
            {
                UniqId = "root",
                Children = { new AppNode { UniqId = "child" } }
            };
            var json = JsonConvert.SerializeObject(node, JsonSettings);

            Assert.That(json, Does.Contain("Children"));
        }

        [Test]
        public void AppNode_PopulatedName_IsSerialised()
        {
            var node = new AppNode { UniqId = "1", Name = "OK" };
            var json = JsonConvert.SerializeObject(node, JsonSettings);

            Assert.That(json, Does.Contain("\"Name\""));
            Assert.That(json, Does.Contain("\"OK\""));
        }

        // ────────────────────────────── Helper ──────────────────────────────

        private static AppNode BuildSmallTree() =>
            new AppNode
            {
                UniqId = "1",
                Name = "root",
                ControlType = "ControlType.Window",
                IsClickable = false,
                IsVisible = true,
                Children =
                {
                    new AppNode { UniqId = "2", Name = "child", ControlType = "ControlType.Button", IsClickable = true }
                }
            };
    }
}
