using System;
using UnityEngine;
namespace BASSLINE.Presentation
{
    [Serializable] public sealed class ApprovedBreakOverlay
    {
        public string CueId;
        public string SpeakerId;
        public string BaseArtRevision;
        public string ApprovalRecordId;
        public Sprite Overlay;
    }
    // Optional 3D sprite adapter. Full dialogue Canvas/standing integration remains a later UI phase.
    // Overlay artwork must be a registered, transparent face/eye/hand crop for the same base art.
    public sealed class VisualBreakBehaviour : MonoBehaviour,IVisualBreakSurface
    {
        public SpriteRenderer NormalStanding;
        public SpriteRenderer FaceOverlay;
        public ApprovedBreakOverlay[] ApprovedOverlays=Array.Empty<ApprovedBreakOverlay>();
        public string SpeakerId;
        public string BaseArtRevision;
        public bool ReducedEffects;
        public bool PresentationPaused;
        VisualBreakPlayer player;
        public string CurrentSpeakerId=>SpeakerId;
        void OnEnable(){player=new VisualBreakPlayer(this);ClearOverlay();}
        public BreakResult PlayVisualBreak(string cueId)
        {
            if(player==null)return BreakResult.Disabled;
            player.ReducedEffects=ReducedEffects;player.Paused=PresentationPaused;
            return player.PlayVisualBreak(cueId);
        }
        ApprovedBreakOverlay Find(BreakCue cue)
        {
            ApprovedBreakOverlay result=null;
            foreach(var item in ApprovedOverlays)
                if(item!=null&&item.CueId==cue.Id){if(result!=null)return null;result=item;}
            return result;
        }
        public bool HasApprovedOverlay(BreakCue cue)
        {
            var entry=Find(cue);
            return NormalStanding&&NormalStanding.sprite&&FaceOverlay&&FaceOverlay!=NormalStanding&&
                entry!=null&&entry.Overlay&&entry.SpeakerId==cue.SpeakerId&&
                !string.IsNullOrWhiteSpace(BaseArtRevision)&&entry.BaseArtRevision==BaseArtRevision&&
                !string.IsNullOrWhiteSpace(entry.ApprovalRecordId);
        }
        public void ShowOverlay(BreakCue cue){if(!HasApprovedOverlay(cue))throw new InvalidOperationException("Missing approved BREAK art");FaceOverlay.sprite=Find(cue).Overlay;FaceOverlay.enabled=true;}
        public void ClearOverlay(){if(FaceOverlay&&FaceOverlay!=NormalStanding){FaceOverlay.enabled=false;FaceOverlay.sprite=null;}}
        void Update(){if(player==null)return;player.ReducedEffects=ReducedEffects;player.Paused=PresentationPaused;player.Advance(Time.unscaledDeltaTime);}
        void OnDisable(){player?.Dispose();player=null;ClearOverlay();}
    }
}
