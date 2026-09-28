using System.Collections.Generic;

namespace BL23.Sim
{
    // P08 서라온 — 인디 밴드 베이시스트. 건조한 반말과 비꼼, 소리·박자 어휘.
    // 모욕은 농담으로 흘리고, 진심이 되면 농담이 멈춘다.
    public static partial class BondScenes
    {
        static partial void Init_P08()
        {
            Add(new BondScene
            {
                Id = "B_P08_1", Npc = "P08", Stage = 1, Title = "핫팩 경제학",
                Lines =
                {
                    N("P08", "손. 아니 그니까, 손 내밀어 보라고. 핫팩이야."),
                    N("P08", "편의점 제일 싼 거. 비싼 건 괜히 뜨겁기만 하고 금방 식어."),
                    N("P08", "사람도 비슷해. …아 뭐, 방금 건 좀 허세였다. 취소.", Emotion.Smirk),
                    M("근데 이거 어디서 났어? 여기 편의점 없잖아."),
                    N("P08", "주머니에 있었어. 매일 아침 열 개. 쓴 만큼 도로 차 있더라."),
                    N("P08", "이 집이 나한테 해 주는 유일한 서비스야. 좀 무서운데 따뜻해.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "그럼 하루에 하나씩 나도 줘.", Like = 0.06f, Trust = 0.02f, Attach = 0.05f,
                        Reveal = "note:주머니 속 핫팩이 매일 아침 열 개로 다시 채워진다고 한다",
                        Reply =
                        {
                            N("P08", "헐, 정기 구독이냐. …그래, 하나는 네 몫.", Emotion.Smirk),
                            N("P08", "대신 식어도 버리지 말고 돌려줘. 나 그거 모아."),
                        } },
                    new BondChoice { Label = "사람도 비슷하다는 거, 마저 말해 봐.", Like = 0.05f, Trust = 0.04f, Attach = 0.03f,
                        Reply =
                        {
                            N("P08", "에이, 취소했잖아."),
                            N("P08", "…싸고 조용한 게 오래간다고. 베이스처럼. 됐지? 이제 진짜 끝.", Emotion.Smile),
                        } },
                    new BondChoice { Label = "공짜 핫팩이면 이 집도 괜찮네.", Like = -0.02f,
                        Reply =
                        {
                            N("P08", "공짜가 제일 비싸다더라. 창고 지키는 태겸 씨가.", Emotion.Blank),
                            N("P08", "…넌 이 집이 좀 편한가 봐. 부럽다. 진심 반, 비꼼 반."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P08_2", Npc = "P08", Stage = 2, Title = "합주실 환풍기",
                Lines =
                {
                    N("P08", "음악실 환풍기 소리 들어 봤냐. 반음 낮게 돌아. 거슬려 죽겠어.", Emotion.Disgust),
                    N("P08", "옛날 합주실 환풍기는 딱 라 음이었어. 튜닝을 그걸로 했지."),
                    N("P08", "가난한 밴드들이 쓰는 요령이지. 밴드? 지금은 없어. 그냥 연락이 끊겼어."),
                    M("밖에 있는 거 중에 뭐가 제일 보고 싶어?"),
                    N("P08", "…그 환풍기. 사람 말고. 사람은 좀, 아직.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "사람은 '아직'이구나.", Like = 0.06f, Trust = 0.05f, Attach = 0.04f, Reveal = "hint:P08",
                        Reply =
                        {
                            N("P08", "그니까. 너 딱 좋은 데서 치고 들어온다.", Emotion.Smile),
                            N("P08", "곡 쓰던 형이 있었어. 그 형 얘기는… 다음 합주 때."),
                        } },
                    new BondChoice { Label = "내가 환풍기 소리 내 줄까? 라 음으로.", Like = 0.05f, Trust = 0.02f, Attach = 0.04f,
                        Reply =
                        {
                            N("P08", "해 봐. …헐, 반음 높잖아. 풉. 됐어, 그 정도면 튜닝은 된다.", Emotion.Laugh),
                            N("P08", "웃었다, 오랜만에."),
                        } },
                    new BondChoice { Label = "밴드가 깨졌으면 결국 누가 잘못한 거야?", Like = -0.03f,
                        Reply =
                        {
                            N("P08", "에이, 그런 건 묻는 거 아니야.", Emotion.Blank),
                            N("P08", "방금 합주실 문 닫히는 소리 났다. 오늘은 여기까지."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P08_3", Npc = "P08", Stage = 3, Title = "B면 트랙",
                Lines =
                {
                    N("P08", "오늘은 소원 얘기 하는 날이냐. 좋아, 숨 한 번 고르고."),
                    N("P08", "옛날 밴드에 곡이 하나 있어. 발표 안 된 거. 합주실 하드에만 있어."),
                    N("P08", "형이 썼고, 우린 백 번쯤 쳤어. 무대엔 한 번도 못 올렸고."),
                    M("그 곡을 원하는 거야?"),
                    N("P08", "…들어 준다니까 오히려 무섭네. 촌스러운 소원이라서.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "촌스러워도 돼. 네 속도대로 말해.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P08",
                        Reply =
                        {
                            N("P08", "그 곡을 내 걸로 하는 거. 그게 내 소원이야.", Emotion.Blank),
                            N("P08", "형은 안 낼 거래. 그럼 그냥 사라지잖아. 세상에 한 번도 못 나가고."),
                            N("P08", "…아 뭐, 대충 그런 거. 좋은 이유 같지? 나도 그렇게 믿어 보려고.", Emotion.Smirk),
                        } },
                    new BondChoice { Label = "그 곡, 나도 들어 보고 싶다.", Like = 0.05f, Trust = 0.02f, Attach = 0.05f,
                        Reply =
                        {
                            N("P08", "헐. 그건 좀. 머릿속에선 매일 돌아가는데.", Emotion.Surprised),
                            N("P08", "…나중에. 네가 소음 안 내는 날에."),
                        } },
                    new BondChoice { Label = "남의 곡을 왜 네가 원해?", Like = -0.04f, Trust = 0.01f,
                        Reply =
                        {
                            N("P08", "…에이, 농담이야. 소원 같은 거 없어.", Emotion.Smirk),
                            N("P08", "베이스는 원래 티 안 나게 사는 거야."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P08_4", Npc = "P08", Stage = 4, Title = "크레딧",
                Lines =
                {
                    N("P08", "…야. 방금 흥얼거린 거. 다시 해 봐.", Emotion.Surprised),
                    N("P08", "그 베이스 라인. 무대엔 한 번도 안 올라간 곡이야. 밴드 말고는 들은 사람이 없어."),
                    N("P08", "너 가끔 내가 어디서 말을 끊을지 미리 알아. 전에 같이 합주해 본 사람처럼."),
                    M("…어디서 들었는지 나도 잘 모르겠어."),
                    N("P08", "그니까 더 소름이지. 근데 이상하게 편하다. 그래서 말할게."),
                    N("P08", "농담 안 할게. 지금부터는.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "응. 농담 없이 들을게.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P08",
                        Reply =
                        {
                            N("P08", "형이 그 곡 지우자고 했을 때, 몰래 복사했어. 내 USB에. 그건 아무도 몰라.", Emotion.Pain),
                            N("P08", "세상에 내고 싶은 게 아니야. 크레딧 맨 앞에 내 이름이 박혔으면 해. 형 이름 말고."),
                            N("P08", "베이스는 티가 안 나잖아. 한 번은 앞에 서고 싶었어. …부끄러운 건 알아.", Emotion.Sad),
                        } },
                    new BondChoice { Label = "그 형, 지금 어디 있어?", Like = 0.02f, Trust = 0.03f, Reveal = "hint:P08",
                        Reply =
                        {
                            N("P08", "몰라. 알고 싶지 않은 것도 있고.", Emotion.Blank),
                            N("P08", "…그 형 얘기만 나오면 매번 말이 꼬여."),
                        } },
                    new BondChoice { Label = "혹시 그 곡 훔친 거야?", Like = -0.04f,
                        Reply =
                        {
                            N("P08", "훔쳐? 헐, 세게 나오네.", Emotion.Angry),
                            N("P08", "…딱 삼 초 상처받았다. 아니, 좀 더."),
                        } },
                },
            });
        }
    }
}
