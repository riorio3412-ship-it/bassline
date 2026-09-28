using System;
using NUnit.Framework;
using BASSLINE.World.Mansion;

public sealed class MansionCaseBookTests
{
    static MansionCaseBook Book(int residents=18)
    {
        var book=new MansionCaseBook(1,1,0,residents);
        book.Register("CASE_A","CH_04","CH_02");book.Register("CASE_B","CH_05","CH_03");book.Register("CASE_C","CH_06","CH_07");return book;
    }
    static void Cause(MansionCaseBook book,string id,long tick,long sequence,long due)
    {
        Assert.That(book.Reserve(id,tick),Is.EqualTo("Reserved"));book.CommitCause(id,tick,sequence,due);
    }
    [TestCase(18,2)] [TestCase(7,2)] [TestCase(6,1)] [TestCase(4,1)] [TestCase(3,0)]
    public void ChapterStartingPopulationSetsSharedBudget(int residents,int capacity)
    {
        var b=Book(residents);Assert.That(b.Capacity,Is.EqualTo(capacity));
        Assert.That(b.Reserve("CASE_A",1),Is.EqualTo(capacity>0?"Reserved":"FatalityCapacityUnavailable"));
        Assert.That(b.Reserve("CASE_B",1),Is.EqualTo(capacity>1?"Reserved":"FatalityCapacityUnavailable"));
        Assert.That(b.Reserve("CASE_C",1),Is.EqualTo("FatalityCapacityUnavailable"));
        Assert.That(MansionCaseBook.Restore(b.Capture(),1).Capacity,Is.EqualTo(capacity));
    }
    [Test] public void IndependentActorsMayCommitButVictimAndCrossedRolesCannot()
    {
        var b=Book();b.Register("CASE_DUP","CH_08","CH_02");b.Register("CASE_CROSS","CH_02","CH_09");b.Register("CASE_KILL_ACTOR","CH_09","CH_04");
        Cause(b,"CASE_A",1,10,20);
        Assert.That(b.Reserve("CASE_DUP",2),Is.EqualTo("VictimAlreadyReserved"));
        Assert.That(b.Reserve("CASE_CROSS",2),Is.EqualTo("UnsupportedCausalCombination"));
        Assert.That(b.Reserve("CASE_KILL_ACTOR",2),Is.EqualTo("UnsupportedCausalCombination"));
        Cause(b,"CASE_B",2,11,20);Assert.That(b.Used,Is.EqualTo(2));
        Assert.That(b.Find("CASE_A").CauseSequence,Is.EqualTo(10));
    }
    [Test] public void WithdrawalAndActualRiskResolutionFreeBudgetWithoutErasingCause()
    {
        var b=Book(6);Assert.That(b.Reserve("CASE_A",1),Is.EqualTo("Reserved"));b.Release("CASE_A",2);
        Assert.That(b.Reserve("CASE_A",3),Is.EqualTo("ClosedPlan"));Cause(b,"CASE_B",3,10,10);
        Assert.Throws<InvalidOperationException>(()=>b.Release("CASE_B",4));
        Assert.Throws<InvalidOperationException>(()=>b.Release("CASE_B",2,true));
        b.Release("CASE_B",4,true);Assert.That(b.Used,Is.Zero);Assert.That(b.Find("CASE_B").CauseTick,Is.EqualTo(3));
        Cause(b,"CASE_C",5,11,10);b.CommitResult("CASE_C",10);b.Release("CASE_C",11,true);
        Assert.That(b.Used,Is.EqualTo(1));Assert.That(b.Find("CASE_C").ResultTick,Is.EqualTo(10));
        Assert.DoesNotThrow(()=>MansionCaseBook.Restore(b.Capture(),11));
    }
    [Test] public void EarliestResultWinsEvenWhenItsCauseWasLater()
    {
        var b=Book();Cause(b,"CASE_A",1,10,20);Cause(b,"CASE_B",2,11,10);
        b.CommitResult("CASE_B",10);b.Seal(10);b.CommitResult("CASE_A",20);b.Seal(20);
        Assert.That(b.AdjudicatedActorId,Is.EqualTo("CH_05"));Assert.That(b.HasPendingOutcome,Is.False);Assert.That(b.Used,Is.EqualTo(2));
    }
    [Test] public void SameTickTieUsesCauseSequenceAcrossSaveBetweenReverseCallbacks()
    {
        var b=Book();Cause(b,"CASE_A",1,10,20);Cause(b,"CASE_B",2,11,20);b.Seal(19);
        b.CommitResult("CASE_B",20);Assert.That(b.AdjudicatedCaseId,Is.Empty);
        b=MansionCaseBook.Restore(b.Capture(),20);b.CommitResult("CASE_A",20);b.Seal(20);
        Assert.That(b.AdjudicatedActorId,Is.EqualTo("CH_04"));
        Assert.That(MansionCaseBook.Restore(b.Capture(),20).AdjudicatedActorId,Is.EqualTo("CH_04"));
    }
    [Test] public void SealedTickRejectsLateCallbackInsteadOfChangingVerdict()
    {
        var b=Book();Cause(b,"CASE_A",1,10,20);Cause(b,"CASE_B",2,11,20);b.CommitResult("CASE_B",20);b.Seal(20);
        Assert.Throws<InvalidOperationException>(()=>b.CommitResult("CASE_A",20));Assert.That(b.Find("CASE_A").Reserved,Is.True);
        Assert.That(b.AdjudicatedCaseId,Is.EqualTo("CASE_B"));
    }
    [Test] public void RestoreRejectsAlteredVerdictSequenceAndBudget()
    {
        var b=Book();Cause(b,"CASE_A",1,10,20);Cause(b,"CASE_B",2,11,20);b.CommitResult("CASE_A",20);b.CommitResult("CASE_B",20);b.Seal(20);
        var wrong=b.Capture();wrong.AdjudicatedCaseId="CASE_B";Assert.Throws<ArgumentException>(()=>MansionCaseBook.Restore(wrong,20));
        wrong=b.Capture();wrong.Entries[1].CauseSequence=10;Assert.Throws<ArgumentException>(()=>MansionCaseBook.Restore(wrong,20));
        wrong=b.Capture();wrong.StartingResidents=6;Assert.Throws<ArgumentException>(()=>MansionCaseBook.Restore(wrong,20));
    }
    [Test] public void RestoreRejectsFabricatedReleaseAndCaptureDoesNotMutateAuthority()
    {
        var b=Book();b.Reserve("CASE_A",1);b.Release("CASE_A",2);var copy=b.Capture();copy.Entries[0].ReleaseReason="ResultCommitted";
        Assert.Throws<ArgumentException>(()=>MansionCaseBook.Restore(copy,2));Assert.That(b.Find("CASE_A").ReleaseReason,Is.EqualTo("Withdrawn"));
        copy=b.Capture();copy.Entries[0].ReleaseReason="";Assert.Throws<ArgumentException>(()=>MansionCaseBook.Restore(copy,2));
    }
}
