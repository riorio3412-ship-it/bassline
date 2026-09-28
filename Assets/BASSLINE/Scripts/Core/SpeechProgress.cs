using System;
namespace BASSLINE.Core
{
    // One cursor implementation for player conversations and simultaneous resident conversations.
    public static class SpeechProgress
    {
        public static void Advance(ConversationPlaybackState s,long tick,Func<string,string,bool> canHear)
        {
            s.ElapsedTicks++;
            int through=(int)((long)s.PlannedText.Length*s.ElapsedTicks/s.DurationTicks);
            if(through<=s.EmittedCharacters)return;
            string fragment=s.PlannedText.Substring(s.EmittedCharacters,through-s.EmittedCharacters);
            foreach(var listener in s.Listeners){
                if(canHear(listener.ActorId,s.SpeakerId)){
                    if(listener.Gap)listener.HeardText+=listener.HeardCharacters>0?" … ":"… ";
                    listener.HeardText+=fragment;listener.HeardCharacters+=fragment.Length;listener.Gap=false;
                    if(listener.FirstHeardTick<0)listener.FirstHeardTick=tick;listener.LastHeardTick=tick;
                }else listener.Gap=true;
            }
            s.EmittedCharacters=through;
        }
    }
}
