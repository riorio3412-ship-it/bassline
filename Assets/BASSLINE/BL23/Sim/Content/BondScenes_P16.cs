using System.Collections.Generic;

namespace BL23.Sim
{
    // P16 신채령 — 스타일리스트. 느긋한 반말, 짧은 병렬 문장, 건조한 비꼼과 취향 평가.
    // 경계를 지킨다(내 물건·내 결정). 실패는 밖으로 돌린다. 진심의 칭찬 앞에선 시선을 피한다.
    public static partial class BondScenes
    {
        static partial void Init_P16()
        {
            Add(new BondScene
            {
                Id = "B_P16_1", Npc = "P16", Stage = 1, Title = "단추 상자",
                Lines =
                {
                    N("P16", "움직이지 마. 네 재킷 두 번째 단추. 실밥 나왔어."),
                    N("P16", "…됐어. 떼진 않았어. 내 물건 아니니까."),
                    N("P16", "여기 옷장 단추들 봤어? 다 제각각이야. 촌스러운 거, 예쁜 거, 이 빠진 거."),
                    N("P16", "하나씩 모으는 중이야. 주인 허락은 안 받았어. 주인이 없거든.", Emotion.Smirk),
                    M("제일 예쁜 건 뭔데요?"),
                    N("P16", "이거. 자개 단추. 풋, 네 눈엔 다 똑같지?"),
                },
                Choices =
                {
                    new BondChoice { Label = "아니요, 이게 제일 반짝여요. 빛이 안에서 나와요.", Like = 0.06f, Trust = 0.02f, Attach = 0.04f,
                        Reveal = "note:옷장 단추를 모은다. 자개 단추를 제일 아낀다",
                        Reply =
                        {
                            N("P16", "…보는 눈은 있네. 별로 기대 안 했는데.", Emotion.Surprised),
                            N("P16", "하나 줄게. 아니, 빌려줄게. 날짜 적어 둔다."),
                        } },
                    new BondChoice { Label = "제 재킷도 좀 봐 줄래요?", Like = 0.04f, Trust = 0.02f, Attach = 0.03f,
                        Reply =
                        {
                            N("P16", "공짜 아닌데. 뭐, 오늘은 기분이 괜찮으니까.", Emotion.Smirk),
                            N("P16", "소매 한 번 접어. 그거면 반은 살아. 나머진 가망 없고."),
                        } },
                    new BondChoice { Label = "주인 없다고 막 가져가도 돼요?", Like = -0.04f,
                        Reply =
                        {
                            N("P16", "간섭하지 마. 내 결정이야.", Emotion.Angry),
                            N("P16", "넌 네 단추나 챙겨. 떨어지기 직전이니까."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P16_2", Npc = "P16", Stage = 2, Title = "쇼윈도",
                Lines =
                {
                    N("P16", "갤러리 조명, 별로야. 그림이 불쌍해. 내 가게였으면 다 갈았어.", Emotion.Disgust),
                    N("P16", "가게? 있었어. 편집숍. 쇼윈도가 골목에서 제일 예뻤지."),
                    N("P16", "망했어. 거래처가 배신하고, 건물주가 욕심내고. 운이 나빴어. 그게 다야.", Emotion.Blank),
                    M("아까부터 만지는 그 열쇠는 뭐야?"),
                    N("P16", "…가게 열쇠. 문은 이제 없는데. 버리기 귀찮아서. 그뿐이야."),
                },
                Choices =
                {
                    new BondChoice { Label = "귀찮아서 안 버리는 거, 아끼는 거랑 비슷하던데.", Like = 0.05f, Trust = 0.05f, Attach = 0.04f, Reveal = "hint:P16",
                        Reply =
                        {
                            N("P16", "…말 많네.", Emotion.Blank),
                            N("P16", "마네킹 옷은 매주 내가 갈아입혔어. 그 골목에서 날 기억하는 건 그 유리뿐일걸.", Emotion.Sad),
                        } },
                    new BondChoice { Label = "다음 가게 첫 손님은 내가 할게.", Like = 0.05f, Trust = 0.02f, Attach = 0.05f,
                        Reply =
                        {
                            N("P16", "흥. 첫 손님은 까다로워야 돼. 너 할 수 있겠어?", Emotion.Smirk),
                            N("P16", "…뭐, 어쨌든. 이름은 적어 둔다."),
                        } },
                    new BondChoice { Label = "진짜 운만 나빴던 거야?", Like = -0.04f,
                        Reply =
                        {
                            N("P16", "간섭하지 마.", Emotion.Angry),
                            N("P16", "내 가게 얘기는 내가 정해. 넌 손님도 아니었잖아."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P16_3", Npc = "P16", Stage = 3, Title = "간판",
                Lines =
                {
                    N("P16", "소원? 내 가게. 이번엔 망할 리 없는 걸로."),
                    N("P16", "…이 말, 벌써 여러 번 했지. 됐어. 오늘은 좀 더 말해 줄게."),
                    N("P16", "다시 시작하는 거 말고. 처음부터 성공해 있는 거. 간판에 불 켜진 채로."),
                    M("다시 시작하는 건 왜 싫어?"),
                    N("P16", "다시 시작하면… 또 내가 정해야 하잖아. 전부.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "그 말, 무거워 보여. 더 들어도 돼?", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P16",
                        Reply =
                        {
                            N("P16", "성공한 사업체. 그걸 통째로 갖는 게 내 소원이야.", Emotion.Blank),
                            N("P16", "간판, 직원, 단골. 다 갖춰진 채로. 내가 망칠 틈이 없게."),
                            N("P16", "…방금 거 못 들은 걸로 해. 마지막 문장만.", Emotion.Sad),
                        } },
                    new BondChoice { Label = "불 켜진 간판 밑에서 사진 찍어 줄게.", Like = 0.04f, Attach = 0.04f,
                        Reply =
                        {
                            N("P16", "촌스러운 포즈는 안 해.", Emotion.Smirk),
                            N("P16", "…풋. 한 장은 해 줄게. 한 장만."),
                        } },
                    new BondChoice { Label = "처음부터 성공한 거면, 그건 네 가게가 아니잖아.", Like = -0.05f,
                        Reply =
                        {
                            N("P16", "재밌어? 난 별로.", Emotion.Angry),
                            N("P16", "내 거라고 하면 내 거야. 대화 끝."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P16_4", Npc = "P16", Stage = 4, Title = "주머니 속 오리",
                Lines =
                {
                    N("P16", "…그거 어디서 났어. 내 주머니에 있던 거잖아. 오리.", Emotion.Surprised),
                    N("P16", "떨어뜨렸나. 됐어, 고마워. 근데 방금 뭐라고 불렀어? '꽥 사장'?"),
                    N("P16", "그 이름, 나만 알아. 가게 창고에서 혼자 부르던 거야. 유치하지. 알아."),
                    N("P16", "넌 가끔 선을 너무 정확히 알아. 넘기 직전까지. 오래 본 사람처럼."),
                    M("…넘지는 않을게."),
                    N("P16", "알아. 그래서 짜증 나. 그래서… 하나 말해 줄게. 한 번만.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "한 번이면 충분해. 들을게.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P16",
                        Reply =
                        {
                            N("P16", "가게 망한 거, 거래처 탓 아니야. 내가 원단을 세 배로 들였어. 다들 말렸는데.", Emotion.Blank),
                            N("P16", "건물주 탓도 아니야. 월세는 처음부터 못 낼 금액이었어. 내가 골랐어."),
                            N("P16", "…내 탓이야. 사람한테 이 말 처음 해 봐. 꽥 사장한테만 했었지.", Emotion.Sad),
                        } },
                    new BondChoice { Label = "꽥 사장님이 가게 대표였어?", Like = 0.04f, Attach = 0.04f, Reveal = "hint:P16",
                        Reply =
                        {
                            N("P16", "…풋. 응. 월급은 못 줬지만.", Emotion.Laugh),
                            N("P16", "대표가 오리라서 망했나. …아니야. 그건 아니야."),
                        } },
                    new BondChoice { Label = "이런 거 주머니에 넣고 다니는 거, 의외다.", Like = -0.03f,
                        Reply =
                        {
                            N("P16", "의외라서 뭐.", Emotion.Angry),
                            N("P16", "유치한 거 좋아하면 안 돼? 이것도 내 결정이야."),
                        } },
                },
            });
        }
    }
}
