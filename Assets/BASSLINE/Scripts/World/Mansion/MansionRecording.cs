using System;
using System.Collections.Generic;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.World.Mansion
{
    public sealed class RecordingCoverage
    {
        public string Id,PlaceId;
    }
    public interface IMansionRecordingPhysics
    {
        bool Records(string source,string subject);
        bool IdentifiesOnRecording(string source,string actor);
    }
    public interface IMansionActionRecordingPhysics
    {
        ObservedActionDisplay ReadActionDisplay(string source,string device);
    }
    [Serializable] public sealed class RecordedPresence
    {
        public string Source,Subject,Signature;
        public RecordedPresence Copy()=>(RecordedPresence)MemberwiseClone();
    }
    [Serializable] public sealed class RecordedCausalChain
    {
        public string Source,CaseId,Actor,Target;
        public string ActivationId="";
        public long FromTick;
        public bool Continuous=true;
        public RecordedCausalChain Copy()=>(RecordedCausalChain)MemberwiseClone();
    }
    [Serializable] public sealed class MansionRecordingSnapshot
    {
        public int Loop=1;
        public long InstalledTick,LastTick=-1,Sequence;
        public string[] Sources=Array.Empty<string>();
        public KnownRecord[] Clips=Array.Empty<KnownRecord>();
        public RecordedPresence[] Visible=Array.Empty<RecordedPresence>();
        public RecordedCausalChain[] Chains=Array.Empty<RecordedCausalChain>();
        public MansionRecordingSnapshot Copy()=>new MansionRecordingSnapshot{Loop=Loop,InstalledTick=InstalledTick,LastTick=LastTick,Sequence=Sequence,Sources=(string[])Sources.Clone(),Clips=Clips.Select(c=>c.Copy()).ToArray(),Visible=Visible.Select(v=>v.Copy()).ToArray(),Chains=Chains.Select(c=>c.Copy()).ToArray()};
    }
    // Physical media is separate from personal B. Capture has no knowledge writer; reading is a
    // separate, timed physical interaction. The camera never reads private plans or door permissions.
    public sealed class MansionRecording
    {
        readonly RecordingCoverage[] coverage;
        readonly List<KnownRecord> clips=new List<KnownRecord>();
        MansionRecordingSnapshot state;
        public MansionRecording(IEnumerable<RecordingCoverage> definitions,int loop,long installedTick)
        {
            coverage=definitions.ToArray();
            if(loop<1||installedTick<0||coverage.Any(c=>c==null||string.IsNullOrEmpty(c.Id)||string.IsNullOrEmpty(c.PlaceId))||coverage.Select(c=>c.Id).Distinct().Count()!=coverage.Length)throw new ArgumentException("Invalid recording coverage");
            state=new MansionRecordingSnapshot{Loop=loop,InstalledTick=installedTick,Sources=coverage.Select(c=>c.Id).ToArray()};
        }
        public void Step(MansionWorld world,MansionIncidentSnapshot[] cases,IMansionRecordingPhysics physics)
        {
            if(world.Paused||world.Tick==state.LastTick)return;
            if(world.Loop!=state.Loop||world.Tick<state.InstalledTick||world.Tick<state.LastTick)throw new InvalidOperationException("Recording and world clocks differ");
            if(state.LastTick>=0&&world.Tick!=state.LastTick+1){foreach(var chain in state.Chains)chain.Continuous=false;state.Visible=Array.Empty<RecordedPresence>();}
            state.LastTick=world.Tick;
            var visible=new List<RecordedPresence>();
            foreach(var camera in coverage){
                foreach(var actor in world.Residents.Where(a=>a.Present&&physics.Records(camera.Id,a.Id))){
                    bool identity=physics.IdentifiesOnRecording(camera.Id,actor.Id);
                    bool held=actor.HeldObject!=""&&physics.Records(camera.Id,actor.HeldObject);
                    string signature=(identity?"Identified":"Unknown")+"|"+(actor.Alive?"Standing":"Collapsed")+"|"+(held?actor.HeldObject:"");
                    visible.Add(new RecordedPresence{Source=camera.Id,Subject=actor.Id,Signature=signature});
                    if(state.Visible.Any(v=>v.Source==camera.Id&&v.Subject==actor.Id&&v.Signature==signature))continue;
                    Add(camera,identity?actor.Id:"UNKNOWN_ACTOR","AtPlace",actor.Alive?camera.PlaceId:"Collapsed",actor.Alive?"영상의 촬영 구역 안에 인물이 보인다.":"영상의 촬영 구역 안에 쓰러진 인물이 보인다.",world.Tick,world.Tick+1,identity,actor.Position,new[]{"이 화면에 실제 보이는 위치와 모습"});
                    if(held)Add(camera,identity?actor.Id:"UNKNOWN_ACTOR","HeldObject",actor.HeldObject,"영상에서 인물이 "+world.Object(actor.HeldObject).Name+"을 들고 있다.",world.Tick,world.Tick+1,identity,actor.Position,new[]{"촬영 순간 보이는 소지 상태"});
                }
                foreach(var incident in cases.Where(c=>c.Settings!=null&&!c.Settings.ExplicitTestSession&&(c.RiskContinuedTick==world.Tick||c.CauseTick==world.Tick))){
                    var s=incident.Settings;var display=(physics as IMansionActionRecordingPhysics)?.ReadActionDisplay(camera.Id,s.ObjectId);
                    if(display==null||string.IsNullOrEmpty(display.ActivationId)||display.ActivationId!=incident.ActivationId||display.TargetId!=s.TargetId||!physics.Records(camera.Id,s.ActorId)||!physics.Records(camera.Id,s.TargetId)||!physics.Records(camera.Id,s.ObjectId)||!physics.IdentifiesOnRecording(camera.Id,s.ActorId)||!physics.IdentifiesOnRecording(camera.Id,s.TargetId))continue;
                    void RecordStage(string stage,string text){
                        Add(camera,s.ActorId,"UsedObject",s.ObjectId,text+" 작동 번호: "+display.ActivationId,world.Tick,world.Tick+1,true,incident.ContactPoint,new[]{"촬영된 얼굴·동작과 읽을 수 있었던 장치 표시"});
                        var clip=clips.Last();clip.ActivationId=display.ActivationId;clip.DeviceId=s.ObjectId;clip.OutcomeTarget=display.TargetId;clip.ActionDefinition=display.DefinitionId;clip.ActionRevision=display.Revision;clip.CausalStage=stage;
                    }
                    if(incident.RiskContinuedTick==world.Tick)RecordStage("ContactContinued","장치 경고가 표시된 상태에서 같은 인물이 접촉을 계속 유지했다.");
                    if(incident.CauseTick==world.Tick)RecordStage("Cause","같은 인물의 접촉과 장치의 작동 표시를 함께 확인했다.");
                }
                // Only an action occurring NOW can start a recording chain. Installing/loading a
                // camera after the cause cannot reconstruct past footage from authoritative events.
                foreach(var incident in cases.Where(c=>c.Settings!=null&&c.CauseTick==world.Tick)){
                    var s=incident.Settings;
                    if(!physics.Records(camera.Id,s.ActorId)||!physics.Records(camera.Id,s.TargetId)||!physics.Records(camera.Id,s.ObjectId))continue;
                    bool identity=physics.IdentifiesOnRecording(camera.Id,s.ActorId);
                    Add(camera,identity?s.ActorId:"UNKNOWN_ACTOR","UsedObject",s.ObjectId,"영상에 물건이 인물에게 접촉하는 순간이 찍혔다.",world.Tick,world.Tick+1,identity,incident.ContactPoint,new[]{"촬영된 물건과 접촉 동작"});
                    if(identity&&physics.IdentifiesOnRecording(camera.Id,s.TargetId))state.Chains=state.Chains.Concat(new[]{new RecordedCausalChain{Source=camera.Id,CaseId=s.Id,Actor=s.ActorId,Target=s.TargetId,FromTick=world.Tick,ActivationId=clips.LastOrDefault(r=>r.Source==camera.Id&&r.SubjectId==s.ActorId&&r.CausalStage=="Cause"&&r.FromTick==world.Tick)?.ActivationId??""}}).ToArray();
                }
                foreach(var chain in state.Chains.Where(c=>c.Source==camera.Id)){
                    var incident=cases.FirstOrDefault(c=>c.Settings?.Id==chain.CaseId);
                    if(incident==null||!physics.Records(camera.Id,chain.Actor)||!physics.Records(camera.Id,chain.Target)||!physics.IdentifiesOnRecording(camera.Id,chain.Actor)||!physics.IdentifiesOnRecording(camera.Id,chain.Target))chain.Continuous=false;
                    if(incident?.ResultTick!=world.Tick)continue;
                    if(chain.Continuous)Add(camera,chain.Actor,"CausedOutcome",(string.IsNullOrEmpty(chain.ActivationId)?incident.Settings.Template+"_Contact_Then_Collapse":CausalEvidence.OutcomeValue(chain.ActivationId,chain.Target)),"영상에서 같은 인물의 접촉부터 상대가 쓰러질 때까지 끊김 없이 확인된다.",chain.FromTick,world.Tick+1,true,incident.ResultPosition,new[]{"연속 촬영된 신원·접촉·쓰러짐"});
                }
            }
            state.Chains=state.Chains.Where(c=>cases.Any(i=>i.Settings?.Id==c.CaseId&&i.ResultTick<0)).ToArray();state.Visible=visible.ToArray();
        }
        void Add(RecordingCoverage camera,string subject,string predicate,string value,string text,long from,long to,bool identity,Point3 position,string[] supports)
        {
            var clip=new KnownRecord{Id="MEDIA_L"+state.Loop+"_"+(++state.Sequence),LoopId="LOOP_"+state.Loop.ToString("00"),Kind="Video",Source=camera.Id,ProvenanceKey=camera.Id+"_STREAM_L"+state.Loop,SubjectId=subject,Predicate=predicate,Value=value,Text=text,PlaceId=camera.PlaceId,FromTick=from,ToTick=to,ReceivedTick=to-1,IdentityConfirmed=identity,Position=position,Supports=supports,DoesNotEstablish=new[]{"화면 밖 이동·행동·대화·숨은 의도는 알 수 없음","촬영 구역에 보였다는 이유로 유일한 출입자라고 단정할 수 없음","쓰러짐의 관측과 공식 사망 확인은 별개"}};
            clips.Add(clip);
        }
        public KnownRecord[] Read(string source)=>clips.Where(c=>c.Source==source).Select(c=>c.Copy()).ToArray();
        public static bool SameClip(KnownRecord copy,KnownRecord clip)=>CausalEvidence.SameBinding(copy,clip)&&copy.Kind=="Video"&&copy.ProvenanceKey==clip.ProvenanceKey&&copy.SubjectId==clip.SubjectId&&copy.Predicate==clip.Predicate&&copy.Value==clip.Value&&copy.FromTick==clip.FromTick&&copy.ToTick==clip.ToTick;
        public MansionRecordingSnapshot Capture(){var copy=state.Copy();copy.Clips=clips.Select(c=>c.Copy()).ToArray();return copy;}
        public static MansionRecording Restore(MansionRecordingSnapshot snapshot,IEnumerable<RecordingCoverage> definitions,MansionWorld world,MansionIncidentSnapshot[] cases)
        {
            if(snapshot==null||snapshot.Sources==null||snapshot.Clips==null||snapshot.Visible==null||snapshot.Chains==null)throw new ArgumentException("Missing recording state");
            var result=new MansionRecording(definitions,world.Loop,snapshot.InstalledTick);var s=snapshot.Copy();
            if(s.Loop!=world.Loop||s.InstalledTick>world.Tick||s.LastTick>world.Tick||s.LastTick< -1||s.LastTick>=0&&s.LastTick<s.InstalledTick||s.Sequence!=s.Clips.LongLength||!s.Sources.OrderBy(x=>x).SequenceEqual(result.state.Sources.OrderBy(x=>x)))throw new ArgumentException("Recording clock or source mismatch");
            for(int i=0;i<s.Clips.Length;i++){
                var c=s.Clips[i];var source=result.coverage.FirstOrDefault(d=>d.Id==c.Source);
                if(source==null||c.Id!="MEDIA_L"+s.Loop+"_"+(i+1)||c.Kind!="Video"||c.LoopId!="LOOP_"+s.Loop.ToString("00")||c.ProvenanceKey!=c.Source+"_STREAM_L"+s.Loop||c.PlaceId!=source.PlaceId||c.FromTick<s.InstalledTick||c.ToTick<=c.FromTick||c.ToTick>s.LastTick+1||c.ReceivedTick!=c.ToTick-1||!c.Position.Finite()||c.Direct||c.Parents.Length!=0||!new[]{"AtPlace","HeldObject","UsedObject","CausedOutcome"}.Contains(c.Predicate)||c.IdentityConfirmed&&!world.Residents.Any(a=>a.Id==c.SubjectId))throw new ArgumentException("Invalid physical recording clip");
                if(!string.IsNullOrEmpty(c.ActivationId)&&(c.Predicate!="UsedObject"||!c.IdentityConfirmed||c.Value!=c.DeviceId||new[]{c.DeviceId,c.OutcomeTarget,c.ActionDefinition,c.ActionRevision}.Any(string.IsNullOrEmpty)||!new[]{"Cause","ContactContinued"}.Contains(c.CausalStage)))throw new ArgumentException("Invalid recorded display binding");
            }
            if(s.Visible.Any(v=>v==null||!s.Sources.Contains(v.Source)||!world.Residents.Any(a=>a.Id==v.Subject)||string.IsNullOrEmpty(v.Signature))||s.Visible.Select(v=>v.Source+"|"+v.Subject).Distinct().Count()!=s.Visible.Length)throw new ArgumentException("Invalid recorded presence");
            if(s.Chains.Select(c=>c.Source+"|"+c.CaseId).Distinct().Count()!=s.Chains.Length||s.Chains.Any(c=>!s.Sources.Contains(c.Source)||!cases.Any(i=>i.Settings?.Id==c.CaseId&&i.Settings.ActorId==c.Actor&&i.Settings.TargetId==c.Target&&i.CauseTick==c.FromTick&&i.CauseTick>=s.InstalledTick&&i.ResultTick<0)||!s.Clips.Any(r=>r.Source==c.Source&&r.SubjectId==c.Actor&&r.Predicate=="UsedObject"&&r.FromTick==c.FromTick)))throw new ArgumentException("Invalid recorded cause continuity");
            if(s.Chains.Any(c=>!string.IsNullOrEmpty(c.ActivationId)&&!s.Clips.Any(r=>r.Source==c.Source&&r.SubjectId==c.Actor&&r.CausalStage=="Cause"&&r.ActivationId==c.ActivationId&&r.FromTick==c.FromTick)))throw new ArgumentException("Chain references an unread display");
            result.clips.AddRange(s.Clips);s.Clips=Array.Empty<KnownRecord>();result.state=s;return result;
        }
    }
}
