using System.Collections.Generic;

namespace BL23.Sim
{
    // P10 강준서 — 요리사. 느긋하고 온화한 존댓말("허허", "어이쿠"). 밥과 휴식을 챙기고,
    // 거절을 보호 요청으로 읽어 남의 결정을 대신한다. 화가 나면 꾸밈이 사라진다.
    public static partial class BondScenes
    {
        static partial void Init_P10()
        {
            Add(new BondScene
            {
                Id = "B_P10_1", Npc = "P10", Stage = 1, Title = "온실 바질",
                Lines =
                {
                    N("P10", "어이쿠, 민혁 씨. 발밑 조심하세요. 거기 바질 새순이에요.", Emotion.Surprised),
                    N("P10", "여기 흙이 좋아요. 이틀 만에 한 뼘이나 자랐어요. 좀 무서울 정도로요."),
                    N("P10", "바질은 꽃대 올라오기 전에 윗순을 따 줘야 해요. 그래야 옆으로 번져요."),
                    N("P10", "우선 이거 하나 비벼서 향 맡아 보세요. 아침은 드셨어요?", Emotion.Smile),
                    M("아직이요."),
                    N("P10", "그럴 줄 알았어요. 얼굴에 쓰여 있어요. 허허."),
                },
                Choices =
                {
                    new BondChoice { Label = "윗순 따는 거, 저도 해 봐도 돼요?", Like = 0.06f, Trust = 0.03f, Attach = 0.04f,
                        Reveal = "note:온실에서 바질을 키운다. 이 저택 흙에선 이틀 만에 한 뼘이 자란다",
                        Reply =
                        {
                            N("P10", "그럼요. 마디 두 개 위에서, 이렇게. 똑.", Emotion.Smile),
                            N("P10", "잘하시네요. 이건 이따 파스타에 넣을게요. 민혁 씨 접시엔 두 배로."),
                        } },
                    new BondChoice { Label = "아침 대신 바질만 씹을게요.", Like = 0.04f, Attach = 0.03f,
                        Reply =
                        {
                            N("P10", "허허, 염소도 아니고요.", Emotion.Laugh),
                            N("P10", "기다리세요. 오 분이면 돼요. 거절은 안 받을게요."),
                        } },
                    new BondChoice { Label = "괜찮아요, 배 안 고파요.", Like = -0.02f, Attach = 0.01f,
                        Reply =
                        {
                            N("P10", "괜찮다는 사람이 제일 걱정돼요.", Emotion.Sad),
                            N("P10", "…우선 앉아 계세요. 제가 알아서 할게요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P10_2", Npc = "P10", Stage = 2, Title = "숟가락 세기",
                Lines =
                {
                    N("P10", "전 식탁 차릴 때 숟가락부터 세어요. 열여덟 개요. 버릇이에요."),
                    N("P10", "어릴 때 할머니가 동네 애들 밥을 다 먹였어요. 숟가락이 늘 모자랐죠.", Emotion.Smile),
                    N("P10", "그래서 전 숟가락이 남으면 무서워요. 누가 안 온 거니까."),
                    M("할머니 식탁에서도 숟가락이 남은 적 있어요?"),
                    N("P10", "…딱 한 번요. 할머니 자리였어요. 그래서 지금도 세기 전에 숨부터 크게 쉬어요.", Emotion.Sad),
                },
                Choices =
                {
                    new BondChoice { Label = "할머니 밥은 어떤 맛이었어요?", Like = 0.06f, Trust = 0.05f, Attach = 0.04f, Reveal = "hint:P10",
                        Reply =
                        {
                            N("P10", "…누룽지 끓인 물이요. 별거 아닌데, 제 손으로는 그 맛이 안 나요.", Emotion.Sad),
                            N("P10", "요즘 제 혀가 좀 게을러서요. 허허, 이건 비밀로 해 주세요."),
                        } },
                    new BondChoice { Label = "숟가락은 제가 셀게요. 준서 씨는 국만 뜨세요.", Like = 0.05f, Trust = 0.03f, Attach = 0.05f,
                        Reply =
                        {
                            N("P10", "…그래도 될까요? 그럼 부탁해요.", Emotion.Smile),
                            N("P10", "남아도 말하지 마시고, 그냥 하나 치워 주세요."),
                        } },
                    new BondChoice { Label = "안 오는 사람까지 챙길 필요 있어요?", Like = -0.03f,
                        Reply =
                        {
                            N("P10", "있어요.", Emotion.Angry),
                            N("P10", "…죄송해요. 그 말엔 좀 예민해요. 밥은 드시고 가세요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P10_3", Npc = "P10", Stage = 3, Title = "백야의 씨앗",
                Lines =
                {
                    N("P10", "민혁 씨, 백야 본 적 있어요? 해가 안 지는 밤이요."),
                    N("P10", "제 소원이 딱 그 이름이에요. '백야의 씨앗'. 신만 가진 식재료래요."),
                    N("P10", "심으면 어떤 맛이든 난대요. 한 번 먹어 본 맛이면, 그대로."),
                    M("어떤 맛을 내고 싶은데요?"),
                    N("P10", "그걸 말하면… 좀 무거워져요. 빈속에 하기엔 좀 그런 얘기라서요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "그럼 같이 먹으면서 들을게요.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P10",
                        Reply =
                        {
                            N("P10", "허허. 그 말 들으니 해도 될 것 같네요.", Emotion.Smile),
                            N("P10", "할머니가 마지막 날 끓여 주신 누룽지 물이요. 그 뒤로 아무도 그 맛을 못 냈어요."),
                            N("P10", "그 씨앗만 있으면 누구한테든 그 맛을 먹여 줄 수 있어요. 그게 제 소원이에요."),
                        } },
                    new BondChoice { Label = "신이 주는 재료면 좀 위험하지 않아요?", Like = 0.02f, Trust = 0.03f, Reveal = "hint:P10",
                        Reply =
                        {
                            N("P10", "…위험하죠. 알아요.", Emotion.Blank),
                            N("P10", "그래도 맛이 그리우면 사람은 위험한 것도 먹어요. 저도 그렇고요."),
                        } },
                    new BondChoice { Label = "요리사가 신한테 재료를 빌다니, 반칙이죠.", Like = -0.04f,
                        Reply =
                        {
                            N("P10", "어이쿠, 아프네요.", Emotion.Pain),
                            N("P10", "…다른 얘기 해요. 국 식어요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P10_4", Npc = "P10", Stage = 4, Title = "게으른 혀",
                Lines =
                {
                    M("오늘 간은 제가 볼게요."),
                    N("P10", "…간을 민혁 씨가요? 왜요? 제가 뭘 이상하게 했어요?", Emotion.Surprised),
                    N("P10", "다들 맛있다고만 해요. 착해서요. 그런데 민혁 씨는 다 알고 있는 사람처럼 말하네요."),
                    N("P10", "늘 제가 맛보기 전에 숟가락을 먼저 내미시고요. 몇 번 해 본 사람처럼."),
                    N("P10", "허허. 들켰네요. 들킨 김에 다 말할게요. 식기 전에.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "네. 다 먹을 때까지 안 일어날게요.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f,
                        Reveal = "secret:P10;note:준서는 맛을 반쯤 잃었다. 음식에 무언가 섞여도 알아채지 못할 수 있다",
                        Reply =
                        {
                            N("P10", "작년 겨울에 크게 앓고 나서 맛을 반쯤 잃었어요. 짠맛이랑 쓴맛은 특히요.", Emotion.Sad),
                            N("P10", "그래도 계속 요리했어요. 다들 괜찮다고 하니까요. 한 번은 상한 걸 낼 뻔했어요."),
                            N("P10", "지금 누가 제 냄비에 뭘 넣어도 전 몰라요. 그래도 그 씨앗이 갖고 싶어요.", Emotion.Fear),
                        } },
                    new BondChoice { Label = "혹시 요즘 맛이 잘 안 느껴져요?", Like = 0.03f, Trust = 0.04f, Reveal = "hint:P10",
                        Reply =
                        {
                            N("P10", "…어이쿠. 그렇게 바로 들어오시면 곤란한데.", Emotion.Pain),
                            N("P10", "조금요. 조금. 오늘은 거기까지만 할게요."),
                        } },
                    new BondChoice { Label = "사실 간이 좀 셌어요. 다들 참는 것 같던데.", Like = -0.05f,
                        Reply =
                        {
                            N("P10", "…그래요?", Emotion.Break),
                            N("P10", "참고 먹었다고요. 다들.", Emotion.Sad),
                        } },
                },
            });
        }
    }
}
