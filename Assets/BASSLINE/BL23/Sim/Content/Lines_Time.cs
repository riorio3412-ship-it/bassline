namespace BL23.Sim
{
    // time-on-demand: lines for a still world (a second chat within the hour gets a friendly "we just talked"), and Yusti's word
    // after a trial when the clock stands still (OnDemand): "아침입니다" only in the morning — at night everyone is sent to bed,
    // by day back to the day (Settlements.Continuation).
    public static partial class LineBank
    {
        static void Init_Time()
        {
            const string A = "ANY";
            Add(A, "talked_recently",
                P("아까도 이야기했잖아요.", "방금 이야기 나눴잖아요. 조금 있다가 또 봐요.", "또 오셨네요? 아까 한 얘기 말고는 딱히…"),
                C("또 왔네?", "아까도 얘기했잖아. 좀 있다 또 보자.", "방금 얘기했잖아. 할 말 더 있어?"));

            const string Y = "NPC00";
            Add(Y, "y_trial_night", P(
                "심판을 마쳤습니다. 밤이 깊었습니다.|모두 방으로 돌아가 쉬십시오. 아침 종이 울리면 문을 다시 열겠습니다.",
                "오늘의 절차는 여기까지입니다.|곧 밤사이 문을 잠급니다. …부디 편히 주무십시오.",
                "심판이 끝났습니다. 각자 방으로 돌아가십시오.|내일 아침, 식당에 식사를 준비해 두겠습니다."));
            Add(Y, "y_trial_day", P(
                "심판을 마쳤습니다. 남은 하루는 각자 보내십시오.|공용 시설은 모두 열려 있습니다.",
                "오늘의 절차는 여기까지입니다.|…저택의 일과는 평소대로 이어집니다.",
                "심판이 끝났습니다.|필요한 것이 있으시면 언제든 저를 부르십시오."));
            // the house's own voice, should Yusti's lines ever be missing
            Add(House, "y_trial_night", P("종소리가 한 번, 길게 울린다. 밤이 깊었다.|모두 말없이 방으로 돌아간다."));
            Add(House, "y_trial_day", P("종소리가 짧게 울린다. 심판이 끝났다.|저택의 하루가 다시 흘러간다."));
        }
    }
}
