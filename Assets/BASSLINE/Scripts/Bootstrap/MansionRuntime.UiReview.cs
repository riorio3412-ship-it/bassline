#if UNITY_EDITOR || DEBUG
using System;
using System.Linq;
using UnityEngine;
using BASSLINE.Trial;
using BASSLINE.Investigation;

namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        // Explicit presentation fixture. It does not assert physical summons, seating or testimony.
        // A real player-owned observation supplies the evidence; no personal memory is injected.
        public string ConfigureTrialUiReview()
        {
            if(!Application.isEditor&&(!Debug.isDebugBuild||Array.IndexOf(Environment.GetCommandLineArgs(),"-bassline-mansion-smoke")<0))
                throw new InvalidOperationException("Explicit UI review session required");
            if(court.Phase!="NotStarted"||incidents.Count!=0)throw new InvalidOperationException("Fresh review session required");
            var record=Knowledge.For("CH_01").Records().FirstOrDefault(r=>r.Direct&&r.Predicate=="AtPlace"&&r.IdentityConfirmed&&r.SubjectId.StartsWith("CH_",StringComparison.Ordinal));
            if(record==null)throw new InvalidOperationException("Observe a participant before reviewing the trial UI");
            UseIsolatedTestStorage();
            var participants=World.Residents.Where(r=>r.Alive&&r.Present).Select(r=>r.Id).ToArray();
            if(court.Start("TESTONLY_UI_REVIEW",Knowledge.LoopId,participants,_=>true)!="Started")throw new InvalidOperationException("UI review court did not start");
            var claim=new ClaimRecord{Id="TESTONLY_UI_CLAIM",OwnerId="CH_04",LoopId=Knowledge.LoopId,
                Text=NameOf(record.SubjectId)+" 씨가 그 시각에는 "+PlaceLabel(record.Value)+"에 있었어요.",
                Spans=new[]{new ClaimSpan{Id="TESTONLY_UI_SPAN",SubjectId=record.SubjectId,Predicate=record.Predicate,Value=record.Value,PlaceId=record.PlaceId,FromTick=record.FromTick,ToTick=record.ToTick}}};
            if(court.QueueSpeech(new SpeechDraft{Id="TESTONLY_UI_SPEECH",Speaker="CH_04",Topic="위치 확인",Text=claim.Text,Claim=claim})!="Queued")throw new InvalidOperationException("UI review speech unavailable");
            for(int i=0;i<claim.Text.Length*3+6;i++)court.Step(_=>true);
            if(court.Read("CH_01",false).ActiveClaims.Length!=1)throw new InvalidOperationException("UI review utterance not completed");
            proceedings.Phase="Debate";World.Pause("M_COURT",true);move=default;running=false;
            return record.Id;
        }
    }
}
#endif
