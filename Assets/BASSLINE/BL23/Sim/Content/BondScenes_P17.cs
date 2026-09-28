using System.Collections.Generic;

namespace BL23.Sim
{
    // P17 송예담 — 숏폼 창작자. 높낮이 큰 반말("짜잔!", "에엥?", "있지있지", "히히"). 놀이·퀴즈·초대장.
    // 진심일 때는 운율과 장난이 사라지고 문장이 평평해진다. 자기 이름으로 자신을 부르지 않는다.
    public static partial class BondScenes
    {
        static partial void Init_P17()
        {
            Add(new BondScene
            {
                Id = "B_P17_1", Npc = "P17", Stage = 1, Title = "초대장",
                Lines =
                {
                    N("P17", "짜잔! 초대장! 오늘 밤 라운지, '저택 퀴즈쇼' 제1회!", Emotion.Grin),
                    N("P17", "받는 사람 명단에 너 일 번. 거절 버튼 없는 초대장이야. 히히."),
                    N("P17", "있지있지, 종이 모서리 봐. 계단 모양으로 접었어. 이 저택 계단!"),
                    M("계단이 몇 개인지까지 셌어요?"),
                    N("P17", "당연하지! 올라갈 땐 열세 개. 근데 내려갈 땐 열네 개야. 이것도 퀴즈에 나와.", Emotion.Smile),
                },
                Choices =
                {
                    new BondChoice { Label = "갈게요. 대신 저도 문제 하나 낼래요.", Like = 0.06f, Trust = 0.03f, Attach = 0.05f,
                        Reveal = "note:저택 계단은 오를 때 열세 개, 내려갈 때 열네 개라고 한다",
                        Reply =
                        {
                            N("P17", "에엥? 출연자가 문제를? 좋아, 특별 코너 만든다!", Emotion.Laugh),
                            N("P17", "너 진짜 좋다. 다들 이렇게만 해 주면 좋을 텐데."),
                        } },
                    new BondChoice { Label = "거절 버튼이 없는 건 좀 반칙 아니에요?", Like = 0.02f, Trust = 0.02f,
                        Reply =
                        {
                            N("P17", "반칙 아니야. 기능이야.", Emotion.Smirk),
                            N("P17", "…근데 진짜 싫으면 말해. 음, 말하지 마. 그냥 와."),
                        } },
                    new BondChoice { Label = "전 퀴즈 별로 안 좋아해서요.", Like = -0.05f,
                        Reply =
                        {
                            N("P17", "…아.", Emotion.Blank),
                            N("P17", "그래. 괜찮아. 다른 사람한테 줄게. 일 번은… 비워 둘게."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P17_2", Npc = "P17", Stage = 2, Title = "종이 저택",
                Lines =
                {
                    N("P17", "짜잔, 종이 모형! 이 저택 축소판. 창문은 아직 다 못 뚫었어.", Emotion.Grin),
                    N("P17", "어릴 때 언니랑 이런 거 만들었어. 언니가 설계, 나는 풀칠 담당."),
                    N("P17", "언니가 찍고 내가 나오고. 그게 우리 첫 영상이었어. 조회수 열한 번."),
                    M("언니는 지금 뭐 해?"),
                    N("P17", "…실종됐어. 몇 년 전에. 그렇게 말하는 게 편해. 다음 퀴즈!", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "다음 퀴즈 전에, 조금만 더 들어도 돼?", Like = 0.05f, Trust = 0.06f, Attach = 0.04f, Reveal = "hint:P17",
                        Reply =
                        {
                            N("P17", "근데에, 들어도 재미없어.", Emotion.Sad),
                            N("P17", "언니 번호 아직 외우고 있어. 누른 적은 없어. 그게 다야. 이제 진짜 다음 퀴즈."),
                        } },
                    new BondChoice { Label = "열두 번째 시청자는 내가 할게.", Like = 0.05f, Trust = 0.02f, Attach = 0.05f,
                        Reply =
                        {
                            N("P17", "히히, 열두 번째 시청자! 특전은 풀칠 체험.", Emotion.Laugh),
                            N("P17", "…고마워. 언니도 이런 말 할 줄 알았는데."),
                        } },
                    new BondChoice { Label = "실종이면 신고는 했어?", Like = -0.04f, Trust = 0.01f,
                        Reply =
                        {
                            N("P17", "에엥? 갑자기 뉴스 톤이야.", Emotion.Surprised),
                            N("P17", "…그런 거 묻지 마. 지루해."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P17_3", Npc = "P17", Stage = 3, Title = "주소 한 줄",
                Lines =
                {
                    N("P17", "퀴즈! 내 소원은 뭘까? 힌트, 종이 한 장에 들어가.", Emotion.Grin),
                    M("…편지?"),
                    N("P17", "땡. 근데 가까워. 편지를 보낼 수 있게 해 주는 거."),
                    N("P17", "…퀴즈 그만할래. 이건 퀴즈로 하면 안 되는 거 같아.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "퀴즈 아니어도 돼. 그냥 말해 줘.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P17",
                        Reply =
                        {
                            N("P17", "언니 주소. 지금 사는 데. 그리고 딱 한 번 만나는 거.", Emotion.Blank),
                            N("P17", "만나서 할 말이 있어. 무슨 말인지는 아직 몰라. 만나면 생각나겠지."),
                            N("P17", "한 번이면 돼. 두 번은 욕심이니까.", Emotion.Sad),
                        } },
                    new BondChoice { Label = "정답 맞히면 상품 있어?", Like = 0.03f, Attach = 0.03f,
                        Reply =
                        {
                            N("P17", "상품은… 다음 퀴즈 참가권! 히히.", Emotion.Laugh),
                            N("P17", "정답은 비밀. 방송 사고 날 것 같아서."),
                        } },
                    new BondChoice { Label = "실종된 사람 주소를 신이 알까?", Like = -0.04f, Trust = 0.02f, Reveal = "hint:P17",
                        Reply =
                        {
                            N("P17", "알겠지. 신이잖아.", Emotion.Blank),
                            N("P17", "실종이라는 건… 그냥 내가 붙인 제목이야. 아, 방금 거 편집."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P17_4", Npc = "P17", Stage = 4, Title = "지운 문자",
                Lines =
                {
                    N("P17", "있지있지, 문제! 심장이 세 개인 동물은—", Emotion.Grin),
                    M("문어."),
                    N("P17", "…에엥? 아직 문제 다 안 냈는데.", Emotion.Surprised),
                    N("P17", "그거 언니가 내던 문제야. 매일 아침. 여기선 한 번도 안 냈어."),
                    N("P17", "너 가끔 그래. 내가 뭐 할지 알고 먼저 비켜 줘. 전에 같이 찍어 본 사람처럼."),
                    N("P17", "…그럼 이것도 알 수도 있겠다. 알아도 모르는 척해 줘. 그리고 들어 줘.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "모르는 척할게. 끝까지 들을게.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P17",
                        Reply =
                        {
                            N("P17", "언니 실종 아니야. 이사 간다고 연락 왔었어. 새 주소도 보내 줬어.", Emotion.Blank),
                            N("P17", "답장 안 했어. 가지 말라고 하면 거절당할까 봐. 그래서 문자를 지웠어."),
                            N("P17", "그러고 나서 사람들한테 실종이라고 했어. 그럼 내가 놓아 버린 게 아니게 되니까.", Emotion.Crying),
                        } },
                    new BondChoice { Label = "언니한테 마지막으로 보낸 게 뭐였어?", Like = 0.02f, Trust = 0.04f, Reveal = "hint:P17",
                        Reply =
                        {
                            N("P17", "…보낸 거 없어. 받은 것만 있어.", Emotion.Sad),
                            N("P17", "그게 무슨 뜻인지는, 다음 회에."),
                        } },
                    new BondChoice { Label = "알아. 너 언니한테 연락 안 한 거지?", Like = -0.05f,
                        Reply =
                        {
                            N("P17", "스포하지 마.", Emotion.Angry),
                            N("P17", "그건 내가 말해야 되는 거였어. 내가."),
                        } },
                },
            });
        }
    }
}
