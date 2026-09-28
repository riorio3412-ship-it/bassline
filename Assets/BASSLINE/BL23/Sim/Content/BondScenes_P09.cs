using System.Collections.Generic;

namespace BL23.Sim
{
    // P09 문재하 — 배우. 이름부터 부르는 부드러운 반말, 무대 어휘. 긴장하면 유창함이 끊기고,
    // 힘든 일은 듣기 좋은 말로 미룬다. 침묵과 비판을 싫어한다.
    public static partial class BondScenes
    {
        static partial void Init_P09()
        {
            Add(new BondScene
            {
                Id = "B_P09_1", Npc = "P09", Stage = 1, Title = "팸플릿 냄새",
                Lines =
                {
                    N("P09", "민혁아! 이거 봐, 극장 매표소 서랍에서 찾았어. 팸플릿이야.", Emotion.Grin),
                    N("P09", "제목도 없고 배우 이름 칸도 비어 있어. 근데 종이 냄새는 진짜 극장 냄새야."),
                    N("P09", "있잖아, 나 팸플릿 모아. 삼백 장쯤. 공연은 끝나도 이건 남으니까."),
                    M("삼백 장이면 방 하나 가득이겠다."),
                    N("P09", "침대 밑 상자 네 개. 아하하. 이사할 때마다 그것부터 챙겨.", Emotion.Laugh),
                },
                Choices =
                {
                    new BondChoice { Label = "빈 배우 칸에 네 이름 적어 줄까?", Like = 0.06f, Trust = 0.02f, Attach = 0.05f,
                        Reveal = "note:공연 팸플릿을 모은다. 저택 극장에서 배우 칸이 빈 팸플릿을 찾았다",
                        Reply =
                        {
                            N("P09", "세상에, 브라보! 주연으로 적어 줘. 제일 위에.", Emotion.Laugh),
                            N("P09", "…아니다, 두 번째 칸. 첫 칸은 비워 두자. 누가 올지도 모르잖아.", Emotion.Smile),
                        } },
                    new BondChoice { Label = "제일 아끼는 한 장은 뭐야?", Like = 0.05f, Trust = 0.04f, Attach = 0.03f,
                        Reply =
                        {
                            N("P09", "음~ 소극장 첫 공연. 관객 스물두 명. 얼굴이 하나하나 다 기억나."),
                            N("P09", "그중에 맨 앞줄 한 사람은… 아하하, 그건 다음에."),
                        } },
                    new BondChoice { Label = "공연 끝나면 그냥 종이 아니야?", Like = -0.04f,
                        Reply =
                        {
                            N("P09", "…민혁아, 그건 좀 아프다.", Emotion.Sad),
                            N("P09", "종이라도 남아야 끝난 게 덜 끝난 거야."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P09_2", Npc = "P09", Stage = 2, Title = "짐 빼 간 날",
                Lines =
                {
                    N("P09", "민혁아, 아무 말이나 좀 해 줘. 여기 너무 조용해.", Emotion.Fear),
                    N("P09", "나 침묵이 싫어. 무대에서 대사 까먹은 삼 초, 그게 평생 같거든."),
                    N("P09", "음~ 원래는 괜찮았어. 집에 왔는데 너무 조용했던 날이 있었거든. 그날부터 싫어졌어."),
                    M("무슨 날이었는데?"),
                    N("P09", "…누가 짐을 다 빼 간 날. 아하하. 대사처럼 말하니까 좀 낫네.", Emotion.Sad),
                },
                Choices =
                {
                    new BondChoice { Label = "대사 말고 그냥 말해. 조용해지면 내가 채울게.", Like = 0.06f, Trust = 0.05f, Attach = 0.05f, Reveal = "hint:P09",
                        Reply =
                        {
                            N("P09", "…민혁아, 그거 반칙이야. 울면 분장 번진다고.", Emotion.Crying),
                            N("P09", "같이 살던 사람이 있었어. 말 한마디 없이 갔어. 쪽지에 두 단어만 남기고."),
                        } },
                    new BondChoice { Label = "그럼 즉흥극 하자. 상대역은 내가 할게.", Like = 0.05f, Trust = 0.02f, Attach = 0.04f,
                        Reply =
                        {
                            N("P09", "세상에, 좋아! 장면은… 텅 빈 집에 돌아온 배우.", Emotion.Grin),
                            N("P09", "…아니다, 다른 장면. 그건 너무 잘 알아서 연기가 안 돼."),
                        } },
                    new BondChoice { Label = "짐 빼 간 거면, 헤어진 거네.", Like = -0.04f,
                        Reply =
                        {
                            N("P09", "그렇게 요약하지 마. 비평가처럼.", Emotion.Angry),
                            N("P09", "막이 내려도 배우는 한동안 무대에 서 있어. 그런 거야."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P09_3", Npc = "P09", Stage = 3, Title = "리허설",
                Lines =
                {
                    N("P09", "민혁아, 소원 말이야. 난 '해피엔딩'이라고 대답하고 다녀. 제목만.", Emotion.Smile),
                    N("P09", "다들 박수 쳐 줘. 좋은 대답이래. 근데 그건 대본이야."),
                    N("P09", "있잖아, 너한테는 본 공연 말고 리허설을 보여 주고 싶어. 엉망인 거."),
                    M("엉망이어도 볼게."),
                    N("P09", "…음~ 대사가 안 붙네. 잠깐만. 조명 좀 낮춰 줄래?", Emotion.Fear),
                },
                Choices =
                {
                    new BondChoice { Label = "조명 없이도 들을게. 천천히 해.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P09",
                        Reply =
                        {
                            N("P09", "떠난 사람이 돌아오는 거. 그게 내 소원이야.", Emotion.Sad),
                            N("P09", "돌아오기만 하면 다 괜찮아질 거야. 그 사람도 분명… 음, 분명 그럴 거야.", Emotion.Pain),
                            N("P09", "브라보 해 줘. 아니, 하지 마. 오늘은 박수 없는 게 좋다."),
                        } },
                    new BondChoice { Label = "해피엔딩이면 됐지, 뭘.", Like = 0.03f, Attach = 0.02f,
                        Reply =
                        {
                            N("P09", "그치? 아하하. 역시 그 대답이 제일 잘 먹혀.", Emotion.Laugh),
                            N("P09", "…고마워. 도망갈 문 열어 줘서."),
                        } },
                    new BondChoice { Label = "그 사람도 돌아오고 싶어 할까?", Like = -0.05f, Trust = 0.02f,
                        Reply =
                        {
                            N("P09", "그런 질문은… 대본에 없어.", Emotion.Break),
                            N("P09", "리허설 끝. 다들 퇴장.", Emotion.Blank),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P09_4", Npc = "P09", Stage = 4, Title = "빈 객석",
                Lines =
                {
                    N("P09", "민혁아, 거기… 앉지 마. 아니, 앉아도 돼. 근데 왜 하필 그 자리야?", Emotion.Surprised),
                    N("P09", "셋째 줄 일곱 번째. 그 사람 자리야. 첫 공연 땐 맨 앞줄, 그다음부턴 늘 거기."),
                    N("P09", "넌 가끔 내 다음 대사를 나보다 먼저 알아. 같은 무대를 두 번 본 관객처럼."),
                    M("…여기 앉아 있으면, 무슨 얘기든 들을 수 있을 것 같아서."),
                    N("P09", "그럼 오늘은 리허설 말고 커튼 뒤 얘기 할게. 대본에 없는 거.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "대본에 없어도 괜찮아. 끝까지 있을게.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P09",
                        Reply =
                        {
                            N("P09", "그 사람이 짐 뺀 날, 식탁에 메모가 있었어. '기다리지 마.'", Emotion.Pain),
                            N("P09", "난 그걸 '기다려'로 읽기로 했어. 한 글자쯤 틀려도 연기는 되니까."),
                            N("P09", "돌아오고 싶은지 한 번도 안 물어봤어. 물어보면… 공연이 끝나잖아.", Emotion.Crying),
                        } },
                    new BondChoice { Label = "그 사람 이름, 물어봐도 돼?", Like = 0.02f, Trust = 0.03f, Reveal = "hint:P09",
                        Reply =
                        {
                            N("P09", "…이름을 부르면 진짜 없는 게 돼. 그러니까 아직은 '그 사람'.", Emotion.Sad),
                            N("P09", "미안. 이건 대사 아니야."),
                        } },
                    new BondChoice { Label = "그만 기다리는 게 너한테도 좋지 않을까?", Like = -0.05f,
                        Reply =
                        {
                            N("P09", "비판은 사양할게, 민혁아.", Emotion.Angry),
                            N("P09", "…알아. 아는데, 그 말은 막 내린 다음에 해 줘."),
                        } },
                },
            });
        }
    }
}
