using System.Collections.Generic;

namespace BL23.Sim
{
    // P02 김진우 — 짧은 반말, 질문으로 되받기. 반응을 시험하다가 진심이 되면 웃음부터 사라진다.
    public static partial class BondScenes
    {
        static partial void Init_P02()
        {
            Add(new BondScene
            {
                Id = "B_P02_1", Npc = "P02", Stage = 1, Title = "투명 사탕",
                Lines =
                {
                    N("P02", "골라 봐. 빨강, 초록, 투명. 아무 의미 없어. …아마도.", Emotion.Smirk),
                    M("의미 없다면서 왜 그렇게 쳐다봐?"),
                    N("P02", "오늘만 열한 명한테 내밀었거든. 투명 고른 사람이 몇 명일 것 같아?"),
                    N("P02", "하나도 없어. 다들 맛이 안 보이는 건 못 믿나 봐. 큭큭.", Emotion.Grin),
                    N("P02", "심리책엔 이런 거 안 나와. 사탕 봉지가 교과서보다 솔직해."),
                },
                Choices =
                {
                    new BondChoice { Label = "그럼 난 투명.", Like = 0.06f, Trust = 0.03f, Attach = 0.04f,
                        Reveal = "note:사탕 색으로 사람을 시험한다. 투명을 고른 사람은 아직 없었다",
                        Reply =
                        {
                            N("P02", "오? 열두 번째 만에 나왔네. 끌려서 고른 거야, 날 이기려고 고른 거야?", Emotion.Surprised),
                            N("P02", "대답하지 마. 둘 다 재밌으니까.", Emotion.Grin),
                        } },
                    new BondChoice { Label = "네가 먹기 싫은 걸로 줘.", Like = 0.05f, Trust = 0.02f, Attach = 0.03f,
                        Reply =
                        {
                            N("P02", "흐응. 남한테 고르게 하는 사람은 둘 중 하나야. 착하거나, 귀찮거나."),
                            N("P02", "넌 앞쪽이라고 해 두자. 초록 먹어. 내가 제일 싫어하는 맛이야.", Emotion.Smile),
                        } },
                    new BondChoice { Label = "사람을 실험 대상으로 보는 거, 별로다.", Like = -0.03f,
                        Reply =
                        {
                            N("P02", "실험 안 하는 사람도 있어? 다들 속으로만 하지.", Emotion.Blank),
                            N("P02", "난 대놓고 할 뿐이야. 그게 더 정직하잖아."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P02_2", Npc = "P02", Stage = 2, Title = "11시 40분",
                Lines =
                {
                    N("P02", "몇 시야? …아, 됐어. 여기 시계는 다 제멋대로지.", Emotion.Blank),
                    N("P02", "밖에 있을 땐 매일 밤 11시 40분이면 폰을 봤어. 버릇이야."),
                    M("그 시간에 뭐가 오는데?"),
                    N("P02", "왔었지. 정치 기사 링크. 욕 반 농담 반 섞어서. 매일 같은 애한테서."),
                    N("P02", "내가 왜 심리학 하냐고? 말 뒤에 뭐가 있는지 알면 덜 다칠 줄 알았거든."),
                    N("P02", "근데 알아도 다치더라. 그건 교과서에 없던데."),
                },
                Choices =
                {
                    new BondChoice { Label = "그 링크에 너도 답장했어?", Like = 0.06f, Trust = 0.04f, Attach = 0.04f, Reveal = "hint:P02",
                        Reply =
                        {
                            N("P02", "…응. 늘 한 줄로. '또 음모론이냐.'", Emotion.Sad),
                            N("P02", "마지막 것도 그렇게 답했어. 큭, 이 얘긴 여기까지.", Emotion.Smirk),
                        } },
                    new BondChoice { Label = "그럼 이제 11시 40분엔 내가 말 걸게.", Like = 0.05f, Trust = 0.02f, Attach = 0.05f,
                        Reply =
                        {
                            N("P02", "오? 대신 욕 반 농담 반 섞어야 돼. 할 수 있어?", Emotion.Grin),
                            N("P02", "…못 하겠지. 넌 너무 다정하게 생겼어."),
                        } },
                    new BondChoice { Label = "괜찮아, 언젠가 다시 연락 올 거야.", Like = -0.04f,
                        Reply =
                        {
                            N("P02", "그거 뻔한 위로야. 내가 제일 싫어하는 종류.", Emotion.Disgust),
                            N("P02", "안 올 걸 아는 사람한테 그 말은, 좀 잔인해."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P02_3", Npc = "P02", Stage = 3, Title = "과거형",
                Lines =
                {
                    N("P02", "소원 얘기 하자. 여기서 소원을 털어놓는 건 동기를 털어놓는 거나 마찬가지야. 알지?", Emotion.Blank),
                    N("P02", "그러니까 이건 시험 아니야. 반대야. 너한테 칼자루를 주는 거지."),
                    M("그럼 천천히 들을게."),
                    N("P02", "친구가 하나 있었어. 있었다고. 아직도 과거형이 입에 안 붙네."),
                    N("P02", "걔를 돌려받고 싶어. 죽은 사람을 살려 달라고 빌었어, 내가.", Emotion.Sad),
                },
                Choices =
                {
                    new BondChoice { Label = "돌려받으면, 제일 먼저 뭐라고 할 거야?", Like = 0.07f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P02",
                        Reply =
                        {
                            N("P02", "…몰라. 웃기지. 할 말도 모르면서 소원부터 빌었어.", Emotion.Sad),
                            N("P02", "마지막에 걔한테 한 말 말고, 다른 거. 뭐든 다른 거.", Emotion.Pain),
                        } },
                    new BondChoice { Label = "그 칼자루, 안 쓸게. 약속해.", Like = 0.05f, Trust = 0.05f, Attach = 0.03f,
                        Reply =
                        {
                            N("P02", "약속은 싸. 근데 네 건 좀 비싸 보이네. 큭.", Emotion.Smile),
                            N("P02", "…믿어 볼게. 이건 실험 아니야."),
                        } },
                    new BondChoice { Label = "죽은 사람을 살리는 게 맞는 소원일까?", Like = -0.03f, Trust = 0.01f,
                        Reply =
                        {
                            N("P02", "맞고 틀리고를 네가 정해?", Emotion.Angry),
                            N("P02", "…방금 건 못 들은 걸로 해. 소원 얘기 끝."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P02_4", Npc = "P02", Stage = 4, Title = "마지막 답장",
                Lines =
                {
                    N("P02", "방금 초록 사탕만 빼고 줬지. 나 초록 싫어하는 거, 너한텐 말한 적 없는데.", Emotion.Surprised),
                    N("P02", "넌 가끔 내 대답을 이미 들은 사람처럼 기다려. 소원 얘기 때도 그랬어."),
                    N("P02", "흐응. 기분 나빠. 좀 좋기도 하고."),
                    M("…말하기 싫으면 안 해도 돼."),
                    N("P02", "아니. 어차피 알 것 같으니까 내가 먼저 말할게. 그래야 덜 지는 거니까.", Emotion.Blank),
                    N("P02", "그날 밤 걔가 물었어. '나 없어지면 아무도 모르겠지?' 난 그 말부터 분석했어."),
                    N("P02", "그래서 답했지. '그런 말은 관심받고 싶을 때 하는 거야.' 교과서처럼."),
                },
                Choices =
                {
                    new BondChoice { Label = "말해 줘서 고마워. 끝까지 들을게.", Like = 0.06f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P02",
                        Reply =
                        {
                            N("P02", "…다음 날 아침에 걔는 없었어. 마지막 답장이 내 거였어.", Emotion.Pain),
                            N("P02", "11시 40분에 온 게 링크가 아니었던 거야. 난 그것도 분석했지.", Emotion.Sad),
                            N("P02", "살려 내도 그 문장은 안 지워져. 알아. 그래도 빌었어."),
                        } },
                    new BondChoice { Label = "너 지금도 내 반응 보고 있지?", Like = 0.04f, Trust = 0.04f, Attach = 0.05f, Reveal = "hint:P02",
                        Reply =
                        {
                            N("P02", "큭. 들켰네. 보고 있어.", Emotion.Smirk),
                            N("P02", "…근데 이번엔 무서워서 보는 거야. 너까지 없어질까 봐."),
                        } },
                    new BondChoice { Label = "네 탓 아니야.", Like = -0.02f, Trust = 0.02f,
                        Reply =
                        {
                            N("P02", "그 말 할 줄 알았어. 넌 착하니까.", Emotion.Blank),
                            N("P02", "근데 탓이 아니면 뭐야. 나한텐 그게 제일 안 풀리는 문제야."),
                        } },
                },
            });
        }
    }
}
