using System.Collections.Generic;

namespace BL23.Sim
{
    // P18 임민서 — 노동자. 아주 짧은 존댓말. 과묵하고 공정한 분담에 민감하다.
    // '모릅니다'와 '못 봤습니다'를 구분한다. 오래 눌러 둔 불만이 갑자기 터진다.
    public static partial class BondScenes
    {
        static partial void Init_P18()
        {
            Add(new BondScene
            {
                Id = "B_P18_1", Npc = "P18", Stage = 1, Title = "보온병",
                Lines =
                {
                    N("P18", "…차 드시겠습니까."),
                    N("P18", "보리차입니다. 아침에 끓였습니다. 아직 뜨겁습니다."),
                    M("보온병 오래 쓰셨나 봐요. 뚜껑이 다 긁혔네요."),
                    N("P18", "아버지가 현장에서 십이 년 쓰시던 겁니다. 물려받았습니다. 지금은 제가 들고 다닙니다."),
                    N("P18", "…이건 숫자 퍼즐입니다. 가로세로 합이 같아야 합니다. 거의 다 풀었습니다."),
                },
                Choices =
                {
                    new BondChoice { Label = "마지막 칸, 같이 봐도 돼요?", Like = 0.06f, Trust = 0.03f, Attach = 0.04f,
                        Reveal = "note:아버지가 현장에서 십이 년 쓰던 보온병을 물려받아 보리차를 담아 다닌다. 쉴 때 숫자 퍼즐을 푼다",
                        Reply =
                        {
                            N("P18", "…됩니다.", Emotion.Smile),
                            N("P18", "칠입니다. …맞았습니다. 허. 둘이 하니까 빠릅니다."),
                        } },
                    new BondChoice { Label = "그럼 보온병이 민서 씨 선배네요.", Like = 0.05f, Trust = 0.02f, Attach = 0.04f,
                        Reply =
                        {
                            N("P18", "…그렇습니다. 말은 없는데 일은 합니다.", Emotion.Smile),
                            N("P18", "저랑 비슷합니다."),
                        } },
                    new BondChoice { Label = "퍼즐은 그냥 시간 때우기죠?", Like = -0.03f,
                        Reply =
                        {
                            N("P18", "…그렇게 말하는 사람들 있습니다.", Emotion.Blank),
                            N("P18", "저는 아닙니다."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P18_2", Npc = "P18", Stage = 2, Title = "첫차",
                Lines =
                {
                    N("P18", "새벽 네 시 사십 분 첫차 탑니다. …탔습니다. 밖에서는."),
                    N("P18", "동생 둘 있습니다. 학교 다닙니다. 제가 보냅니다."),
                    N("P18", "여기 온 뒤로 누가 제 자리를 메우는지 모릅니다. 못 봤으니까 모릅니다."),
                    M("힘들지 않아요?"),
                    N("P18", "힘들지 않습니다. …그렇게 말해 왔습니다.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "여기서는 그렇게 말 안 해도 돼요.", Like = 0.06f, Trust = 0.06f, Attach = 0.04f, Reveal = "hint:P18",
                        Reply =
                        {
                            N("P18", "…버스에서 책을 봤습니다. 동생 교과서. 제 건 없어서.", Emotion.Sad),
                            N("P18", "그냥 본 겁니다. 할 일이 없어서요."),
                        } },
                    new BondChoice { Label = "오늘 옮길 짐, 반은 제가 들게요.", Like = 0.06f, Trust = 0.03f, Attach = 0.04f,
                        Reply =
                        {
                            N("P18", "…반이면 공평합니다.", Emotion.Smile),
                            N("P18", "고맙습니다. 무임승차 안 하는 사람, 좋습니다."),
                        } },
                    new BondChoice { Label = "형이니까 당연히 해야죠.", Like = -0.05f,
                        Reply =
                        {
                            N("P18", "…당연한 거 아니었습니다.", Emotion.Angry),
                            N("P18", "제가 하는 일이 당연한 건 아니었습니다. …가 보겠습니다."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P18_3", Npc = "P18", Stage = 3, Title = "4년",
                Lines =
                {
                    N("P18", "소원 말입니까."),
                    N("P18", "집입니다. 월세 안 오르는 집. 동생들 방 하나씩."),
                    N("P18", "그리고 4년. 4년 치 생활비."),
                    M("왜 4년이에요? 딱 떨어지네요."),
                    N("P18", "…질문이 정확합니다.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "대답 안 해도 돼요. 좋은 소원이에요.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P18",
                        Reply =
                        {
                            N("P18", "…고맙습니다.", Emotion.Smile),
                            N("P18", "4년이면 제가 일을 안 해도 집이 굴러갑니다. 한 번은 쉬어 보고 싶었습니다."),
                            N("P18", "쉬면서 뭘 할지는… 아직 말 안 하겠습니다."),
                        } },
                    new BondChoice { Label = "4년이면 대학 다닐 시간이네요.", Like = 0.02f, Trust = 0.03f, Reveal = "hint:P18",
                        Reply =
                        {
                            N("P18", "우연입니다.", Emotion.Blank),
                            N("P18", "…우연이라고 해 두겠습니다."),
                        } },
                    new BondChoice { Label = "4년 쉬고 싶다는 거죠? 부럽다.", Like = -0.04f,
                        Reply =
                        {
                            N("P18", "노는 거 아닙니다.", Emotion.Angry),
                            N("P18", "…됐습니다."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P18_4", Npc = "P18", Stage = 4, Title = "접힌 책장",
                Lines =
                {
                    M("그 책, 3단원부터 보세요. 거기서 멈추셨잖아요."),
                    N("P18", "…3단원이라고 하셨습니까.", Emotion.Surprised),
                    N("P18", "도서실 맨 아래 칸 미적분 책. 3단원 첫 장이 접혀 있습니다. 제가 접었습니다."),
                    N("P18", "아무도 못 봤습니다. 밤에만 봤습니다."),
                    N("P18", "민혁 씨는 제 걸음 수를 압니다. 가끔. 오래 같이 걸은 사람처럼."),
                    M("…민서 씨가 말하고 싶을 때까지 기다릴게요."),
                    N("P18", "기다리지 마십시오. 지금 하겠습니다. 짧게.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "짧아도 돼요. 다 들을게요.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P18",
                        Reply =
                        {
                            N("P18", "공부, 계속하고 싶었습니다. 붙은 학교도 있었습니다.", Emotion.Blank),
                            N("P18", "집에는 공부가 싫다고 했습니다. 그래야 동생들이 미안해하지 않으니까."),
                            N("P18", "4년은… 학교입니다. 처음 말합니다. 허.", Emotion.Sad),
                        } },
                    new BondChoice { Label = "왜 밤에만 봤어요?", Like = 0.02f, Trust = 0.04f, Reveal = "hint:P18",
                        Reply =
                        {
                            N("P18", "낮에는 옮길 게 많습니다.", Emotion.Blank),
                            N("P18", "…밤에는 저 혼자라서요."),
                        } },
                    new BondChoice { Label = "공부 싫다더니, 거짓말이었네요.", Like = -0.05f,
                        Reply =
                        {
                            N("P18", "그렇게 말하면 할 말 없습니다.", Emotion.Angry),
                            N("P18", "거짓말이라도, 제가 정한 겁니다."),
                        } },
                },
            });
        }
    }
}
