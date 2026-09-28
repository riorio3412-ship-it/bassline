using System;
namespace BASSLINE.Core
{
    public static class WitnessQuestions
    {
        public static readonly string[] Kinds={"When","Where","Direct","Source","Conditions","Compare"};
        public static string Label(string kind)
        {
            switch(kind){case "When":return "언제 확인했어?";case "Where":return "어디에서 확인했어?";case "Direct":return "직접 본 거야?";case "Source":return "누구에게 들었어?";case "Conditions":return "어디까지 확실해?";case "Compare":return "아까 한 말과 비교해 줄래?";default:return "";}
        }
        public static string Resolve(string text)
        {
            foreach(string kind in Kinds)if(kind==text||Label(kind)==text)return kind;
            return text=="직접 확인한 내용을 말해 주세요."?"Direct":"";
        }
    }
    public sealed class WitnessAnswerDraft
    {
        public string State="Unknown";
        public NpcTrialSpeech Speech;
    }
}
