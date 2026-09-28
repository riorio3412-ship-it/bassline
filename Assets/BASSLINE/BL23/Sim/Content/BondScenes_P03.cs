using System.Collections.Generic;

namespace BL23.Sim
{
    // P03 한서윤 — 단정한 존댓말. 정리·분담·기록. 도움받기를 패배처럼 여긴다. 화날수록 낮고 또렷하게.
    public static partial class BondScenes
    {
        static partial void Init_P03()
        {
            Add(new BondScene
            {
                Id = "B_P03_1", Npc = "P03", Stage = 1, Title = "여백의 햄스터",
                Lines =
                {
                    N("P03", "잠깐, 거기서 멈추세요. 설거지 당번표 잉크가 아직 안 말랐어요.", Emotion.Surprised),
                    N("P03", "번지면 처음부터 다시 써야 해요. 쓸 만한 볼펜이 이것뿐이라서요."),
                    M("표 구석에 있는 거… 햄스터예요?"),
                    N("P03", "…보셨어요? 그러니까, 그건 빈칸 표시예요. 다 쓸모가 있어서 그린 거예요."),
                    N("P03", "후훗, 거짓말이에요. 생각이 막히면 그려요. 예전에 키우던 애예요.", Emotion.Smile),
                    N("P03", "이름은 '회의록'이었어요. 제가 지었어요. 네, 웃으셔도 돼요."),
                },
                Choices =
                {
                    new BondChoice { Label = "제 칸 옆에도 한 마리 그려 주세요.", Like = 0.06f, Trust = 0.02f, Attach = 0.04f,
                        Reveal = "note:생각이 막히면 표 여백에 햄스터를 그린다. 예전에 키우던 햄스터 이름은 '회의록'",
                        Reply =
                        {
                            N("P03", "…정말요? 그럼 제일 통통한 걸로 그려 드릴게요.", Emotion.Smile),
                            N("P03", "이건 당번표 규정에 없어요. 둘만 아는 걸로 해 두죠."),
                        } },
                    new BondChoice { Label = "회의록이라니, 그때부터 회장님이었네요.", Like = 0.04f, Attach = 0.03f,
                        Reply =
                        {
                            N("P03", "중학생 때 지은 이름이에요. 학생회랑은 상관없어요.", Emotion.Smirk),
                            N("P03", "…그러니까 타고났다는 뜻이네요. 인정할게요. 후훗.", Emotion.Laugh),
                        } },
                    new BondChoice { Label = "당번표까지요? 좀 쉬셔도 될 텐데.", Like = -0.02f, Trust = 0.01f,
                        Reply =
                        {
                            N("P03", "누군가는 해야 하잖아요. 제가 하면 빨라요.", Emotion.Blank),
                            N("P03", "쉬는 건… 표가 다 채워지면요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P03_2", Npc = "P03", Stage = 2, Title = "잉크 냄새",
                Lines =
                {
                    N("P03", "서재 종이 냄새가 좋네요. 새 종이 말고, 오래 쌓아 둔 종이요."),
                    N("P03", "저희 집이 인쇄소였어요. 동네에서 제일 오래된 곳이요. 삼십 년 넘게 했어요."),
                    N("P03", "선거철이면 밤새 벽보를 찍었어요. 저는 그 옆에서 숙제하고요.", Emotion.Smile),
                    M("지금은요?"),
                    N("P03", "…다 옛날 얘기예요. 지금은 셔터에 종이가 붙어 있어요. 저희가 찍지 않은 종이요.", Emotion.Sad),
                    N("P03", "괜찮아요. 제가 정리하면 돼요. 정리는 제가 제일 잘하니까요."),
                },
                Choices =
                {
                    new BondChoice { Label = "혼자 정리 안 해도 돼요. 제 몫도 적어 주세요.", Like = 0.06f, Trust = 0.05f, Attach = 0.04f, Reveal = "hint:P03",
                        Reply =
                        {
                            N("P03", "…그 말, 생각보다 무겁게 들리네요.", Emotion.Sad),
                            N("P03", "그럼 딱 한 칸만요. 나머지는 아직 제 거예요."),
                            N("P03", "그 셔터, 운이 나빠서 내려간 게 아니에요. 확인되면 말씀드릴게요."),
                        } },
                    new BondChoice { Label = "벽보 옆에서 숙제하는 서윤 씨, 상상이 가요.", Like = 0.05f, Trust = 0.02f, Attach = 0.04f,
                        Reply =
                        {
                            N("P03", "후훗, 코끝에 늘 잉크가 묻어 있었대요. 아버지가 그걸로 늘 놀리셨어요.", Emotion.Smile),
                            N("P03", "…오랜만에 그 얘기 했네요. 고마워요."),
                        } },
                    new BondChoice { Label = "망한 이유가 뭐였는데요?", Like = -0.03f,
                        Reply =
                        {
                            N("P03", "잠깐. 그건 제가 확인한 것만 말할게요.", Emotion.Angry),
                            N("P03", "아직 확인 중이에요. 그러니까, 오늘은 여기까지요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P03_3", Npc = "P03", Stage = 3, Title = "영수증 뭉치",
                Lines =
                {
                    N("P03", "이거 보실래요? 여기 오던 날 주머니에 있던 거예요. 영수증이요."),
                    N("P03", "전부 제가 대신 낸 거예요. 동생 학원비, 아버지 약값, 가게 전기세."),
                    M("…이걸 계속 들고 다녔어요?"),
                    N("P03", "버리면 안 낸 게 될 것 같아서요. 이상하죠. 그러니까, 소원 얘기예요.", Emotion.Blank),
                    N("P03", "제 힘으로 되는 건 안 빌어요. 이건 안 되는 거라서 빌었어요."),
                },
                Choices =
                {
                    new BondChoice { Label = "들을 수 있는 만큼은 다 들을게요.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P03",
                        Reply =
                        {
                            N("P03", "…돈이요. 저희 가족이 다시 일어설 수 있을 만큼.", Emotion.Sad),
                            N("P03", "빌린 돈 말고요. 아무한테도 고맙다고 안 해도 되는 돈."),
                            N("P03", "저는 도와 달라는 말이 그렇게 어려워요. 그래서 신한테 했어요. 사람 말고요."),
                        } },
                    new BondChoice { Label = "영수증 정리, 같이 할까요?", Like = 0.05f, Trust = 0.03f, Attach = 0.04f,
                        Reply =
                        {
                            N("P03", "후훗, 그건 제 특기인데 뺏으시려고요?", Emotion.Smile),
                            N("P03", "…반만요. 나머지 반은 제가 할게요."),
                        } },
                    new BondChoice { Label = "소원으로 돈을 빌다니, 서윤 씨답지 않네요.", Like = -0.04f,
                        Reply =
                        {
                            N("P03", "저다운 게 뭔데요.", Emotion.Angry),
                            N("P03", "…죄송해요. 방금 건 기록에서 지워 주세요. 저도 지울게요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P03_4", Npc = "P03", Stage = 4, Title = "맨 끝의 이름",
                Lines =
                {
                    M("서윤아, 네 이름은 맨 끝에 쓸게. 늘 그러잖아."),
                    N("P03", "…잠깐. 방금 '서윤아'라고 하셨어요? 그리고 제 이름을 맨 끝에?", Emotion.Surprised),
                    N("P03", "분담표에 제 이름을 맨 끝에 쓰는 거, 제 버릇이에요. 말씀드린 적 없는데요."),
                    N("P03", "이상하네요. 반말도 거슬리지 않았어요. 원래 그렇게 부르던 사람 같아서."),
                    N("P03", "민혁 씨는 가끔 저를 저보다 먼저 정리해요. 무서울 만큼요."),
                    N("P03", "…좋아요. 그럼 저도 정리 안 된 걸 하나 꺼낼게요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "서윤 씨가 확인한 것만, 천천히요.", Like = 0.06f, Trust = 0.08f, Attach = 0.05f,
                        Reveal = "secret:P03;note:서윤은 이현과 인사할 때 펜을 쥔 손이 멈춘다",
                        Reply =
                        {
                            N("P03", "…백이현 씨요. 제 가족은 그 사람 서명 한 번에 전부 잃었어요.", Emotion.Pain),
                            N("P03", "그 사람은 저를 몰라요. 여기서 매일 웃으면서 인사해요."),
                            N("P03", "저는 그때마다 분담표에 그 이름을 제일 반듯하게 써요. 그게 다예요."),
                        } },
                    new BondChoice { Label = "혹시 여기 있는 누구 때문이에요?", Like = 0.02f, Trust = 0.03f, Reveal = "hint:P03",
                        Reply =
                        {
                            N("P03", "…네. 그 이상은 아직요.", Emotion.Blank),
                            N("P03", "확인 안 된 말을 하면, 저도 그 사람이랑 똑같아지니까요."),
                        } },
                    new BondChoice { Label = "정리 안 된 거면 제가 대신 정리해 줄까요?", Like = -0.02f,
                        Reply =
                        {
                            N("P03", "잠깐. 그건 제 일이에요.", Emotion.Angry),
                            N("P03", "남이 대신 정리하는 순간, 그건 더는 제 일이 아니게 돼요. …오늘은 여기까지 할게요."),
                        } },
                },
            });
        }
    }
}
