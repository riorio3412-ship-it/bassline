using System;
using System.Linq;
namespace BASSLINE.Trial
{
    public sealed partial class TrialDirector
    {
        public string QueueFinalExplanation(SpeechDraft explanation)
        {
            if(state.Phase!="Debate"||state.Focus!=null||JointArgumentBusy||explanation?.Claim==null||state.Examinations.Any(e=>new[]{"QuestionQueued","AwaitingAnswer","AnswerQueued"}.Contains(e.Phase)))return "Unavailable";
            string result=QueueSpeech(explanation);if(result!="Queued")return result;
            // Finish the current utterance. Unspoken ordinary turns are held, not converted
            // into testimony, so the final explanation cannot deadlock behind three old claims.
            var current=state.VoiceCursor>0?state.Pending.First():null;
            state.DeferredSpeeches=state.DeferredSpeeches.Concat(state.Pending.Where(s=>s.Id!=explanation.Id&&s!=current).Select(s=>s.Copy())).ToArray();
            state.Pending=(current==null?Array.Empty<SpeechDraft>():new[]{current}).Concat(new[]{explanation.Copy()}).ToArray();
            state.ActiveClaimIds=Array.Empty<string>();return "Queued";
        }
        public string ResumeOrdinaryDebate()
        {
            if(state.Phase!="Debate"||state.Focus!=null)return "Unavailable";
            state.Pending=state.Pending.Concat(state.DeferredSpeeches.Select(s=>s.Copy())).ToArray();state.DeferredSpeeches=Array.Empty<SpeechDraft>();return "Resumed";
        }
    }
}
