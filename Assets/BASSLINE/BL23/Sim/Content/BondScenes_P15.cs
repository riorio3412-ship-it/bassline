using System.Collections.Generic;

namespace BL23.Sim
{
    // P15 남가온 — 기자. 짧은 존댓말, 구체적 확인 질문(몇 시, 누가, 직접?), 출처와 전언의 구분,
    // 기다리는 침묵("……"). 공개가 곧 정의라고 믿는다. 과거의 오보는 끝까지 담담하게.
    public static partial class BondScenes
    {
        static partial void Init_P15()
        {
            Add(new BondScene
            {
                Id = "B_P15_1", Npc = "P15", Stage = 1, Title = "복도 지도",
                Lines =
                {
                    N("P15", "잠깐만요. 거기 서 계세요. 이 문에서 저 문까지 몇 걸음일 것 같아요?"),
                    M("…세어 보진 않았는데요."),
                    N("P15", "민서 씨가 세어 줬어요. 어제는 스물두 걸음, 오늘은 스물여섯. 복도가 길어졌어요."),
                    N("P15", "지도 그리는 중이에요. 걸음은 민서 씨가 세고, 출처랑 기록은 제가 맡아요."),
                    N("P15", "구석에 그린 건 고양이 아니에요. 유스티 씨예요. 어항 머리. 흐.", Emotion.Smirk),
                },
                Choices =
                {
                    new BondChoice { Label = "저도 끼워 주세요. 같이 기록해요.", Like = 0.06f, Trust = 0.03f, Attach = 0.04f,
                        Reveal = "note:복도 길이가 매일 달라진다. 가온이 민서가 센 걸음 수로 지도를 그린다",
                        Reply =
                        {
                            N("P15", "좋아요. 세는 건 민서 씨 몫이니까, 민혁 씨는 문 위치를 불러 주세요.", Emotion.Smile),
                            N("P15", "…환영한다는 말이에요. 제 방식으로는."),
                        } },
                    new BondChoice { Label = "유스티 씨 어항에 금붕어까지 그렸네요.", Like = 0.05f, Trust = 0.02f, Attach = 0.03f,
                        Reply =
                        {
                            N("P15", "네 마리예요. 매일 한 마리씩 색이 바뀌는 것 같아서 확인 중이에요.", Emotion.Smirk),
                            N("P15", "제 눈으로 직접 봤어요. 들은 얘기 아니에요."),
                        } },
                    new BondChoice { Label = "그렇게 다 재고 다니면 안 피곤해요?", Like = -0.02f,
                        Reply =
                        {
                            N("P15", "피곤해요. 모르는 채로 있는 것보다는 덜하고요.", Emotion.Blank),
                            N("P15", "……다음 질문 있으세요?"),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P15_2", Npc = "P15", Stage = 2, Title = "제목 없는 일기",
                Lines =
                {
                    N("P15", "편의점 커피가 그리워요. 맛 말고, 새벽 두 시에 그 가격이요."),
                    N("P15", "야간 데스크 볼 때 매일 마셨어요. 손 녹이면서 제목을 고쳤죠."),
                    N("P15", "일기는 제목 없이 써요. 제목을 붙이면 결론이 먼저 나오니까요."),
                    M("기사는 제목이 있어야 하잖아요."),
                    N("P15", "네. 그래서 무서워요. ……한 번, 제목이 사실보다 먼저 간 적이 있어요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "그 얘기는 가온 씨가 원할 때 해 줘요.", Like = 0.06f, Trust = 0.06f, Attach = 0.04f, Reveal = "hint:P15",
                        Reply =
                        {
                            N("P15", "……확인할게요. 제가 원하는지.", Emotion.Sad),
                            N("P15", "지금은 아니에요. 그렇게 말해 준 사람은 처음이에요. 수첩에 적어 둘게요."),
                        } },
                    new BondChoice { Label = "여기서 편의점 커피 비슷한 거 만들어 볼까요?", Like = 0.05f, Trust = 0.02f, Attach = 0.05f,
                        Reply =
                        {
                            N("P15", "…부엌 인스턴트커피에 설탕 반 봉. 종이컵에 타면 칠십 퍼센트쯤 비슷해요.", Emotion.Smile),
                            N("P15", "흐. 벌써 연구해 봤거든요."),
                        } },
                    new BondChoice { Label = "제목이 먼저 간 게 무슨 기사였는데요?", Like = -0.03f, Trust = 0.01f,
                        Reply =
                        {
                            N("P15", "잠깐만요. 그런 건 원래 제가 묻는 쪽이에요.", Emotion.Blank),
                            N("P15", "……오늘은 수첩 닫을게요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P15_3", Npc = "P15", Stage = 3, Title = "틀리지 않는 법",
                Lines =
                {
                    N("P15", "민혁 씨, 제 소원 추측해 보셨어요? 다들 특종일 거라고 하던데.", Emotion.Smirk),
                    N("P15", "틀렸어요. 기사는 제 손으로 써요. 신한테 빌 일 아니에요."),
                    N("P15", "……빌 일은 따로 있어요. 제 손으로는 안 되는 거."),
                    M("뭔데요?"),
                    N("P15", "하나만 확인할게요. 이 얘기, 어디에도 안 옮기실 거죠? 취재원 보호요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "어디에도 안 옮길게요. 약속해요.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P15",
                        Reply =
                        {
                            N("P15", "천재적인 두뇌요. 모든 걸 확인할 수 있는 머리.", Emotion.Blank),
                            N("P15", "쓰기 전에 다 알 수 있으면, 다시는 틀리지 않을 테니까요."),
                            N("P15", "……틀리지 않는 게 제 소원이에요. 제목으로는 최악이죠.", Emotion.Sad),
                        } },
                    new BondChoice { Label = "기사로 안 나갈 얘기면 편하게 들을게요.", Like = 0.04f, Trust = 0.03f,
                        Reply =
                        {
                            N("P15", "기사로 안 나가는 얘기는 없어요. 아직 안 나간 얘기만 있죠.", Emotion.Smirk),
                            N("P15", "……그래도 오늘 건 안 나가요. 다음에 말할게요."),
                        } },
                    new BondChoice { Label = "기자시잖아요. 저도 알 권리 있어요.", Like = -0.04f,
                        Reply =
                        {
                            N("P15", "……제 말을 저한테 그대로 돌려주시네요.", Emotion.Angry),
                            N("P15", "좋은 논리예요. 그래서 대답 안 할 거예요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P15_4", Npc = "P15", Stage = 4, Title = "정정 보도",
                Lines =
                {
                    M("확인은 세 번, 제목은 마지막. 맞죠?"),
                    N("P15", "잠깐만요. 방금 그 말, 어디서 들으셨어요?", Emotion.Surprised),
                    N("P15", "제 수첩 첫 장에 쓴 말이에요. 첫 장은 아무한테도 안 보여 줘요."),
                    N("P15", "……어디서 들었는지는 말 못 하시네요. 그런데 거짓말하는 얼굴은 아니에요."),
                    N("P15", "민혁 씨는 가끔 제 기사를 다 읽고 온 사람 같아요. 아직 안 쓴 것까지."),
                    M("…가온 씨가 말하고 싶은 것만 말해도 돼요."),
                    N("P15", "아니요. 오늘은 제가 취재원이 될게요. 처음 해 보는 역할이에요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "받아 적지 않을게요. 그냥 들을게요.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P15",
                        Reply =
                        {
                            N("P15", "제 기사 때문에 한 사람이 범인으로 몰렸어요. 확인은 두 번만 했어요.", Emotion.Pain),
                            N("P15", "범인이 아니었어요. 정정 기사는 2주 뒤, 12면 구석에 났어요."),
                            N("P15", "그분은 그걸 못 읽으셨어요. ……그 전에, 스스로. 그 뒤로 첫 장에 그 말을 썼어요.", Emotion.Crying),
                        } },
                    new BondChoice { Label = "백이현 씨 기사도 가온 씨가 쓴 거예요?", Like = 0.02f, Trust = 0.03f,
                        Reveal = "hint:P15;note:가온은 이현에 관한 기사를 썼다. 틀리지 않은 기사였다고 한다",
                        Reply =
                        {
                            N("P15", "……네. 그건 맞는 기사였어요. 확인 세 번 했어요.", Emotion.Blank),
                            N("P15", "맞는 기사도 누군가를 무너뜨려요. 그래서 요즘은 잠을 못 자요."),
                        } },
                    new BondChoice { Label = "기자라면 한 번쯤 틀릴 수도 있죠.", Like = -0.04f,
                        Reply =
                        {
                            N("P15", "한 번쯤이요.", Emotion.Angry),
                            N("P15", "그 한 번이 한 사람 전부였어요. ……이 얘긴 여기서 끝낼게요."),
                        } },
                },
            });
        }
    }
}
