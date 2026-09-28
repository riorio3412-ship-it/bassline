using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
using BASSLINE.Authoring;
using BASSLINE.World;
using BASSLINE.Save;
using System.Text.Json;

class Program
{
    sealed class Codec : ISnapshotCodec
    {
        readonly JsonSerializerOptions options=new JsonSerializerOptions { IncludeFields=true };
        public string Encode(SessionSnapshot snapshot)=>JsonSerializer.Serialize(snapshot,options);
        public SessionSnapshot Decode(string text)=>JsonSerializer.Deserialize<SessionSnapshot>(text,options);
    }
    static (WorldState world,WorldClock clock,PauseCoordinator pause) WorldFixture()
    {
        var pause=new PauseCoordinator();var clock=new WorldClock(pause,60);
        var world=new WorldState(clock,pause,"TEST_01","TestOnly_P0","Bootstrap_001_REV10",new[]{"R_A","R_B"},
            new[]{new ActorRecord {Id="CH_01",RoomId="R_A",X=0,Y=0,Z=0}},
            new[]{new DoorRecord {Id="D_01",RoomA="R_A",RoomB="R_B",X=0,Y=0,Z=1,Width=1.2,AccessPolicy="OwnerPermission",OwnerId="CH_01",LockState="KeyLocked"}});
        return (world,clock,pause);
    }
    static CommandEnvelope Unlock()=>new CommandEnvelope {CommandId="CMD_UNLOCK_01",ActorId="CH_01",TargetId="D_01",Action="RequestUnlock",ExpectedRevision=0,RequestTick=0};
    static int failures;
    static readonly List<string> log = new List<string>();
    static void Test(string id, Action body)
    {
        try {body();log.Add(id+"|PASS");}
        catch(Exception ex){failures++;log.Add(id+"|FAIL|"+ex);}
    }
    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    static void Throws(Action action){bool caught=false;try{action();}catch{caught=true;}Assert(caught,"Expected rejection");}
    static int Main(string[] args)
    {
        var root=Path.GetFullPath(args.Length>0?args[0]:".");var source=Path.Combine(root,"SourcePackage");
        log.Add("Runner=.NET " + Environment.Version + "; Unity=NOT RUN; Editor baseline=6000.6.0f1; Map=M01/TestOnly_P0; Rule=Bootstrap_001_REV10; seed=0; Revision="+File.ReadAllText(Path.Combine(root,"Verification/build-revision.txt")).Trim()+"");
        Test("CSV-01",()=>{var rows=CsvTable.Read("\uFEFFID,Name\r\nA,\"한글, \"\"인용\"\"\n둘째 줄\"\r\n");Assert(rows.Count==1&&rows[0]["Name"]=="한글, \"인용\"\n둘째 줄","CSV roundtrip");});
        Test("CSV-02",()=>{Throws(()=>CsvTable.Read("A,A\nx,y"));Throws(()=>CsvTable.Read("A,B\nx"));Throws(()=>CsvTable.Read("A\n\"broken"));Throws(()=>CsvTable.Read("A\n\"x\"y"));});
        Test("ID-01",()=>{Throws(()=>new StableId("김민혁"));Throws(()=>new StableId("World/CH_01"));Assert(new StableId("CH_01").Equals(new StableId("CH_01")),"ID equality");});
        Test("AT-TIME-02.Domain",()=>{
            var pause=new PauseCoordinator();var clock=new WorldClock(pause,60);
            pause.Acquire(new StableId("FOCUS_01"),ClockScope.Court);Assert(clock.Step(),"Court-only must not imply World pause");
            pause.Acquire(new StableId("NOTE_01"),ClockScope.Both);pause.Acquire(new StableId("SETTINGS_01"),ClockScope.Both);
            Assert(!clock.Step(),"World paused");pause.Release(new StableId("NOTE_01"));Assert(!clock.Step(),"Nested settings retained");
            var restored=PauseCoordinator.Restore(pause.Capture());Assert(restored.IsPaused(ClockScope.Both),"Semantic pause restore");
            pause.Release(new StableId("SETTINGS_01"));Assert(clock.Step()&&clock.Tick==2,"No catch-up ticks");Assert(pause.IsPaused(ClockScope.Court),"Focus retained");
        });
        var data=PackageData.Load(source);
        var diagnostics=PackageValidator.Validate(data,root);
        Directory.CreateDirectory(Path.Combine(root,"Verification"));
        File.WriteAllLines(Path.Combine(root,"Verification/manifest-diagnostics.txt"),diagnostics.Select(x=>x.ToString()));
        Test("AT-SPACE-01.Static",()=>Assert(!diagnostics.Any(x=>x.Severity=="ERROR"),"Manifest errors: "+string.Join("; ",diagnostics.Where(x=>x.Severity=="ERROR"))));
        Test("VAL-DUPLICATE",()=>{var d=PackageData.Load(source);d.Rooms.Add(d.Rooms[0]);Assert(PackageValidator.Validate(d,root).Any(x=>x.Code=="DUPLICATE_ID"),"Duplicate not diagnosed");});
        Test("VAL-FK",()=>{var d=PackageData.Load(source);d.Connections[0].Fields["RoomB"]="R_MISSING";Assert(PackageValidator.Validate(d,root).Any(x=>x.Code=="UNKNOWN_FK"),"FK not diagnosed");});
        Test("VAL-BOUNDARY",()=>{var d=PackageData.Load(source);d.Connections[0].Fields["X"]="100";Assert(PackageValidator.Validate(d,root).Any(x=>x.Code=="DOOR_BOUNDARY"),"Invalid doorway accepted");});
        Test("VAL-CYCLE",()=>{var d=PackageData.Load(source);d.Rooms[0].Fields["ParentRoomID"]=d.Rooms[1]["RoomID"];d.Rooms[1].Fields["ParentRoomID"]=d.Rooms[0]["RoomID"];Assert(PackageValidator.Validate(d,root).Any(x=>x.Code=="ROOM_PARENT_CYCLE"),"Cycle not diagnosed");});
        Test("VAL-NONFINITE",()=>{var d=PackageData.Load(source);d.Rooms[0].Fields["Width"]="NaN";Throws(()=>PackageValidator.Validate(d,root));});
        Test("P0-COMMAND-IDEMPOTENCY",()=>{var f=WorldFixture();var c=Unlock();var first=f.world.Submit(c);Assert(first.Status==CommandStatus.Committed,"Unlock failed");first.EventIds[0]="EV_MUTATED";Assert(f.world.Submit(c).EventIds[0]!="EV_MUTATED","Result escaped mutably");Assert(f.world.Capture().Events.Length==1,"Duplicate event");Assert(f.world.Capture().Doors[0].OpenState=="Closed","Unlock must not open");c.Action="Teleport";Assert(f.world.Submit(c).ReasonCode=="CommandIdConflict","Reused ID must reject different payload");});
        Test("P0-REJECT-ATOMIC",()=>{var f=WorldFixture();var c=Unlock();c.ExpectedRevision=7;Assert(f.world.Submit(c).Status==CommandStatus.Rejected,"Stale accepted");Assert(f.world.Capture().Events.Length==0&&f.world.Capture().Doors[0].LockState=="KeyLocked","Partial mutation");c.CommandId="CMD_UNIMPLEMENTED";c.ExpectedRevision=0;c.Action="RequestPassage";Assert(f.world.Submit(c).ReasonCode=="NotImplemented","Placeholder pretended success");});
        Test("P0-SNAPSHOT-IMMUTABILITY",()=>{var f=WorldFixture();var s=f.world.Capture();s.Actors[0].X=99;Assert(f.world.Capture().Actors[0].X==0,"Snapshot leaked mutable state");});
        Test("AT-SAVE-02.Domain",()=>{var f=WorldFixture();f.world.Submit(Unlock());f.pause.Acquire(new StableId("NOTE_01"),ClockScope.Both);var codec=new Codec();var s=new SessionSnapshot {CatalogHash="test",WorldTick=f.clock.Tick,TickRate=60,Pause=f.pause.Capture(),World=f.world.Capture()};var text=codec.Encode(s);var decoded=codec.Decode(text);var p=PauseCoordinator.Restore(decoded.Pause);var clock=new WorldClock(p,60,decoded.WorldTick);var restored=WorldState.Restore(decoded.World,clock,p,"TestOnly_P0","Bootstrap_001_REV10");Assert(codec.Encode(new SessionSnapshot {CatalogHash="test",WorldTick=clock.Tick,TickRate=60,Pause=p.Capture(),World=restored.Capture()})==text,"Restored snapshot differs");Assert(restored.Submit(Unlock()).Status==CommandStatus.Committed&&restored.Capture().Events.Length==1,"Loaded command duplicated");Assert(!clock.Step(),"Pause lost");});
        Test("AT-SAVE-01.File",()=>{
            var f=WorldFixture();var store=new AtomicSaveStore(new Codec());var s=new SessionSnapshot {CatalogHash="test",WorldTick=0,TickRate=60,Pause=f.pause.Capture(),World=f.world.Capture()};
            var path=Path.Combine(root,"Tests/obj/save-fixture/slot.dat");Directory.CreateDirectory(Path.GetDirectoryName(path));store.Save(path,s);var original=File.ReadAllText(path);
            f.world.Submit(Unlock());s.World=f.world.Capture();Throws(()=>store.Save(path,s,c=>{if(c==SaveCheckpoint.BeforeReplace)throw new IOException("Injected write interruption");}));Assert(File.ReadAllText(path)==original,"Old save lost on interrupted write");
            store.Save(path,s);Assert(store.Load(path,"test","TestOnly_P0","Bootstrap_001_REV10").World.Revision==1,"New save not committed");Assert(File.ReadAllText(path+".previous")==original,"Previous valid save lost");
            File.AppendAllText(path,"corrupt");Throws(()=>store.Load(path,"test","TestOnly_P0","Bootstrap_001_REV10"));Assert(store.Load(path+".previous","test","TestOnly_P0","Bootstrap_001_REV10").World.Revision==0,"Explicit recovery failed");
        });
        Test("AT-SAVE-04.Domain",()=>{var f=WorldFixture();Throws(()=>WorldState.Restore(f.world.Capture(),f.clock,f.pause,"M01","Bootstrap_001_REV10"));var s=f.world.Capture();s.Actors[0].RoomId="R_MISSING";Throws(()=>WorldState.Restore(s,f.clock,f.pause,"TestOnly_P0","Bootstrap_001_REV10"));});
        log.Add("Manifest counts: rooms="+data.Rooms.Count+", connections="+data.Connections.Count+", anchors="+data.Anchors.Count+", seats="+data.Seats.Count+", characters="+data.Characters.Count+", screens="+data.Screens.Count+", assets="+data.Assets.Count);
        log.Add("Diagnostics: errors="+diagnostics.Count(x=>x.Severity=="ERROR")+", warnings="+diagnostics.Count(x=>x.Severity=="WARNING"));
        log.Add("Failed="+failures);File.WriteAllLines(Path.Combine(root,"Verification/domain-test-results.txt"),log);
        foreach(var line in log)Console.WriteLine(line);return failures==0?0:1;
    }
}
