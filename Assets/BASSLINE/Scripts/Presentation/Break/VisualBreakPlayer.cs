using System;
using System.Collections.Generic;

namespace BASSLINE.Presentation
{
    public enum BreakResult { Started, UnknownCue, WrongSpeaker, MissingApprovedArt, Busy, ReducedEffects, Disabled }
    public sealed class BreakCue
    {
        public string Id { get; }
        public string SpeakerId { get; }
        public double Seconds { get; }
        internal BreakCue(string id,string speaker,double seconds){Id=id;SpeakerId=speaker;Seconds=seconds;}
    }
    // A surface may expose only the current public speaker and an approved presentation asset.
    // It must not query WorldState, truth, intent, hidden testimony, or private NPC knowledge.
    public interface IVisualBreakSurface
    {
        string CurrentSpeakerId { get; }
        bool HasApprovedOverlay(BreakCue cue);
        void ShowOverlay(BreakCue cue);
        void ClearOverlay();
    }
    public sealed class VisualBreakPlayer : IDisposable
    {
        // User-confirmed allowlist. PRES_YUSTI is a presentation identity, never a participant slot.
        static readonly Dictionary<string,BreakCue> cues=new Dictionary<string,BreakCue>(StringComparer.Ordinal)
        {
            {"Jinwoo_Mocking_01",new BreakCue("Jinwoo_Mocking_01","CH_02",0.85)},
            {"Yusti_Panic_01",new BreakCue("Yusti_Panic_01","PRES_YUSTI",0.35)},
            {"Doyoon_Silence_01",new BreakCue("Doyoon_Silence_01","CH_04",0.9)}
        };
        readonly IVisualBreakSurface surface;
        BreakCue active; double elapsed; bool disabled,reducedEffects;
        public bool Paused {get;set;}
        public bool ReducedEffects {get=>reducedEffects;set{reducedEffects=value;if(value)Cancel();}}
        public string ActiveCueId=>active?.Id;
        public VisualBreakPlayer(IVisualBreakSurface surface){this.surface=surface??throw new ArgumentNullException(nameof(surface));}
        public BreakResult PlayVisualBreak(string cueId)
        {
            if(disabled)return BreakResult.Disabled;
            if(cueId==null||!cues.TryGetValue(cueId,out var cue))return BreakResult.UnknownCue;
            if(surface.CurrentSpeakerId!=cue.SpeakerId)return BreakResult.WrongSpeaker;
            if(reducedEffects)return BreakResult.ReducedEffects;
            if(active!=null)return BreakResult.Busy;
            if(!surface.HasApprovedOverlay(cue))return BreakResult.MissingApprovedArt;
            surface.ShowOverlay(cue);active=cue;elapsed=0;return BreakResult.Started;
        }
        public void Advance(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(active==null)return;
            if(surface.CurrentSpeakerId!=active.SpeakerId||!surface.HasApprovedOverlay(active)){Cancel();return;}
            if(Paused)return;
            elapsed+=seconds;if(elapsed>=active.Seconds)Cancel();
        }
        public void Cancel(){if(active==null)return;active=null;elapsed=0;surface.ClearOverlay();}
        public void Dispose(){Cancel();disabled=true;}
    }
}
