using System.Collections;
using System.Linq;
using UnityEngine;
using BASSLINE.NPC;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        IEnumerator ReviewIncidentDialogue()
        {
            driveWorld=false;string npc=FramePlayerNearResident();
            Require(runtime.CanDiscussIncident(npc),"Known player incident did not enable ordinary conversation choices");
            Call(hud,"Primary",npc);yield return FinishLoanSpeech();
            yield return Capture("incident_dialogue_choices",3,1920,1080);
            Click("무슨 일이 있었어?");yield return FinishLoanSpeech();
            Require(runtime.Knowledge.For("CH_01").Records().Any(r=>r.Source==npc&&r.Value=="IncidentObservation"),"Incident answer was not actually heard");
            Click("직접 본 거야?");yield return FinishLoanSpeech();
            var original=runtime.Knowledge.For(npc).Records().Where(r=>r.Direct&&IncidentConversation.Relevant(r)).Select(r=>r.Id).ToArray();
            Click("내 입장 말하기");Click("내가 공격했어.");
            for(int i=0;i<12;i++)runtime.AdvanceOne();
            Require(runtime.ReadConversationPlayback().Speaking&&runtime.ReadConversationPlayback().SpeakerId=="CH_01","Player admission did not use the actual speech system");
            string saved=runtime.SaveSlot();Require(saved=="저장 완료","Save during admission failed: "+saved);
            string loaded=runtime.LoadSlot();Require(loaded=="불러오기 완료","Load during admission failed: "+loaded);Call(hud,"SyncPauseView");
            yield return FinishLoanSpeech();
            Require(runtime.Knowledge.For(npc).Records().Any(r=>r.Source=="CH_01"&&r.Value=="IncidentPosition:Admit"&&r.Predicate=="SaidStatement"),"Listener did not receive the player's admission as a statement");
            Click("내 입장 말하기");Click("내가 공격한 게 아니야.");yield return FinishLoanSpeech();
            Require(runtime.Knowledge.For(npc).Records().Any(r=>r.Source=="CH_01"&&r.Value=="IncidentPosition:Deny"&&r.Predicate=="SaidStatement"),"Listener did not retain the contradictory statement");
            Require(original.All(id=>runtime.Knowledge.For(npc).Find(id)!=null),"Player denial erased a witness observation");
            Require(!runtime.Knowledge.For(npc).Records().Any(r=>r.Source=="CH_01"&&r.Predicate=="CausedOutcome"&&r.Value.StartsWith("IncidentPosition")),"Player's claim became a direct fact");
            yield return Capture("incident_dialogue_reply",3,1920,1080);
            Click("그만 이야기하기");
            receipt.Scope+=" Incident dialogue: real questions and spoken replies, player admission/save/resume and denial, original observation retention. Conversations used nearby test-only framing; no hidden culprit facts were supplied.";
        }
    }
}
