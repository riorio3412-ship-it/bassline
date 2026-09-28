using BASSLINE.Core;
namespace BASSLINE.NPC
{
 // Only an actor's own activity count enters this planner. No World/Save references.
 public sealed class LifePlanner
 {
  public LifeRequest Choose(PersonalLifeView own)
  {
   if(!own.Available)return null;
   string[] places={"H","W","L","G"};
   int offset=own.ActorId=="CH_02"?1:own.ActorId=="CH_03"?2:own.ActorId=="CH_04"?3:own.ActorId=="CH_11"?0:1;
   string room=places[(offset+own.CompletedActivities)%places.Length];
   return new LifeRequest{AnchorId="K_SEAT_"+room+"_"+(own.ActorId=="CH_03"||own.ActorId=="CH_18"?"02":"01"),ActivityId=room=="L"?"ACT_READ":room=="W"?"ACT_WORK":room=="G"?"ACT_REST":"ACT_DRINK"};
  }
 }
}
