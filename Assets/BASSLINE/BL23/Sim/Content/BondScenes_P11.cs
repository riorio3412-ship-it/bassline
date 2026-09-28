using System.Collections.Generic;

namespace BL23.Sim
{
    // P11 윤해린 — 기계공학 전문가. 빠르고 짧은 반말, 수리·검증 어휘("잠깐", "그러니까", "오!").
    // 실패를 인정할 때만 말이 느려진다. 대충과 거짓말을 싫어한다.
    public static partial class BondScenes
    {
        static partial void Init_P11()
        {
            Add(new BondScene
            {
                Id = "B_P11_1", Npc = "P11", Stage = 1, Title = "태엽 새",
                Lines =
                {
                    N("P11", "오! 이거 봐. 전시실 새장에 있던 태엽 새. 부리가 안 닫혀서 뜯었어.", Emotion.Grin),
                    N("P11", "잠깐, 만지지 마. 스프링이 반대로 감겨 있어. 이런 설계 처음 봐."),
                    N("P11", "태엽은 정직해. 감은 만큼만 움직이잖아. 사람은 안 그런데."),
                    M("고치면 어떻게 되는데?"),
                    N("P11", "원래는 노래하는 거야. 근데 얘는 방금 들은 말을 흉내 내. 헤헤, 소름 돋지?", Emotion.Smile),
                },
                Choices =
                {
                    new BondChoice { Label = "나사 잡아 줄까? 네가 말하는 대로 할게.", Like = 0.06f, Trust = 0.03f, Attach = 0.04f,
                        Reveal = "note:전시실 태엽 새는 방에서 들린 말을 흉내 낸다. 해린이 고치고 있다",
                        Reply =
                        {
                            N("P11", "오, 좋아. 드라이버 말고 손톱으로. 살살. …그렇지!", Emotion.Smile),
                            N("P11", "손이 안 떨리네. 조수 합격. 탄산수 하나 줄게. 흔들지 말고 따."),
                        } },
                    new BondChoice { Label = "그 새가 뭘 흉내 냈는데?", Like = 0.04f, Trust = 0.04f, Attach = 0.02f,
                        Reply =
                        {
                            N("P11", "…'왼쪽으로 1도.' 전시실에서 누가 그렇게 중얼거렸나 봐. 목소리는 모르겠어.", Emotion.Surprised),
                            N("P11", "헤헤, 무슨 암호 같지? 누가 새장 앞에서 혼잣말한 거겠지 뭐."),
                        } },
                    new BondChoice { Label = "그냥 망가진 장난감 아니야?", Like = -0.03f,
                        Reply =
                        {
                            N("P11", "대충 보면 다 그렇게 보여.", Emotion.Disgust),
                            N("P11", "이건 대충 볼 물건이 아니야. 비켜 봐, 조명 가려."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P11_2", Npc = "P11", Stage = 2, Title = "못 고치는 것",
                Lines =
                {
                    N("P11", "세나가 또 물어봤어. 고칠 수 있냐고. 뭘 말하는지는 걔 사정이라 패스.", Emotion.Blank),
                    N("P11", "구조는 알 것 같아. 근데 걔가 원하는 건 구조가 아니잖아."),
                    N("P11", "난 고치는 사람이야. 못 고치면… 그러니까, 그냥 사람이지. 쓸모없는."),
                    M("쓸모로만 사람을 재진 않잖아."),
                    N("P11", "잠깐, 그거 위로야? 검증된 문장이야?", Emotion.Smirk),
                },
                Choices =
                {
                    new BondChoice { Label = "검증은 못 했어. 그래도 진짜야.", Like = 0.06f, Trust = 0.06f, Attach = 0.04f, Reveal = "hint:P11",
                        Reply =
                        {
                            N("P11", "…헤헤. 모른다고 하는 사람 좋아. 된다고 우기는 사람보다.", Emotion.Smile),
                            N("P11", "나도 한 번 우긴 적 있어. 된다고. 확인도 안 하고. …오늘은 여기까지."),
                        } },
                    new BondChoice { Label = "그럼 내 것도 하나 고쳐 줘. 고민 하나.", Like = 0.04f, Trust = 0.02f, Attach = 0.04f,
                        Reply =
                        {
                            N("P11", "고민은 부품이 없잖아. 어디가 헐거운데?", Emotion.Surprised),
                            N("P11", "…말해 봐. 못 고쳐도 조여는 줄게."),
                        } },
                    new BondChoice { Label = "고치는 거 말고 잘하는 건 없어?", Like = -0.04f,
                        Reply =
                        {
                            N("P11", "…없어. 그래서 고치는 거야.", Emotion.Sad),
                            N("P11", "됐어. 나 부품 찾으러 간다."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P11_3", Npc = "P11", Stage = 3, Title = "되감기",
                Lines =
                {
                    N("P11", "태엽은 거꾸로 감으면 끊어져. 알아? 되감기는 설계에 없어."),
                    N("P11", "근데 난 되감기를 빌었어. 웃기지. 기계 하는 사람이."),
                    M("뭘 되감고 싶은데?"),
                    N("P11", "그러니까… 잠깐. 탄산수 좀 따고. …손이 좀 미끄럽네.", Emotion.Fear),
                    N("P11", "말하면 따져 봐야 되잖아. 내가 한 게 맞았는지. 그게 무서워."),
                },
                Choices =
                {
                    new BondChoice { Label = "따지는 건 나중에. 지금은 그냥 들을게.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P11",
                        Reply =
                        {
                            N("P11", "…사고가 있었어. 내가 수리해서 넘긴 물건에서.", Emotion.Sad),
                            N("P11", "그걸 되돌리고 싶어. 사고 나기 전으로. 그게 내 소원이야."),
                            N("P11", "결함은… 아니, 오늘은 여기까지. 말이 느려지면 나 진짜 못 해."),
                        } },
                    new BondChoice { Label = "탄산수는 내가 따 줄게.", Like = 0.04f, Trust = 0.02f, Attach = 0.04f,
                        Reply =
                        {
                            N("P11", "흔들지 마. …오, 안 터졌다. 헤헤.", Emotion.Smile),
                            N("P11", "고마워. 소원 얘기는… 다음에 풀어 볼게."),
                        } },
                    new BondChoice { Label = "되감기는 없다고 방금 네가 말했잖아.", Like = -0.04f,
                        Reply =
                        {
                            N("P11", "알아. 내가 제일 잘 알아.", Emotion.Angry),
                            N("P11", "그러니까 빈 거야. 없는 거라서."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P11_4", Npc = "P11", Stage = 4, Title = "검수 도장",
                Lines =
                {
                    N("P11", "…잠깐. 그 드라이버. 테이프 감긴 거. 달라고 안 했는데.", Emotion.Surprised),
                    N("P11", "공구가 열두 개나 있는데 넌 꼭 그것만 줘. 내가 그것만 쓰는 거 어떻게 알아?"),
                    N("P11", "너 가끔 내 손보다 먼저 움직여. 같이 작업해 본 사람처럼. 설명이 안 돼."),
                    M("…틀렸으면 말해. 다시 고를게."),
                    N("P11", "안 틀렸어. 그래서 무서운 거야. …말할게. 좀 느려도 들어 줘.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "느려도 괜찮아. 끝까지 기다릴게.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P11",
                        Reply =
                        {
                            N("P11", "리프트 제어기였어. 마감이 내일이었고. 센서에 결함이 있는 거… 알았어.", Emotion.Pain),
                            N("P11", "다음 주에 고치면 된다고 생각했어. 검수 도장 찍고… 넘겼어."),
                            N("P11", "의뢰한 사람이 죽었어. 그 리프트에서. 그러니까… 난 고치는 사람이 아니야.", Emotion.Crying),
                        } },
                    new BondChoice { Label = "사고 난 물건, 뭐였어?", Like = 0.02f, Trust = 0.03f, Reveal = "hint:P11",
                        Reply =
                        {
                            N("P11", "…제어기. 올라가고 내려가는 거.", Emotion.Blank),
                            N("P11", "그 이상은 나중에. 나도 아직 내 속을 다 못 뜯어봤어."),
                        } },
                    new BondChoice { Label = "결함 알고 넘긴 거지? 얼굴에 쓰여 있어.", Like = -0.05f,
                        Reply =
                        {
                            N("P11", "넘겨짚지 마. 너까지 대충 하지 마.", Emotion.Angry),
                            N("P11", "나한테 '대충'은… 사람 죽이는 단어야."),
                        } },
                },
            });
        }
    }
}
