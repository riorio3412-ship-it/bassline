using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.Investigation;

public sealed class LocalCausalEvidenceTests
{
    [Test] public void CausalSummaryKeepsOriginalEvidenceAndDoesNotCreateANewObservation()
    {
        var ledger=new KnowledgeLedger(new[]{"CH_01","CH_04","CH_06"},"LOOP_01");
        foreach(var record in Evidence())ledger.Observe("CH_01",record,100);
        var own=ledger.For("CH_01");long before=own.Revision;
        string cause=own.Records().Single(r=>r.Kind=="Visual"&&r.CausalStage=="Cause").Id;
        var summary=FinalReconstruction.BuildConnection(own,cause);
        Assert.That(summary,Is.Not.Null);Assert.That(summary.ActorId,Is.EqualTo("CH_04"));
        Assert.That(summary.RecordIds.Length,Is.GreaterThan(1));Assert.That(summary.RecordIds.All(id=>own.Find(id)!=null),Is.True);
        Assert.That(own.Revision,Is.EqualTo(before));Assert.That(own.Records().Any(r=>r.Predicate=="CausedOutcome"),Is.False);
        Assert.That(FinalReconstruction.BuildConnection(ledger.For("CH_06"),cause),Is.Null);
    }
    [Test] public void CausalSummaryCannotFillInAnUncollectedResult()
    {
        var ledger=new KnowledgeLedger(new[]{"CH_01","CH_04","CH_06"},"LOOP_01");
        foreach(var record in Evidence().Where(r=>r.CausalStage!="Result"))ledger.Observe("CH_01",record,100);
        var own=ledger.For("CH_01");string cause=own.Records().Single(r=>r.Kind=="Visual"&&r.CausalStage=="Cause").Id;
        Assert.That(FinalReconstruction.BuildConnection(own,cause),Is.Null);
    }
    static KnownRecord Stage(string kind,long tick,bool journal)
    {
        return new KnownRecord{Id=(journal?"LOG_":"SEEN_")+kind,LoopId="LOOP_01",Kind=journal?"DeviceLog":"Visual",Direct=true,
            Source=journal?"TERMINAL":"CH_01",ProvenanceKey=journal?"LOCAL_JOURNAL_L1":"WITNESS_1",SubjectId=journal?"TOOL":"CH_04",
            IdentityConfirmed=!journal,Predicate=journal?"DeviceStateTransition":kind=="WarningShown"?"DeviceWarning":"UsedObject",
            Value=journal?"ACT_1:"+kind:"TOOL",ActivationId="ACT_1",DeviceId="TOOL",OutcomeTarget="CH_06",ActionDefinition="ACTION",ActionRevision="1",
            CausalStage=kind,FromTick=tick,ToTick=tick+1,Text="test observation",PlaceId="WORK"};
    }
    static KnownRecord[] Evidence()=>new[]{
        Stage("WarningShown",11,false),Stage("ContactContinued",20,false),Stage("Cause",30,false),
        Stage("WarningShown",10,true),Stage("ContactContinued",20,true),Stage("Cause",30,true),Stage("Result",40,true),
        new KnownRecord{Id="RULE",LoopId="LOOP_01",Kind="Document",Direct=true,SubjectId="ACTION",Predicate="IncidentActionRule",Value="1",ProvenanceKey="ACTION_RULE_ACTION_1",Source="PLATE",FromTick=1,ToTick=2,Text="公開 rule"},
        new KnownRecord{Id="COVERAGE",LoopId="LOOP_01",Kind="Document",Direct=true,SubjectId="TOOL",Predicate="DeviceLogCoverage",Value="LOCAL",ProvenanceKey="COVERAGE",Source="TERMINAL",FromTick=2,ToTick=3,Text="local device only"}
    };
    static bool Link(KnownRecord[] records,out bool conditional)=>CausalEvidence.TryLinkActionJournal(records,"CH_04",CausalEvidence.OutcomeValue("ACT_1","CH_06"),30,41,out conditional,out _);

