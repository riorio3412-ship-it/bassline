using System.Collections.Generic;

namespace BL23.Sim
{
    // P14 차은결 — 장의사. 낮고 고른 존댓말, 질문 뒤의 기다림, 말장난(차/접다/매듭/결).
    // 공개적으로는 '도윤 씨'라 부르며 남매임을 숨긴다. 도윤의 범죄는 끝까지 직접 말하지 않는다.
    public static partial class BondScenes
    {
        static partial void Init_P14()
        {
            Add(new BondScene
            {
                Id = "B_P14_1", Npc = "P14", Stage = 1, Title = "종이 백합",
                Lines =
                {
                    N("P14", "어머나, 민혁 씨. 발소리가 조용해서 몰랐어요. 좋은 발소리예요.", Emotion.Surprised),
                    N("P14", "종이 백합을 접고 있었어요. 여기 계신 분들 수만큼. 열여덟 송이요."),
                    N("P14", "장례식장에서 배웠어요. 손이 바쁘면 슬픔이 덜 엉켜요."),
                    M("제 것도 있어요?"),
                    N("P14", "그럼요. …민혁 씨 건 아직이에요. 어떤 결인지 몰라서요. 기다릴게요.", Emotion.Smile),
                },
                Choices =
                {
                    new BondChoice { Label = "그럼 제 건 같이 접어요. 제 결은 서툴러요.", Like = 0.06f, Trust = 0.03f, Attach = 0.05f,
                        Reveal = "note:예배실에서 열여덟 명 몫의 종이 백합을 접는다",
                        Reply =
                        {
                            N("P14", "후후, 서툰 결도 결이에요. 여기, 모서리부터 반으로.", Emotion.Smile),
                            N("P14", "…접힌 자국은 안 없어져요. 오늘 일은 오래 남겠네요."),
                        } },
                    new BondChoice { Label = "종이 백합이라니, 좀 불길한데요.", Like = 0.02f, Attach = 0.02f,
                        Reply =
                        {
                            N("P14", "후후, 그런가요. 백합은 원래 반가운 꽃이에요.", Emotion.Smirk),
                            N("P14", "불길한 건 꽃이 아니라 꽃을 받는 날이죠."),
                        } },
                    new BondChoice { Label = "열여덟 송이인데, 사람이 줄면 어떡해요?", Like = -0.02f, Trust = 0.02f,
                        Reply =
                        {
                            N("P14", "…그럼 한 송이는 제단에 두면 돼요.", Emotion.Blank),
                            N("P14", "미리 다 접어 두는 건 그래서예요. 그날 서두르지 않으려고요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P14_2", Npc = "P14", Stage = 2, Title = "같은 길",
                Lines =
                {
                    N("P14", "어릴 때 이사를 자주 다녔어요. 새 동네에 가면 장례식장부터 찾았어요."),
                    N("P14", "장례식장은 어디나 같거든요. 같은 향, 같은 조용함. 거기선 길을 안 잃어요."),
                    N("P14", "그래서 산책도 같은 길로만 해요. 같아야 달라진 걸 알 수 있으니까요."),
                    M("요즘 달라진 게 있어요?"),
                    N("P14", "…도윤 씨요. 요즘 산책길에 안 보여요. 어디 계신지, 혹시 아세요?", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "같이 찾아볼까요? 은결 씨 길을 따라서.", Like = 0.05f, Trust = 0.05f, Attach = 0.05f, Reveal = "hint:P14",
                        Reply =
                        {
                            N("P14", "…고마워요. 그래도 괜찮아요. 찾는다고 제 옆에 계시는 건 아니니까요.", Emotion.Sad),
                            N("P14", "이사할 때 짐은 늘 상자 세 개였어요. 하나는 제 게 아니었고요."),
                        } },
                    new BondChoice { Label = "도윤 씨 걱정을 많이 하시네요. 친한가 봐요.", Like = 0.01f, Trust = 0.02f,
                        Reveal = "note:은결은 도윤이 어디 있는지 늘 알고 싶어 한다",
                        Reply =
                        {
                            N("P14", "…친하다는 말로 묶기엔 매듭이 좀 복잡해요.", Emotion.Blank),
                            N("P14", "후후, 말장난이에요. 반쯤은요."),
                        } },
                    new BondChoice { Label = "장례식장이 편하다니, 좀 이상하네요.", Like = -0.03f,
                        Reply =
                        {
                            N("P14", "그런가요. 다들 그렇게 말해요.", Emotion.Blank),
                            N("P14", "저는 산 사람들 사이에서 더 자주 길을 잃어요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P14_3", Npc = "P14", Stage = 3, Title = "매듭",
                Lines =
                {
                    N("P14", "소원은 매듭 같아요. 묶을 땐 쉬운데 풀려면 손톱이 다 상해요."),
                    N("P14", "제 건 한 사람 얘기예요. 오늘은 그 매듭을 조금 보여 드릴게요."),
                    N("P14", "차 한 잔 드세요. 성이 차라서 차를 드리는 거예요. …농담이에요. 반쯤은요.", Emotion.Smile),
                    M("나머지 반은요?"),
                    N("P14", "…말을 고르고 있어요. 기다려 주세요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "기다릴게요. 은결 씨 속도로.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P14",
                        Reply =
                        {
                            N("P14", "도윤 씨가 제 뜻대로 움직였으면 해요. 그게 제 소원이에요.", Emotion.Blank),
                            N("P14", "어디로 갈지, 무엇을 할지, 누구 곁에 있을지. 전부요."),
                            N("P14", "…무섭죠. 저도요. 그래도 그게 그 사람을 지키는 유일한 매듭이에요.", Emotion.Sad),
                        } },
                    new BondChoice { Label = "나머지 반은 차 맛으로 대신 들을게요.", Like = 0.04f, Attach = 0.04f,
                        Reply =
                        {
                            N("P14", "후후, 차 맛을 아시는 분이네요.", Emotion.Smile),
                            N("P14", "그럼 소원은 다음 잔에. 식기 전에 드세요."),
                        } },
                    new BondChoice { Label = "그 한 사람, 도윤 씨죠?", Like = -0.03f, Trust = 0.02f, Reveal = "hint:P14",
                        Reply =
                        {
                            N("P14", "…어머나.", Emotion.Surprised),
                            N("P14", "맞혀도 상은 없어요. 오늘은 여기까지 할게요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P14_4", Npc = "P14", Stage = 4, Title = "뒷정리",
                Lines =
                {
                    N("P14", "어머나. 세 번째 기둥 모퉁이, 아홉 시 십이 분. 제가 도는 자리예요.", Emotion.Surprised),
                    N("P14", "제 길을 아시네요. 시간까지요. 가르쳐 드린 적 없는데요."),
                    N("P14", "민혁 씨는 가끔 저를 오래 본 사람 같아요. 장례를 두 번 치러 본 사람처럼."),
                    M("…은결 씨가 여기 지나갈 것 같았어요."),
                    N("P14", "그런가요. 그럼 모르시는 척해 주세요. 제가 먼저 말할 테니까요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "모르는 척할게요. 그리고 다 들을게요.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P14",
                        Reply =
                        {
                            N("P14", "도윤 씨와 저는 남매예요. 여기선 아무도 몰라요. 이제 민혁 씨만요.", Emotion.Blank),
                            N("P14", "그 사람이 어지른 자리를 제가 치워 왔어요. 뭘 치웠는지는 말하지 않을게요."),
                            N("P14", "지키는 건지 묶는 건지, 매듭 모양이 똑같아서 저도 모르겠어요.", Emotion.Sad),
                        } },
                    new BondChoice { Label = "매일 같은 길에서 뭘 확인하세요?", Like = 0.01f, Trust = 0.03f, Reveal = "hint:P14",
                        Reply =
                        {
                            N("P14", "달라진 것들이요. 문 아래 흙, 젖은 소매, 제자리에 없는 액자.", Emotion.Blank),
                            N("P14", "찾으면 치워요. 장의사는 사인을 말하지 않아요. 뒷정리만 하죠."),
                        } },
                    new BondChoice { Label = "은결 씨, 좀 무서운 사람이네요.", Like = -0.04f,
                        Reply =
                        {
                            N("P14", "후후. 그런가요.", Emotion.Smirk),
                            N("P14", "무서운 건 제가 아니라 조용함이에요. 저는 그걸 좀 좋아할 뿐이고요."),
                        } },
                },
            });
        }
    }
}
