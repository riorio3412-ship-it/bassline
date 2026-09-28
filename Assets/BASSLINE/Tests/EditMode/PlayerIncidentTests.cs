using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
using BASSLINE.Trial;
public sealed class PlayerIncidentTests
{
    static readonly MansionNode[] Nodes={new MansionNode{Id="NODE_A",Room="ROOM_A",Position=default},new MansionNode{Id="NODE_B",Room="ROOM_A",Position=new Point3(0,0,1)}};
    static readonly MansionEdge[] Edges={new MansionEdge{From="NODE_A",To="NODE_B"},new MansionEdge{From="NODE_B",To="NODE_A"}};
    sealed class Physical:IMansionPhysics,IMansionIncidentPhysics
    {
        public MansionWorld World;
        public Point3 Move(string id,Point3 delta)=>World.Resident(id).Position.Plus(delta);
        public bool DoorClear(string id)=>true;public void DoorPose(string id,double open){}
        public bool CanReach(string a,string b,double d)=>true;
        public bool CanSee(string a,string b)=>a=="CH_03"||a=="CH_01";
        public bool Identifies(string a,string b)=>true;
        public bool HasContact(string a,string b,string item,out Point3 point){point=default;return true;}
        public bool ReceivesSpeech(string a,string b)=>CanSee(a,b);
    }
    static (MansionWorld w,MansionIncidentCollection cases,KnowledgeLedger k,Physical p) Fixture()
    {
        var actors=Enumerable.Range(1,18).Select(i=>new ResidentState{Id="CH_"+i.ToString("00"),Node="NODE_A",Position=default}).ToArray();
        var w=new MansionWorld(new MansionState{Residents=actors,Objects=new[]{new MansionObjectState{Id="KNIFE",Name="칼",Position=default}}},Nodes,Edges);
        Assert.That(w.Pickup("KNIFE","CH_01"),Is.EqualTo("Committed"));
        return(w,new MansionIncidentCollection(),new KnowledgeLedger(actors.Select(a=>a.Id)),new Physical{World=w});
    }
    [Test] public void RequiresActualHitAndDoesNotPublishUnseenCulprit()
    {
        var f=Fixture();Assert.That(f.cases.CommitPlayerStrike(f.w,"CH_02","KNIFE",default,f.p),Is.EqualTo("PhysicalContactRequired"));Assert.That(f.cases.Count,Is.Zero);
        f.w.Emit("PlayerWeaponHit","CH_01","CH_02","KNIFE");Assert.That(f.cases.CommitPlayerStrike(f.w,"CH_02","KNIFE",default,f.p),Is.EqualTo("CauseCommitted"));
        var incident=f.cases.All().Single();Assert.That(incident.ReceiptsFor("CH_04"),Is.Empty);Assert.That(incident.ReceiptsFor("CH_03").Any(r=>r.Record.Predicate=="UsedObject"),Is.True);
        var saved=f.w.Capture();f.w=new MansionWorld(saved,Nodes,Edges);f.p.World=f.w;f.cases=MansionIncidentCollection.Restore(f.cases.Capture().Select(s=>UnityEngine.JsonUtility.FromJson<MansionIncidentSnapshot>(UnityEngine.JsonUtility.ToJson(s))).ToArray(),f.w);
        while(f.w.Tick<=MansionIncident.PlayerResultDelay){f.w.Step(default,false,f.p);f.cases.Step(f.w,f.k.For,f.p);}
        incident=f.cases.All().Single();Assert.That(f.w.Resident("CH_02").Alive,Is.False);Assert.That(f.w.CaseBook.AdjudicatedActorId,Is.EqualTo("CH_01"));
        foreach(var r in incident.ReceiptsFor("CH_03"))f.k.Observe("CH_03",r.Record,f.w.Tick);
        Assert.That(new TrialReasoning().ChooseVote(f.k.For("CH_03"),new[]{"CH_01","CH_04"},42).Choice,Is.EqualTo("CH_01"));
        Assert.That(incident.ReceiptsFor("CH_04"),Is.Empty);
        Assert.That(incident.Discover(f.w,"CH_03",f.p),Is.EqualTo("Discovered"));Assert.That(incident.Report(f.w,"CH_03",true),Is.EqualTo("Reported"));Assert.That(incident.Confirm(f.w,true),Is.EqualTo("Confirmed"));
        Assert.DoesNotThrow(()=>MansionIncidentCollection.Restore(f.cases.Capture(),new MansionWorld(f.w.Capture(),Nodes,Edges)));
    }
    [TestCase("CH_01",true)] [TestCase("CH_03",false)]
    public void PlayerCulpritCanBeConvictedOrEscapeWrongVerdict(string voted,bool correct)
    {
        var actors=new[]{"CH_01","CH_03","CH_04","CH_05"};var court=new TrialDirector();court.Start("TRIAL_01","LOOP_01",actors,_=>true);court.ConfigureVoting("Basic",42);court.OpenVoting();foreach(var a in actors)court.ChooseVote(a,voted);court.LockVotes();court.ResolveVoting();
        var ending=new TrialEndgame();Assert.That(ending.CommitVerdict(court,new VerdictInput{CaseId="CASE_PLAYER",CampaignId="CAMPAIGN_01",ChapterId="CHAPTER_01",ActualCulprit="CH_01",LivingResidents=actors,ConfirmedDead=new[]{"CH_02"},CaseBundleClosed=true,DrawSeed=42}),Is.EqualTo("VerdictCommitted"));
        var p=ending.Capture().Plan;Assert.That(p.Correct,Is.EqualTo(correct));Assert.That(p.Executed,Does.Contain(voted));Assert.That(p.Escaped.Contains("CH_01"),Is.EqualTo(!correct));Assert.DoesNotThrow(()=>TrialEndgame.Restore(ending.Capture()));
    }
}
