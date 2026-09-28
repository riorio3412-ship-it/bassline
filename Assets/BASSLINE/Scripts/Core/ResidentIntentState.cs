using System;
using System.Linq;
namespace BASSLINE.Core
{
    [Serializable] public sealed class ResidentIntentState
    {
        public IncidentPlanState Plan;
        public string PurposeId="";
        public ResidentPurposeBinding Purpose;
        public string SourceExperienceId="",ContextId="",ItemId="",Phase="Selected",RecipientId="",LocationRecordId="",ProofRecordId="",DestinationNode="",Outcome="";
        public long NextPlanTick,DeadlineTick,CompletedTick=-1;public int Attempts;
        public ConversationPlaybackState Speech=new ConversationPlaybackState();
        public ConversationPlaybackState Reply=new ConversationPlaybackState();
        public ResidentIntentState Copy(){var c=(ResidentIntentState)MemberwiseClone();c.Plan=Plan.Copy();c.Purpose=string.IsNullOrEmpty(PurposeId)?null:Purpose?.Copy();c.Speech=Speech.Copy();c.Reply=Reply.Copy();return c;}
    }
    [Serializable] public sealed class ResidentIntentRandom
    {
        public string OwnerId="";public uint State=1;
        public ResidentIntentRandom Copy()=>(ResidentIntentRandom)MemberwiseClone();
    }
    [Serializable] public sealed class ResidentIntentSnapshot
    {
        public int Sequence;
        public ResidentIntentState[] Intents=Array.Empty<ResidentIntentState>();
        public ResidentIntentRandom[] Random=Array.Empty<ResidentIntentRandom>();
        public PurposeReadingProgress[] PurposeReadings=Array.Empty<PurposeReadingProgress>();
        public ResidentIntentSnapshot Copy()=>new ResidentIntentSnapshot{Sequence=Sequence,Intents=Intents.Select(x=>x.Copy()).ToArray(),Random=Random.Select(x=>x.Copy()).ToArray(),PurposeReadings=(PurposeReadings??Array.Empty<PurposeReadingProgress>()).Select(x=>x.Copy()).ToArray()};
    }
    [Serializable] public sealed class PurposeReadingProgress
    {
        public string OwnerId="",PlateId="",DefinitionId="",Revision="",Text="";
        public long StartedTick,LastTick;public int ElapsedTicks;
        public PurposeReadingProgress Copy()=>(PurposeReadingProgress)MemberwiseClone();
    }
}
