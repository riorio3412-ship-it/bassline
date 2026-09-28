using System.Collections.Generic;

namespace BL23.Sim
{
    // P05 백이현 — 정당 청년대변인. 존댓말·요약("요컨대", "말씀하신 건"), 사적인 자리에선 반말이 샌다.
    // 공익 언어로 사익을 포장한다. 희극적 악당이 아니다. 금·수영·첫 문장. 기자와 망신을 싫어한다.
    public static partial class BondScenes
    {
        static partial void Init_P05()
        {
            Add(new BondScene
            {
                Id = "B_P05_1", Npc = "P05", Stage = 1, Title = "첫 문장",
                Lines =
                {
                    N("P05", "아, 민혁 씨. 물에 떠 있으면 아무 소리도 안 들려요. 최고의 회의실이죠.", Emotion.Smile),
                    N("P05", "자, 자. 하나만 도와주세요. 내일 모임에서 꺼낼 첫 문장 후보가 두 개인데요."),
                    N("P05", "A안은 '우리는 서로를 의심할 이유보다 믿을 이유가 많습니다.'"),
                    N("P05", "B안은 '배고픈 분부터 식당으로 가시죠.' 어느 쪽이 더 기억에 남을까요?"),
                },
                Choices =
                {
                    new BondChoice { Label = "B안이요. 배고픈 건 다들 진짜니까요.", Like = 0.06f, Trust = 0.03f, Attach = 0.04f,
                        Reveal = "note:연설은 첫 문장이 전부라고 믿는다. 아침마다 수영장에 떠 있다",
                        Reply =
                        {
                            N("P05", "하하하! 역시. 요컨대 사람은 배가 먼저라는 거죠.", Emotion.Laugh),
                            N("P05", "A안은 기자들이 좋아할 문장이에요. 여긴 기자가 한 명뿐이지만요.", Emotion.Smirk),
                        } },
                    new BondChoice { Label = "둘 다 이현 씨가 기억되고 싶은 문장 같은데요.", Like = 0.04f, Trust = 0.04f, Attach = 0.02f,
                        Reply =
                        {
                            N("P05", "…날카로우시네요. 말씀하신 건, 요컨대 제가 주인공이 되고 싶어 한다는 거죠?", Emotion.Surprised),
                            N("P05", "정치하는 사람은 원래 그래요. 하하. 비공개로 해 주세요."),
                        } },
                    new BondChoice { Label = "그냥 아무 말도 안 하시면 안 돼요?", Like = -0.03f,
                        Reply =
                        {
                            N("P05", "침묵도 메시지예요. 제일 비싼 메시지.", Emotion.Blank),
                            N("P05", "그건 제가 감당 못 할 가격이고요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P05_2", Npc = "P05", Stage = 2, Title = "금니의 유래",
                Lines =
                {
                    N("P05", "금니요? 다들 물어보시는데, 사실 기사 제목 때문이에요.", Emotion.Smile),
                    N("P05", "열아홉, 첫 선거 캠프 자원봉사 때였어요. 유세장 계단에서 넘어져서 앞니가 반 나갔죠."),
                    N("P05", "다음 날 제목이 '청년 정치, 이 빠지다'였습니다. 하하하. 명문이죠.", Emotion.Laugh),
                    M("그래서 금으로 하신 거예요?"),
                    N("P05", "숨기면 약점, 보여 주면 상징. 요컨대 제가 먼저 웃어 버리면 이겨요."),
                    N("P05", "…그 뒤로 기자 앞에선 한 번도 안 넘어졌습니다. 한 번도요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "그날은 많이 창피했겠어요.", Like = 0.06f, Trust = 0.05f, Attach = 0.04f, Reveal = "hint:P05",
                        Reply =
                        {
                            N("P05", "…하하. 네. 그날 밤 수영장에서 세 시간을 떠 있었어요.", Emotion.Sad),
                            N("P05", "물속에선 아무도 말을 못 걸거든요. 이건 공식 입장 아닙니다."),
                        } },
                    new BondChoice { Label = "제목은 정말 잘 뽑았네요, 그 기자.", Like = 0.03f, Attach = 0.02f,
                        Reply =
                        {
                            N("P05", "하하, 그렇죠? 저도 인정해요. 그래서 더 싫고요.", Emotion.Grin),
                            N("P05", "좋은 제목은 사실보다 오래 살아요. 그게 문제죠."),
                        } },
                    new BondChoice { Label = "안 넘어진 게 아니라 안 들킨 거 아니에요?", Like = -0.04f, Trust = 0.01f,
                        Reply =
                        {
                            N("P05", "…말씀하신 건, 재밌는 가설이네요.", Emotion.Blank),
                            N("P05", "가설은 기사로 쓰지 마세요. 부탁입니다."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P05_3", Npc = "P05", Stage = 3, Title = "비공개 회의",
                Lines =
                {
                    N("P05", "비공개 회의 하시죠. 속기록 없이. 민혁 씨니까요."),
                    N("P05", "요컨대 제 소원은 '정리'예요. 흩어진 걸 모으는 것. 공익적으로요.", Emotion.Smile),
                    M("공익적으로요?"),
                    N("P05", "…하하. 역시 그 단어에서 멈추시네. 다들 그냥 넘어가는데.", Emotion.Smirk),
                    N("P05", "좋아요. 말 놓을게. 이 얘긴 존댓말로 하면 연설이 돼 버리거든."),
                },
                Choices =
                {
                    new BondChoice { Label = "연설 말고, 이현 씨 말로 해 줘요.", Like = 0.06f, Trust = 0.06f, Attach = 0.04f, Reveal = "contract:P05",
                        Reply =
                        {
                            N("P05", "내 이름이 적힌 장부들이 있어. 적히면 안 되는 돈이 적힌 거.", Emotion.Pain),
                            N("P05", "그걸 전부 회수하는 게 소원이야. 한 장도 안 남기고."),
                            N("P05", "내가 무너지면 날 믿은 사람들도 무너져. 이건 진심이야. 반쯤은.", Emotion.Sad),
                        } },
                    new BondChoice { Label = "공익 좋죠. 모두를 위한 정리라면요.", Like = 0.03f,
                        Reply =
                        {
                            N("P05", "그렇지? 하하. 역시 말이 통해.", Emotion.Grin),
                            N("P05", "…근데 방금 좀 실망했어. 넌 안 넘어갈 줄 알았거든."),
                        } },
                    new BondChoice { Label = "그거, 불법이잖아요.", Like = -0.03f, Trust = 0.02f, Reveal = "hint:P05",
                        Reply =
                        {
                            N("P05", "기자처럼 말하네.", Emotion.Disgust),
                            N("P05", "회의 끝. 속기록 없는 거 잊지 마."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P05_4", Npc = "P05", Stage = 4, Title = "장부의 이름들",
                Lines =
                {
                    N("P05", "…방금 깊은 쪽으로 가지 말라고 했지. 발 안 닿는 쪽.", Emotion.Surprised),
                    N("P05", "나 잠수 못 해. 떠 있기만 하지. 그거 아무도 몰라. 비서도."),
                    N("P05", "넌 가끔 내 원고를 먼저 읽은 사람처럼 굴어. 수정본까지."),
                    M("…거기서만 이현 씨가 좀 굳는 것 같아서요."),
                    N("P05", "기자였으면 큰일 날 뻔했네. 근데 이상하게 화가 안 나."),
                    N("P05", "어차피 다 아는 눈치니까, 난 맞는지만 확인해 줄게.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "이현 씨 입으로 들을게요. 전부.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P05",
                        Reply =
                        {
                            N("P05", "태겸 씨가 내 돈을 관리했어. 어디서 난 돈인지는 묻지 않는 조건으로.", Emotion.Blank),
                            N("P05", "그 돈 때문에 문 닫은 가게들이 있어. 서류엔 숫자로만 남았지만.", Emotion.Pain),
                            N("P05", "가온 씨 기사가 그 장부를 건드렸고. …여긴 그 이름들이 다 모여 있어."),
                        } },
                    new BondChoice { Label = "가온 씨를 피하는 것도 그것 때문이에요?", Like = 0.02f, Trust = 0.03f,
                        Reveal = "hint:P05;note:이현은 가온을 피한다. 가온의 기사가 이현의 장부를 건드렸다",
                        Reply =
                        {
                            N("P05", "…눈치 빠르네. 그 사람이 내 장부 첫 장을 건드렸어. 기사로.", Emotion.Blank),
                            N("P05", "기자는 싫어. 틀려서가 아니라, 가끔 맞아서."),
                        } },
                    new BondChoice { Label = "들킨 거면 이제 끝난 거 아니에요?", Like = -0.04f,
                        Reply =
                        {
                            N("P05", "끝? 정치엔 끝이 없어. 정정 보도가 있을 뿐이지.", Emotion.Smirk),
                            N("P05", "…오늘 얘기는 없던 걸로 합시다. 공식적으로요."),
                        } },
                },
            });
        }
    }
}
