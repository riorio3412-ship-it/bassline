using System.Collections.Generic;

namespace BL23.Sim
{
    // P13 정세나 — 프로게이머. 직접적인 반말, 짧은 감탄("좋아!", "뭐?"), 게임 어휘. 정의감, 빠른 결론.
    // 오른손 의수. 동정을 싫어한다. 의수를 폭력의 동기나 무기로 쓰지 않는다.
    public static partial class BondScenes
    {
        static partial void Init_P13()
        {
            Add(new BondScene
            {
                Id = "B_P13_1", Npc = "P13", Stage = 1, Title = "리플레이",
                Lines =
                {
                    N("P13", "야, 한 판. 봐주면 죽는다. 진짜로.", Emotion.Grin),
                    N("P13", "…좋아! 이겼다. 근데 방금 네 세 번째 판단, 나쁘지 않았어."),
                    N("P13", "리플레이 볼래? 난 진 판보다 이긴 판을 더 오래 봐."),
                    M("이긴 판을 왜 의심해?"),
                    N("P13", "운으로 이긴 건 다음에 져. 반복 연습이 제일 정직해. 한 만큼 나와."),
                },
                Choices =
                {
                    new BondChoice { Label = "한 판 더. 이번엔 내가 리플레이 볼게.", Like = 0.06f, Trust = 0.03f, Attach = 0.04f,
                        Reveal = "note:이긴 판의 리플레이를 더 오래 본다. 운으로 이긴 대목을 찾으려고",
                        Reply =
                        {
                            N("P13", "좋아! 그 자세 좋다. 지는 사람이 음료 셔틀.", Emotion.Laugh),
                            N("P13", "…너랑 하면 져도 기분 안 나쁘겠다. 아, 안 질 거지만."),
                        } },
                    new BondChoice { Label = "왼손으로만 하던데, 원래 왼손잡이야?", Like = 0.02f, Trust = 0.02f, Reveal = "hint:P13",
                        Reply =
                        {
                            N("P13", "…어. 지금은.", Emotion.Blank),
                            N("P13", "그 얘긴 리플레이에 없어. 다음 판."),
                        } },
                    new BondChoice { Label = "게임 가지고 너무 진지한 거 아니야?", Like = -0.04f,
                        Reply =
                        {
                            N("P13", "뭐? 진지하게 안 할 거면 왜 해.", Emotion.Angry),
                            N("P13", "비겁한 것만큼 싫은 게 대충이야. 다음 판 넌 빠져."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P13_2", Npc = "P13", Stage = 2, Title = "휴식 공지",
                Lines =
                {
                    N("P13", "우리 팀 공지 알아? '정세나 선수 휴식.' 그 한 줄이 다야.", Emotion.Smirk),
                    N("P13", "휴식 좋지. 근데 내 자리에 신인이 들어왔어. 반응속도가 나보다 빠른 애."),
                    N("P13", "새벽 세 시 연습실 냄새 알아? 에너지 음료랑 식은 치킨. 그게 그리워."),
                    M("돌아갈 거지?"),
                    N("P13", "솔직히? 돌아갈 거야. …돌아갈 수 있으면.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "'있으면'이라고 한 거, 들었어.", Like = 0.05f, Trust = 0.06f, Attach = 0.04f, Reveal = "hint:P13",
                        Reply =
                        {
                            N("P13", "…하, 귀 밝네.", Emotion.Blank),
                            N("P13", "처음부터 다시 배우는 중이야. 뭘 배우는지는… 동정 안 하면 나중에 말해 줄게."),
                        } },
                    new BondChoice { Label = "그 신인이랑 붙으면 네가 이겨.", Like = 0.06f, Trust = 0.02f, Attach = 0.04f,
                        Reply =
                        {
                            N("P13", "당연하지! 좋아, 그 말 저장.", Emotion.Laugh),
                            N("P13", "…고마워. 근거는 없는데 기분은 좋다."),
                        } },
                    new BondChoice { Label = "쉬는 김에 그만둬도 되잖아.", Like = -0.05f,
                        Reply =
                        {
                            N("P13", "뭐?", Emotion.Angry),
                            N("P13", "지는 게 무서워서 안 하는 거랑 쉬는 건 달라. 너 방금 비겁했어."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P13_3", Npc = "P13", Stage = 3, Title = "손맛",
                Lines =
                {
                    N("P13", "게이머들은 실력을 '손'이라고 해. 손이 풀렸다, 손이 죽었다."),
                    N("P13", "내 소원도 손이야. 잃은 손을 되찾는 거."),
                    M("슬럼프 같은 거야?"),
                    N("P13", "…하. 다들 그렇게 알아듣더라. 그게 편해서 그냥 둬.", Emotion.Smirk),
                    N("P13", "야, 하나만 약속해. 지금부터 무슨 말 들어도 불쌍한 표정 금지."),
                },
                Choices =
                {
                    new BondChoice { Label = "약속할게. 똑바로 볼게.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P13",
                        Reply =
                        {
                            N("P13", "좋아. 말 그대로야. 손. 잃은 손을 되찾는 게 내 소원이야.", Emotion.Blank),
                            N("P13", "슬럼프면 연습으로 돼. 이건 연습으로 안 되는 거라서 빌었어."),
                            N("P13", "…됐어. 거기까지. 표정 합격이다.", Emotion.Smile),
                        } },
                    new BondChoice { Label = "불쌍한 표정이 뭔데? 이거?", Like = 0.04f, Attach = 0.04f,
                        Reply =
                        {
                            N("P13", "하, 그건 배탈 난 표정이잖아.", Emotion.Laugh),
                            N("P13", "…됐다. 웃겨서 말 못 하겠다. 다음에."),
                        } },
                    new BondChoice { Label = "슬럼프면 연습하면 되지. 소원까지 빌어?", Like = -0.05f,
                        Reply =
                        {
                            N("P13", "뭐? 너 지금 결론부터 냈어.", Emotion.Angry),
                            N("P13", "…나도 그 버릇 있어서 알아. 그거 사람 아프게 해."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P13_4", Npc = "P13", Stage = 4, Title = "오른손",
                Lines =
                {
                    N("P13", "잠깐. 이 패드 키 배치… 누가 바꿨어? 왼손으로 다 되게.", Emotion.Surprised),
                    N("P13", "이거 내가 재활하면서 만든 배치야. 팀원들도 몰라. 아무한테도 안 보여 줬어."),
                    N("P13", "넌 가끔 내 약점을 미리 알고 피해 가. 같은 팀 해 본 애처럼. 소름 돋아."),
                    M("…네가 편하게 하는 거 보고 싶었어."),
                    N("P13", "하. 동정이면 지금 말해. 아니면… 보여 줄게.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "동정 아니야. 보여 줘도 돼.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P13",
                        Reply =
                        {
                            N("P13", "…봐. 손목 아래가 없어. 이건 의수야. 무광 흰색, 일상용.", Emotion.Blank),
                            N("P13", "작년 겨울 교통사고. 팀엔 손목 인대라고 했어. 휴식이라고."),
                            N("P13", "왼손으로 처음부터 다시 배웠어. 아직 0.1초 느려. 그 0.1초를 빌었어.", Emotion.Pain),
                        } },
                    new BondChoice { Label = "해린이는 알아?", Like = 0.02f, Trust = 0.03f,
                        Reveal = "hint:P13;note:세나는 해린에게 무언가를 고칠 수 있냐고 자꾸 묻는다",
                        Reply =
                        {
                            N("P13", "…걔는 알아. 봐 달라고 했거든. 된다는 말이 듣고 싶었나 봐.", Emotion.Sad),
                            N("P13", "걔 탓 아니야. 그건 확실히 해 둬."),
                        } },
                    new BondChoice { Label = "힘들었겠다. 많이 아팠지?", Like = -0.04f, Trust = 0.01f,
                        Reply =
                        {
                            N("P13", "…그 표정. 약속했잖아.", Emotion.Angry),
                            N("P13", "아팠지. 근데 그 말 들으려고 보여 준 거 아니야."),
                        } },
                },
            });
        }
    }
}
