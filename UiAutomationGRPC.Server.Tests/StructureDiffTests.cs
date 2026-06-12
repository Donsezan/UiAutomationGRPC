using NUnit.Framework;
using UiAutomationGRPC.Server.Helpers;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Tests
{
    [TestFixture]
    public class StructureDiffTests
    {
        private static AppNode Node(string id, string name = "", params AppNode[] children)
        {
            var node = new AppNode { UniqId = id, Name = name, ControlType = "ControlType.Button" };
            node.Children.AddRange(children);
            return node;
        }

        [Test]
        public void Compute_IdenticalTrees_IsEmpty()
        {
            var a = Node("root", "win", Node("1", "ok"), Node("2", "cancel"));
            var b = Node("root", "win", Node("1", "ok"), Node("2", "cancel"));

            var diff = StructureDiff.Compute(a, b);

            Assert.That(diff.IsEmpty, Is.True);
        }

        [Test]
        public void Compute_AddedNode_AppearsInAdded_WithParent()
        {
            var before = Node("root", "win", Node("1", "ok"));
            var after = Node("root", "win", Node("1", "ok"), Node("2", "new button"));

            var diff = StructureDiff.Compute(before, after);

            Assert.Multiple(() =>
            {
                Assert.That(diff.Added, Has.Count.EqualTo(1));
                Assert.That(diff.Added[0].Node.UniqId, Is.EqualTo("2"));
                Assert.That(diff.Added[0].Parent, Is.EqualTo("root"));
                Assert.That(diff.Changed, Is.Empty);
                Assert.That(diff.Removed, Is.Empty);
            });
        }

        [Test]
        public void Compute_RemovedNode_AppearsInRemoved()
        {
            var before = Node("root", "win", Node("1", "ok"), Node("2", "gone"));
            var after = Node("root", "win", Node("1", "ok"));

            var diff = StructureDiff.Compute(before, after);

            Assert.That(diff.Removed, Is.EqualTo(new[] { "2" }));
        }

        [Test]
        public void Compute_ChangedName_AppearsInChanged()
        {
            var before = Node("root", "win", Node("display", "Display is 0"));
            var after = Node("root", "win", Node("display", "Display is 25"));

            var diff = StructureDiff.Compute(before, after);

            Assert.Multiple(() =>
            {
                Assert.That(diff.Changed, Has.Count.EqualTo(1));
                Assert.That(diff.Changed[0].Node.Name, Is.EqualTo("Display is 25"));
                Assert.That(diff.Added, Is.Empty);
                Assert.That(diff.Removed, Is.Empty);
            });
        }

        [Test]
        public void Compute_ReparentedNode_AppearsInChanged()
        {
            var before = Node("root", "win", Node("panelA", "", Node("x", "item")), Node("panelB", ""));
            var after = Node("root", "win", Node("panelA", ""), Node("panelB", "", Node("x", "item")));

            var diff = StructureDiff.Compute(before, after);

            Assert.Multiple(() =>
            {
                Assert.That(diff.Changed.Select(e => e.Node.UniqId), Does.Contain("x"));
                Assert.That(diff.Changed.Single(e => e.Node.UniqId == "x").Parent, Is.EqualTo("panelB"));
            });
        }

        [Test]
        public void Compute_DiffEntries_DoNotCarryChildren()
        {
            var before = Node("root", "win");
            var after = Node("root", "win", Node("panel", "", Node("leaf", "deep")));

            var diff = StructureDiff.Compute(before, after);

            // Both new nodes are reported flat: hierarchy comes from Parent, not nesting.
            Assert.Multiple(() =>
            {
                Assert.That(diff.Added, Has.Count.EqualTo(2));
                Assert.That(diff.Added.All(e => e.Node.Children.Count == 0), Is.True);
                Assert.That(diff.Added.Single(e => e.Node.UniqId == "leaf").Parent, Is.EqualTo("panel"));
            });
        }

        [Test]
        public void Compute_ChangedVisibilityOrClickability_Detected()
        {
            var beforeChild = new AppNode { UniqId = "b", ControlType = "ControlType.Button", IsClickable = false, IsVisible = false };
            var afterChild = new AppNode { UniqId = "b", ControlType = "ControlType.Button", IsClickable = true, IsVisible = true };

            var diff = StructureDiff.Compute(Node("root", "w", beforeChild), Node("root", "w", afterChild));

            Assert.That(diff.Changed.Select(e => e.Node.UniqId), Does.Contain("b"));
        }
    }

    [TestFixture]
    public class StructureSnapshotStoreTests
    {
        [SetUp]
        public void SetUp() => StructureSnapshotStore.Clear();

        [TearDown]
        public void TearDown() => StructureSnapshotStore.Clear();

        [Test]
        public void SetAndGet_RoundTrips()
        {
            var tree = new AppNode { UniqId = "w1" };
            StructureSnapshotStore.Set("w1", 123, tree);

            Assert.That(StructureSnapshotStore.Get("w1"), Is.SameAs(tree));
        }

        [Test]
        public void Get_UnknownKey_ReturnsNull()
        {
            Assert.That(StructureSnapshotStore.Get("nope"), Is.Null);
        }

        [Test]
        public void ClearByProcess_RemovesOnlyThatPid()
        {
            StructureSnapshotStore.Set("w1", 100, new AppNode { UniqId = "w1" });
            StructureSnapshotStore.Set("w2", 200, new AppNode { UniqId = "w2" });

            StructureSnapshotStore.ClearByProcess(100);

            Assert.Multiple(() =>
            {
                Assert.That(StructureSnapshotStore.Get("w1"), Is.Null);
                Assert.That(StructureSnapshotStore.Get("w2"), Is.Not.Null);
            });
        }

        [Test]
        public void Set_EmptyKey_IsIgnored()
        {
            StructureSnapshotStore.Set("", 1, new AppNode());
            Assert.That(StructureSnapshotStore.Count, Is.EqualTo(0));
        }
    }
}
