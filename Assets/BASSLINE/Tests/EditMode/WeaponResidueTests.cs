using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
using BASSLINE.Knowledge;
using BASSLINE.Investigation;
public sealed class WeaponResidueTests
{
    static readonly MansionNode[] Nodes={new MansionNode{Id="NODE_A",Room="ROOM_A",Position=default}};
    sealed class PhysicsPort:IMansionPhysics,IMansionIncidentPhysics
    {
        public Point3 Move(string a,Point3 p)=>p;public bool DoorClear(string d)=>true;public void DoorPose(string d,double n){}
        public bool CanReach(string a,string b,double d)=>true;public bool CanSee(string a,string b)=>true;public bool Identifies(string a,string b)=>true;public bool ReceivesSpeech(string a,string b)=>true;
        public bool HasContact(string a,string b,string o,out Point3 p){p=default;return true;}
    }
    static (MansionWorld world,MansionIncident incident,WeaponResidues residue) Fixture()
    {
        var w=new MansionWorld(new MansionState{Residents=Enumerable.Range(1,18).Select(i=>new ResidentState{Id="CH_"+i.ToString("00"),Node="NODE_A"}).ToArray(),Objects=new[]{new MansionObjectState{Id="KNIFE",Name="칼"}}},Nodes,Array.Empty<MansionEdge>());
        w.Pickup("KNIFE","CH_01");var incident=new MansionIncident();var residue=new WeaponResidues(w.Loop,w.Tick);w.Emit("PlayerWeaponHit","CH_01","CH_02","KNIFE");
        Assert.That(incident.CommitPlayerStrike(w,"CH_02","KNIFE",default,new PhysicsPort()),Is.EqualTo("CauseCommitted"));return(w,incident,residue);
    }
    [Test] public void StainRequiresCurrentPhysicalCauseAndCannotBeDepositedTwice()
    {
        var f=Fixture();Assert.That(f.residue.Deposit(f.world,f.incident.Capture()),Is.True);Assert.That(f.residue.Deposit(f.world,f.incident.Capture()),Is.False);
        f.world.Step(default,false,new PhysicsPort());Assert.That(new WeaponResidues(1,f.world.Tick).Deposit(f.world,f.incident.Capture()),Is.False,"Migration must not invent old stains");
        Assert.That(f.residue.For("KNIFE").Single().Appearance,Is.EqualTo("RedStain"));
    }
    [Test] public void RinseRequiresPhysicalEventAndPreservesOriginAcrossSerialization()
    {
        var f=Fixture();f.residue.Deposit(f.world,f.incident.Capture());var before=f.residue.For("KNIFE").Single();
        Assert.Throws<ArgumentException>(()=>f.residue.Rinse(f.world,"KNIFE",1));
        f.world.Step(default,false,new PhysicsPort());f.world.Emit("WeaponRinsed","CH_01","KNIFE","TAP");f.residue.Rinse(f.world,"KNIFE",f.world.Events.Last().Sequence);
        var json=UnityEngine.JsonUtility.ToJson(f.residue.Capture());var saved=UnityEngine.JsonUtility.FromJson<WeaponResidueSnapshot>(json);var restored=WeaponResidues.Restore(saved,f.world.Capture(),new[]{"KNIFE"});
        var after=restored.For("KNIFE").Single();Assert.That(after.Appearance,Is.EqualTo("FaintRedStain"));Assert.That(after.Id,Is.EqualTo(before.Id));Assert.That(after.CauseSequence,Is.EqualTo(before.CauseSequence));
        saved.Marks[0].RinseSequence=0;Assert.Throws<ArgumentException>(()=>WeaponResidues.Restore(saved,f.world.Capture(),new[]{"KNIFE"}));
    }
    [Test] public void SurfaceResidueCannotIdentifyAnAttackerOrProveACause()
    {
        var ledger=new KnowledgeLedger(new[]{"CH_01","CH_02"});
        var r=new KnownRecord{Kind="Visual",SubjectId="KNIFE",Predicate="SurfaceResidue",Value="RedStain",Source="CH_01",ProvenanceKey="WEAPON_SURFACE",FromTick=0,ToTick=1,Text="붉은 얼룩"};
        var id=ledger.Observe("CH_01",r,0);var own=ledger.For("CH_01");
        Assert.That(PhysicalEvidenceScope.IsObjectState(own.Find(id)),Is.True);
        var claim=new ClaimRecord{Id="C",OwnerId="CH_02",LoopId=own.LoopId,Spans=new[]{new ClaimSpan{Id="S",SubjectId="CH_02",Predicate="CausedOutcome",Value="Dead",FromTick=0,ToTick=1}}};
        var result=new LogicResolver().Resolve(own,"REVIEW","LR07",claim,"S",new[]{id});Assert.That(result.ResultType,Is.Not.EqualTo("Support"));Assert.That(result.AReadCount,Is.Zero);
    }
}