    [Test] public void ObservedActionAndLocalResultCanConnectWithoutContinuousWitnessOrVideo()
    {
        Assert.That(Link(Evidence(),out bool conditional),Is.True);Assert.That(conditional,Is.False);
        Assert.That(CausalEvidence.TryLinkWitness(Evidence(),"CH_04",CausalEvidence.OutcomeValue("ACT_1","CH_06"),30,41,out _,out _),Is.False);
    }
    [TestCase("SEEN_WarningShown")][TestCase("SEEN_ContactContinued")][TestCase("SEEN_Cause")]
    [TestCase("LOG_WarningShown")][TestCase("LOG_ContactContinued")][TestCase("LOG_Cause")][TestCase("LOG_Result")]
    [TestCase("RULE")][TestCase("COVERAGE")]
    public void MissingNecessaryObservationCannotBeInferred(string omitted)=>Assert.That(Link(Evidence().Where(r=>r.Id!=omitted).ToArray(),out _),Is.False);

    [TestCase("activation")][TestCase("target")][TestCase("revision")][TestCase("source")][TestCase("loop")][TestCase("time")]
    public void SimilarRecordsFromOtherContextsCannotBeJoined(string difference)
    {
        var records=Evidence();var result=records.Single(r=>r.Id=="LOG_Result");
        switch(difference){case "activation":result.ActivationId="ACT_2";break;case "target":result.OutcomeTarget="CH_08";break;case "revision":result.ActionRevision="2";break;case "source":result.Source="OTHER_TERMINAL";break;case "loop":result.LoopId="LOOP_02";break;case "time":result.FromTick++;result.ToTick++;break;}
        Assert.That(Link(records,out _),Is.False);
    }
    [Test] public void CameraCannotBecomeValidBySharingActivationMetadata()
    {
        var records=Evidence();records[2].Kind="Video";Assert.That(Link(records,out _),Is.False);
    }
    [Test] public void AReportedActionRemainsConditionalEvenWithDirectResultLog()
    {
        var records=Evidence();foreach(var r in records.Where(r=>r.Kind=="Visual")){r.Direct=false;r.Kind="Statement";r.Parents=new[]{"ORIGINAL_"+r.Id};}
        Assert.That(Link(records,out bool conditional),Is.True);Assert.That(conditional,Is.True);
        Assert.That(CausalEvidence.TryLink(records,"CH_04",CausalEvidence.OutcomeValue("ACT_1","CH_06"),30,41,out _),Is.False);
    }
    [TestCase("Cancelled")][TestCase("RiskResolved")]
    public void ContradictoryLocalTerminationBlocksAnAutomaticConclusion(string state)
        =>Assert.That(Link(Evidence().Concat(new[]{Stage(state,35,true)}).ToArray(),out _),Is.False);

    [Test] public void RelatedSelectionIncludesOnlyAlreadyOwnedMatchingCoverage()
    {
        var records=Evidence();var stranger=records.Last().Copy();stranger.Id="OTHER";stranger.SubjectId="OTHER_TOOL";
        var selected=CausalEvidence.RelatedRecords(records.Concat(new[]{stranger}).ToArray(),"SEEN_Cause");
        Assert.That(selected,Does.Contain("COVERAGE"));Assert.That(selected,Does.Contain("RULE"));Assert.That(selected,Does.Not.Contain("OTHER"));
    }
    [Test] public void ActualResolverUsesThePlayersNotebookAndRejectsUnreceivedReferences()
    {
        var ledger=new KnowledgeLedger(new[]{"CH_01","CH_04","CH_06"},"LOOP_01");
        var ids=Evidence().Select(r=>ledger.Observe("CH_01",r,100)).ToArray();
        var claim=new ClaimRecord{Id="CLAIM",OwnerId="CH_01",LoopId="LOOP_01",Spans=new[]{new ClaimSpan{Id="SPAN",Predicate="CausedOutcome",SubjectId="CH_04",Value=CausalEvidence.OutcomeValue("ACT_1","CH_06"),FromTick=30,ToTick=41,Quantifier="Particular"}}};
        var resolver=new LogicResolver();
        var result=resolver.Resolve(ledger.For("CH_01"),"TRY","LR07",claim,"SPAN",ids);
        Assert.That(result.ResultType,Is.EqualTo("Support"),result.ReasonCode);Assert.That(result.ReasonCode,Is.EqualTo("ObservedActionAndLocalJournal"));
        Assert.That(result.AReadCount,Is.Zero);
        Assert.That(resolver.Resolve(ledger.For("CH_06"),"NO_ACCESS","LR07",claim,"SPAN",ids).ReasonCode,Is.EqualTo("AccessDenied"));
    }
}
