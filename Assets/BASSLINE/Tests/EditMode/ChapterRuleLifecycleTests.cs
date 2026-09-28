using System;
using System.Linq;
using BASSLINE.World.Mansion;
using BASSLINE.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class ChapterRuleLifecycleTests
{
    static readonly string[] All=ChapterRules.Catalog().Select(r=>r.Id).ToArray();
    [TestCase(4)][TestCase(5)][TestCase(6)][TestCase(7)][TestCase(8)][TestCase(18)]
    public void HundredChaptersRespectPublicEligibilityCarryCooldownAndPairLimits(int people)
    {
        var state=ChapterRules.Next(null,1,1,people,people-1,All,4512);
        Assert.That(state.Active,Is.Empty);
        for(int chapter=2;chapter<=100;chapter++){
            var before=JsonUtility.ToJson(state);var prior=state;
            state=ChapterRules.Next(prior,1,chapter,people,people-1,All,prior.RandomState);
            Assert.That(JsonUtility.ToJson(prior),Is.EqualTo(before),"Advancing must not rewrite previous history");
            Assert.That(state.Active.Length,Is.LessThanOrEqualTo(chapter==2?1:2));
            Assert.That(state.Active.Select(a=>ChapterRules.Find(a.Id).Type).Distinct().Count(),Is.EqualTo(state.Active.Length));
            Assert.That(state.Active.Count(a=>ChapterRules.Find(a.Id).Major),Is.LessThanOrEqualTo(1));
            Assert.That(state.Active.Count(a=>a.CarryCount>0),Is.LessThanOrEqualTo(1));
            foreach(var a in state.Active){
                Assert.That(ChapterRules.Find(a.Id).MinimumPeople,Is.LessThanOrEqualTo(people));
                if(a.CarryCount>0)Assert.That(prior.Active.Any(p=>p.InstanceId==a.InstanceId&&p.CarryCount==0),Is.True);
                else Assert.That(prior.Active.Any(p=>p.Id==a.Id),Is.False,"An expired ID needs a chapter off");
            }
            if(state.Active.Length==2)Assert.That(ChapterRules.Compatible(state.Active[0].Id,state.Active[1].Id),Is.True);
        }
    }
    [Test] public void SaveRestoreDoesNotRerollAndConsequencesAreDeepCopied()
    {
        var start=ChapterRules.Next(null,1,1,18,17,All,456);
        var second=ChapterRules.Next(start,1,2,18,17,new[]{"CH04"},start.RandomState);
        second.Active[0].ConsequenceRefs=new[]{"C2|CH_04|K_REC_1"};second.Active[0].ReceivedBy=new[]{"CH_01"};
        var loaded=JsonUtility.FromJson<ChapterRulePlan>(JsonUtility.ToJson(second));ChapterRules.Validate(loaded);
        var a=ChapterRules.Next(second,1,3,16,15,All,second.RandomState);
        var b=ChapterRules.Next(loaded,1,3,16,15,All,loaded.RandomState);
        Assert.That(JsonUtility.ToJson(a),Is.EqualTo(JsonUtility.ToJson(b)));
        var carried=a.Active.Single(r=>r.Id=="CH04");Assert.That(carried.InstanceId,Is.EqualTo(second.Active[0].InstanceId));
        Assert.That(carried.ReceivedBy,Is.Empty,"New chapter requires actual reception again");
        carried.ConsequenceRefs[0]="Changed";Assert.That(second.Active[0].ConsequenceRefs[0],Is.EqualTo("C2|CH_04|K_REC_1"));
        var fourth=ChapterRules.Next(b,1,4,14,13,All,b.RandomState);Assert.That(fourth.Active.Any(r=>r.InstanceId==carried.InstanceId),Is.False);
        Assert.That(ChapterRules.Next(fourth,2,1,18,17,All,fourth.RandomState).Active,Is.Empty);
    }
    [Test] public void MissingRuntimeCapabilitiesDoNotActivatePlaceholderRules()
    {
        var s=ChapterRules.Next(null,1,1,18,17,All,1);
        s=ChapterRules.Next(s,1,2,18,17,Array.Empty<string>(),s.RandomState);Assert.That(s.Active,Is.Empty);
        s=ChapterRules.Next(s,1,3,18,17,new[]{"CH01","CH04"},s.RandomState);Assert.That(s.Active.Select(a=>a.Id),Is.EquivalentTo(new[]{"CH01","CH04"}));
    }
    [Test] public void InvalidBoundariesAndTamperedStateAreRejected()
    {
        var s=ChapterRules.Next(null,1,1,18,17,All,4);
        Assert.Throws<ArgumentException>(()=>ChapterRules.Next(s,1,1,18,17,All,4));
        Assert.Throws<ArgumentException>(()=>ChapterRules.Next(s,3,1,18,17,All,4));
        Assert.Throws<ArgumentException>(()=>ChapterRules.Next(s,2,2,18,17,All,4));
        s.Active=new ChapterRuleInstance[]{null};Assert.Throws<ArgumentException>(()=>ChapterRules.Validate(s));
    }
    [Test] public void DefinitionsCannotBeMutatedByReadersAndBannedPairsRemainBanned()
    {
        ChapterRules.Find("CH01").Type="Changed";Assert.That(ChapterRules.Find("CH01").Type,Is.EqualTo("판결"));
        foreach(var pair in new[]{new[]{"CH02","CH03"},new[]{"CH05","CH06"},new[]{"CH06","CH09"}}){Assert.That(ChapterRules.Compatible(pair[0],pair[1]),Is.False);Assert.That(ChapterRules.Compatible(pair[1],pair[0]),Is.False);}
    }
    [Test] public void RecordCategoriesKeepPrivateMapReceiptsOutOfEvidenceAndIncludeReadDocuments()
    {
        var map=new KnownRecord{ProvenanceKey="MAP|room"};Assert.That(PersonalRecordFilter.Includes(map,0),Is.False);Assert.That(PersonalRecordFilter.Includes(map,1),Is.False);
        var document=new KnownRecord{Kind="Document"};Assert.That(PersonalRecordFilter.Includes(document,3),Is.True);Assert.That(PersonalRecordFilter.Includes(document,2),Is.False);
        var speech=new KnownRecord{Kind="Statement",Predicate="SaidStatement",Source="CH_04"};Assert.That(PersonalRecordFilter.Includes(speech,2),Is.True);Assert.That(PersonalRecordFilter.Includes(speech,1),Is.False);
    }
}
