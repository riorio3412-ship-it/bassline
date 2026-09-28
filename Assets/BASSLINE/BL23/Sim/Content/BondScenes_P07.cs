using System.Collections.Generic;

namespace BL23.Sim
{
    // P07 유시온 — 래퍼. 크고 친근한 반말("브로", "야", "레알?", "와우!"). 사과·진심은 작고 짧아진다.
    // 책을 좋아하고 단어를 모은다. 매 문장 랩 금지. 무시와 밀고를 싫어한다.
    public static partial class BondScenes
    {
        static partial void Init_P07()
        {
            Add(new BondScene
            {
                Id = "B_P07_1", Npc = "P07", Stage = 1, Title = "단어 수첩",
                Lines =
                {
                    N("P07", "브로! 딱 좋을 때 왔다. '저택'이랑 라임 되는 단어 하나만 줘 봐.", Emotion.Grin),
                    M("…저녁?"),
                    N("P07", "와우, 반쯤 맞았어! 모음 하나가 달라서 반쯤. 크하하.", Emotion.Laugh),
                    N("P07", "나 책 읽는 거 의외지? 가사는 단어 싸움이라. 모르는 말은 수첩에 적어."),
                    N("P07", "오늘 건진 건 '적막'. 여기 딱이잖아. 근데 이걸로 짜면 다들 잠들어."),
                },
                Choices =
                {
                    new BondChoice { Label = "적막한 저택, 맥주는 적당. 어때?", Like = 0.07f, Trust = 0.02f, Attach = 0.04f,
                        Reveal = "note:가사에 쓸 단어를 수첩에 모은다. 책을 꽤 읽는다",
                        Reply =
                        {
                            N("P07", "레알? 브로, 너 소질 있다! 피처링 자리 하나 비워 둔다.", Emotion.Laugh),
                            N("P07", "근데 '적당'은 빼자. 나 바에 가면 적당히 안 끝나. 가사가 거짓말 돼. 크하하."),
                        } },
                    new BondChoice { Label = "그 수첩, 좀 보여 줘.", Like = 0.04f, Trust = 0.03f, Attach = 0.03f,
                        Reply =
                        {
                            N("P07", "…야, 이건 좀. 뭐, 한 장만.", Emotion.Surprised),
                            N("P07", "첫 장은 안 돼. 거기 적힌 건 옛날 단어라.", Emotion.Smile),
                        } },
                    new BondChoice { Label = "래퍼가 사전 보는 거 좀 웃긴데.", Like = -0.04f,
                        Reply =
                        {
                            N("P07", "…웃겨? 뭐가.", Emotion.Angry),
                            N("P07", "됐어. 무시당하는 거 제일 싫어한다, 나. 기억해 둬."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P07_2", Npc = "P07", Stage = 2, Title = "옥상 관객",
                Lines =
                {
                    N("P07", "야, 수첩 첫 장 궁금하다며. 이거 형들이 가르쳐 준 단어야.", Emotion.Smile),
                    N("P07", "'의리', '빚', '입조심'. 촌스럽지? 동네 옥상에서 맥주 마시면서 배웠어."),
                    N("P07", "내 첫 무대가 그 옥상이었어. 관객 다섯 명. 전부 형들."),
                    M("지금도 연락해?"),
                    N("P07", "…아니. 그 동네 떠났어. 멋있게 떠난 척했는데, 그냥 떠난 거야.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "다섯 명 중에 누가 제일 크게 소리 질렀어?", Like = 0.06f, Trust = 0.05f, Attach = 0.05f, Reveal = "hint:P07",
                        Reply =
                        {
                            N("P07", "…큰형. 박자는 하나도 못 맞추면서 제일 크게.", Emotion.Sad),
                            N("P07", "그 형 지금 안에 있어. 담장 안. …이 얘긴 나중에.", Emotion.Pain),
                        } },
                    new BondChoice { Label = "여기 옥상 하나 만들자. 관객은 내가 할게.", Like = 0.06f, Trust = 0.02f, Attach = 0.05f,
                        Reply =
                        {
                            N("P07", "와우, 레알? 관객 한 명이면 전석 매진이다, 브로.", Emotion.Laugh),
                            N("P07", "…고맙다. 진짜로. 작게 말했으니까 못 들은 척해."),
                        } },
                    new BondChoice { Label = "'입조심'이라니, 좀 무서운 동네네.", Like = -0.02f, Trust = 0.01f,
                        Reply =
                        {
                            N("P07", "무섭지. 근데 그게 규칙이었어. 불면 끝이야.", Emotion.Blank),
                            N("P07", "…이 얘기 재미없다. 딴 얘기 하자, 브로."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P07_3", Npc = "P07", Stage = 3, Title = "틀어진 사람들",
                Lines =
                {
                    N("P07", "브로, 소원 얘기 해도 돼? 웃지 마. 웃으면 삐진다, 진짜로."),
                    N("P07", "내 폰에 '연락 금지'로 저장된 이름이 스물세 개야. 다 상대가 연락하지 말라고 한 사람들."),
                    N("P07", "엄마도 있어. 형들도 있고. 옛날 크루 애들도."),
                    M("스물세 명이나?"),
                    N("P07", "크하하, 인기 많지? …아니, 그냥 전부 내가 틀어지게 만든 거야. 하나씩.", Emotion.Sad),
                },
                Choices =
                {
                    new BondChoice { Label = "그 스물세 명한테 뭘 바라는데?", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P07",
                        Reply =
                        {
                            N("P07", "…다시 잘 지내는 거. 전부. 한 명도 안 빼고.", Emotion.Sad),
                            N("P07", "그게 내 소원이야. 틀어진 사이 전부 되돌리기. 웃기지, 신한테 빈 게 그거라니."),
                            N("P07", "혼자선 스물세 번 사과할 자신이 없어서. 그래서.", Emotion.Pain),
                        } },
                    new BondChoice { Label = "스물네 번째는 나로 해. '연락 가능'으로.", Like = 0.06f, Trust = 0.03f, Attach = 0.06f,
                        Reply =
                        {
                            N("P07", "와우… 야, 반칙이다. 그런 말 하면.", Emotion.Surprised),
                            N("P07", "저장해 둔다. '브로, 연락 가능.' 이름 바꾸면 죽는다. 크하하.", Emotion.Laugh),
                        } },
                    new BondChoice { Label = "네가 틀어지게 만든 거면, 결국 네 탓이네.", Like = -0.04f, Trust = 0.01f,
                        Reply =
                        {
                            N("P07", "…알아. 안다고. 굳이 말 안 해도.", Emotion.Angry),
                            N("P07", "방금 거 잊어. 소원 얘기 안 한 걸로 해."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P07_4", Npc = "P07", Stage = 4, Title = "벽 안의 다섯",
                Lines =
                {
                    N("P07", "브로, 비트 없이 한 줄만 해 본다? '옥상 관객 다섯—'", Emotion.Grin),
                    M("'—지금은 전부 벽 안에.'"),
                    N("P07", "…야. 그거 어떻게 알아.", Emotion.Surprised),
                    N("P07", "그 줄, 무대에서 한 번도 안 했어. 수첩 맨 뒷장에만 있어. 연필로."),
                    N("P07", "너 가끔 그래. 내가 뭐라 할지 알고 먼저 웃어. 소름 돋는데… 싫진 않다."),
                    N("P07", "…그럼 나머지도 알겠네. 모르는 척하지 마. 내가 말할게.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "네 입으로 들을게. 판단은 안 할게.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P07",
                        Reply =
                        {
                            N("P07", "다섯 명 다 들어갔어. 나만 빼고. 이유는… 내가 불었으니까.", Emotion.Pain),
                            N("P07", "형사가 그랬어. 말하면 너는 빼 주겠다고. 무서웠어. 진짜 무서웠어."),
                            N("P07", "밀고하는 놈이 제일 싫다고 했지. 진짜야. 거울 볼 때마다 싫어.", Emotion.Crying),
                        } },
                    new BondChoice { Label = "다섯 명 다 들어갔는데, 너만 밖에 있네.", Like = 0.02f, Trust = 0.03f, Reveal = "hint:P07",
                        Reply =
                        {
                            N("P07", "…그러네. 나만 여기서 너랑 떠들고 있네.", Emotion.Blank),
                            N("P07", "왜 나만 남았는지는 묻지 마. 오늘은."),
                        } },
                    new BondChoice { Label = "혹시 네가 형들 신고한 거야?", Like = -0.05f,
                        Reply =
                        {
                            N("P07", "…야.", Emotion.Angry),
                            N("P07", "그 단어 꺼내지 마. 너한테까지 그 소리 듣기 싫어."),
                        } },
                },
            });
        }
    }
}
