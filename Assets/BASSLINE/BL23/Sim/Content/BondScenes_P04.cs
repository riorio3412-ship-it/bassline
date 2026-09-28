using System.Collections.Generic;

namespace BL23.Sim
{
    // P04 차도윤 — 낮고 공손한 존댓말. 복원·무향차·도자기·액자의 은유. 예의 아래 통제욕.
    // 과거의 범행은 끝까지 직접 말하지 않는다(버릇·제자리·기록으로만 드러난다).
    public static partial class BondScenes
    {
        static partial void Init_P04()
        {
            Add(new BondScene
            {
                Id = "B_P04_1", Npc = "P04", Stage = 1, Title = "기울어진 액자",
                Lines =
                {
                    N("P04", "흠. 이 복도 액자들, 전부 왼쪽으로 1도쯤 기울어 있습니다."),
                    N("P04", "밤마다 바로잡는데 아침이면 또 기울어요. 집이 고집이 세군요."),
                    M("그걸 매일 밤 바로잡는다고요?"),
                    N("P04", "네. 소리 없이 할 수 있는 일은 전부 좋아합니다. 차도 그렇고요.", Emotion.Smile),
                    N("P04", "무향차 드시겠어요? 향이 없으면 다른 게 더 잘 느껴지거든요."),
                },
                Choices =
                {
                    new BondChoice { Label = "네, 주세요. 뭐가 느껴지는지 궁금해요.", Like = 0.06f, Trust = 0.02f, Attach = 0.04f,
                        Reveal = "note:밤마다 복도 액자를 바로잡는다. 향 없는 차를 마신다",
                        Reply =
                        {
                            N("P04", "보통은 물맛이요. 그다음은… 옆 사람의 숨소리입니다.", Emotion.Smile),
                            N("P04", "후. 농담입니다. 반쯤은요."),
                        } },
                    new BondChoice { Label = "제가 액자 반대쪽 잡아 드릴게요.", Like = 0.05f, Trust = 0.03f, Attach = 0.03f,
                        Reply =
                        {
                            N("P04", "…고맙습니다. 손대기 전에 물어봐 주셔서요.", Emotion.Smile),
                            N("P04", "대부분은 그냥 만지거든요. 그게 제일 불편합니다."),
                        } },
                    new BondChoice { Label = "그냥 기울어진 채로 두면 안 돼요?", Like = -0.03f,
                        Reply =
                        {
                            N("P04", "두셔도 됩니다. 민혁 씨 방이라면요.", Emotion.Blank),
                            N("P04", "여긴 제가 보는 복도라서요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P04_2", Npc = "P04", Stage = 2, Title = "빈 얼굴",
                Lines =
                {
                    N("P04", "제게 복원을 가르쳐 주신 분이 있었어요. 그분 원칙은 하나였습니다."),
                    N("P04", "'없던 것을 더하지 말 것.' 긁혀 나간 자리는 긁힌 채로 두라고요."),
                    N("P04", "한번은 얼굴이 지워진 초상화를 받았어요. 저는… 얼굴을 그려 넣었습니다."),
                    M("허락 없이요?"),
                    N("P04", "네. 비어 있는 얼굴을 볼 수가 없었어요. 그날 공방에서 나왔습니다.", Emotion.Blank),
                    N("P04", "그 뒤로 이사를 자주 다녔어요. 짐은 늘 상자 두 개. 그게 편했습니다."),
                },
                Choices =
                {
                    new BondChoice { Label = "그 얼굴, 누구 얼굴로 그렸어요?", Like = 0.05f, Trust = 0.05f, Attach = 0.04f, Reveal = "hint:P04",
                        Reply =
                        {
                            N("P04", "…흠. 좋은 질문이군요. 아무도 안 물어봤는데.", Emotion.Surprised),
                            N("P04", "제가 아는 가장 조용한 얼굴이요. 누구 얼굴인지는 저만 알고 있겠습니다.", Emotion.Smile),
                        } },
                    new BondChoice { Label = "상자 두 개면 이사는 금방이겠네요.", Like = 0.04f, Trust = 0.02f, Attach = 0.03f,
                        Reply =
                        {
                            N("P04", "후. 네, 한 시간이면 충분했습니다. 인사할 사람도 없었고요."),
                            N("P04", "…여기는 처음으로 짐을 풀어 두고 싶은 방이긴 해요."),
                        } },
                    new BondChoice { Label = "쫓겨날 만했네요. 남의 그림인데.", Like = -0.04f,
                        Reply =
                        {
                            N("P04", "그렇군요. 민혁 씨도 그분과 같은 말을 하시네요.", Emotion.Blank),
                            N("P04", "…틀린 말은 아닙니다. 그래서 더 듣기 싫군요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P04_3", Npc = "P04", Stage = 3, Title = "덧칠 벗기기",
                Lines =
                {
                    N("P04", "오래된 니스를 벗기는 중입니다. 덧칠이 두꺼우면 원래 색이 숨을 못 쉬죠."),
                    N("P04", "제 소원도 비슷해요. 벗겨 내고 싶은 게 있습니다."),
                    M("벗겨 내고 싶은 게… 뭔데요?"),
                    N("P04", "…말하기 전에 문을 닫아도 될까요. 소리가 새는 게 싫어서요.", Emotion.Blank),
                    N("P04", "후. 표정이 굳으셨군요. 괜찮습니다. 원하시면 열어 두죠."),
                },
                Choices =
                {
                    new BondChoice { Label = "닫아도 돼요. 들을게요.", Like = 0.05f, Trust = 0.07f, Attach = 0.05f, Reveal = "contract:P04",
                        Reply =
                        {
                            N("P04", "…고맙습니다. 제 이름 옆에 붙은 기록들이 있어요. 서류철 몇 권 분량."),
                            N("P04", "그걸 전부 지우고 싶습니다. 원래 없던 것처럼. 새 액자에 넣듯이."),
                            N("P04", "무슨 기록인지는… 묻지 않으실 분이라 믿고 말씀드린 겁니다.", Emotion.Smile),
                        } },
                    new BondChoice { Label = "문은 열어 두고 얘기해요.", Like = 0.02f, Trust = 0.02f, Reveal = "hint:P04",
                        Reply =
                        {
                            N("P04", "그렇군요. 현명하십니다.", Emotion.Smirk),
                            N("P04", "그럼 이 정도만요. 지우고 싶은 기록이 있습니다. 누구에게나 있죠."),
                        } },
                    new BondChoice { Label = "그 덧칠, 도윤 씨가 한 거죠?", Like = -0.03f, Trust = 0.01f,
                        Reply =
                        {
                            N("P04", "…흠. 날카로우시네요.", Emotion.Blank),
                            N("P04", "오늘은 여기까지 하죠. 니스가 마르기 전에요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P04_4", Npc = "P04", Stage = 4, Title = "제자리",
                Lines =
                {
                    N("P04", "그대로 계세요. …방금 제 찻잔을 받침 왼쪽 끝에 두셨죠.", Emotion.Blank),
                    N("P04", "제가 두는 자리예요. 1센티도 안 틀리게. 가르쳐 드린 적 없는데요."),
                    N("P04", "공방 서랍 두 번째 칸도요. 들어 올려야 열리는 걸 아시더군요."),
                    M("…도윤 씨가 편할 것 같아서요."),
                    N("P04", "흠. 민혁 씨는 저를 너무 잘 아시는군요. 저보다 조금 먼저요."),
                    N("P04", "그런 분 앞에서는 둘 중 하나를 해야 합니다. 치우거나, 털어놓거나."),
                    N("P04", "…후. 농담입니다. 앉으세요. 오늘은 후자로 하죠.", Emotion.Smile),
                },
                Choices =
                {
                    new BondChoice { Label = "뭘 들어도 이 자리에 있을게요.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P04",
                        Reply =
                        {
                            N("P04", "도시를 몇 번 옮겼습니다. 이사할 때마다 상자는 늘 셋이었지요."),
                            N("P04", "둘은 제 것. 하나는 제 것이 아니었습니다. 그 상자는 늘 은결이 들고 나갔고요.", Emotion.Blank),
                            N("P04", "은결과 저는 남매입니다. …이제 제 곁에 민혁 씨 자리도 생겼네요.", Emotion.Smile),
                        } },
                    new BondChoice { Label = "치운다는 건 무슨 뜻이에요?", Like = 0.01f, Trust = 0.03f,
                        Reveal = "hint:P04;note:도윤은 제자리를 벗어난 것을 견디지 못한다. 사람도",
                        Reply =
                        {
                            N("P04", "말 그대로요. 제자리에 없는 걸 원래 자리로 돌려놓는 겁니다.", Emotion.Blank),
                            N("P04", "물건 얘기예요. 대부분은요."),
                        } },
                    new BondChoice { Label = "찻잔은 이쪽이 더 예쁜데요.", Like = -0.04f,
                        Reply =
                        {
                            N("P04", "…원래대로 두시죠.", Emotion.Angry),
                            N("P04", "부탁입니다. 두 번 말하게 하지 마세요."),
                        } },
                },
            });
        }
    }
}
