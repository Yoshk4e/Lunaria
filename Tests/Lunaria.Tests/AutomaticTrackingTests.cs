using Lunaria.Common.Tracking;
using Xunit;

namespace Lunaria.Tests;

public sealed partial class AutomaticTrackingTests
{
    public sealed partial class Counter : TrackedObject
    {
        private int __trackedValue;
        [Tracked] public partial int Value { get; set; }
    }

    [Fact]
    public void SameValueAndRevertedChangesDoNotRequireSaving()
    {
        var counter = new Counter { Value = 10 };
        counter.ClearDirty();
        counter.Value = 10;
        Assert.False(counter.IsDirty);
        counter.Value = 20;
        Assert.True(counter.IsDirty);
        counter.Value = 10;
        Assert.False(counter.IsDirty);
    }

    [Fact]
    public void DictionaryTracksNestedMutationsAndDetachesRemovedValues()
    {
        var child = new Counter { Value = 10 };
        var values = new TrackedDictionary<int, Counter> { [1] = child };
        values.Changes.AcceptAll();
        Assert.False(child.IsDirty);
        child.Value = 20;
        Assert.Equal(1, Assert.Single(values.Changes.ChangedKeys));
        child.Value = 10;
        Assert.False(values.Changes.HasChanges);
        values.Remove(1);
        values.Changes.AcceptAll();
        child.Value = 30;
        Assert.False(values.Changes.HasChanges);
    }

    [Fact]
    public void AddThenRemoveAndRemoveThenRestoreAreNoOps()
    {
        var values = new TrackedDictionary<int, int> { [1] = 10 };
        values.Changes.AcceptAll();
        values.Add(2, 20);
        values.Remove(2);
        values.Remove(1);
        values.Add(1, 10);
        Assert.False(values.Changes.HasChanges);
    }

    [Fact]
    public void KeyedListReportsOnlyAffectedIds()
    {
        var values = new TrackedList<(int Id, int Value)>(item => item.Id) { (1, 10), (2, 20) };
        values.Changes.AcceptAll();
        values[1] = (2, 21);
        Assert.Equal(2, Assert.Single(values.Changes.ChangedKeys));
        values[1] = (2, 20);
        Assert.False(values.Changes.HasChanges);
    }

    [Fact]
    public void AcceptingCapturedBatchPreservesChangesMadeDuringSave()
    {
        var counter = new Counter();
        counter.Value = 1;
        var batch = counter.Changes.Capture();
        counter.Value = 2;
        batch.Accept();
        Assert.True(counter.IsDirty);
        counter.Value = 1;
        Assert.False(counter.IsDirty);
        counter.Value = 3;
        batch.Accept(); // A completed batch cannot clear a subsequent operation.
        Assert.True(counter.IsDirty);
    }

    [Fact]
    public void UnacceptedSaveRetainsNestedChangesForRetry()
    {
        var journal = new ChangeJournal();
        var values = new TrackedDictionary<int, int>();
        journal.Observe(values.Changes, "wallet");
        values[1] = 100;
        _ = journal.Capture(); // Simulate a transaction that throws before commit.
        Assert.True(journal.HasChanges);
        Assert.True(journal.IsChanged("wallet"));
        journal.Capture().Accept();
        Assert.False(journal.HasChanges);
    }
}
