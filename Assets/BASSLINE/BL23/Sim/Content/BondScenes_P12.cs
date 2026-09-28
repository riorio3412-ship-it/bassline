using System.Collections.Generic;

namespace BL23.Sim
{
    // P12 오수아 — 아이돌. 밝은 존댓말("꺄", "대박", "에헤헤"). 이미지를 신경 쓰고 속으로는 겁이 많다.
    // 진심일 땐 톤이 낮아지고 '나는'이 주어가 된다. 활동 정지의 배후는 모른다(미확정).
    public static partial class BondScenes
    {
        static partial void Init_P12()
        {
            Add(new BondScene
            {
                Id = "B_P12_1", Npc = "P12", Stage = 1, Title = "스티커 도감",
                Lines =
                {
                    N("P12", "꺄, 민혁 씨! 마침 잘 오셨어요. 손등 좀 빌려주세요. 스티커 하나만요.", Emotion.Grin),
                    N("P12", "별 스티커예요. 팬 사인회 때 제일 잘한 팬한테만 주던 거. 에헤헤."),
                    N("P12", "있잖아요, 여기 와서 벌써 스물세 개 붙였어요. 문고리, 찻잔, 유스티 씨 장갑."),
                    M("유스티 씨 장갑에도요?"),
                    N("P12", "네! 가만히 계시던데요. 어항 물이 잠깐 멈췄어요. 그거 웃은 거 맞죠?", Emotion.Smile),
                },
                Choices =
                {
                    new BondChoice { Label = "제일 잘한 팬이라니, 영광이에요.", Like = 0.06f, Trust = 0.02f, Attach = 0.05f,
                        Reveal = "note:저택 곳곳에 스티커를 붙인다. 유스티의 장갑에도 붙였다",
                        Reply =
                        {
                            N("P12", "에헤헤, 그 말 팬들한테 배웠죠? 반응이 완벽해요.", Emotion.Laugh),
                            N("P12", "…진짜 좋네요. 박수 없는데도요.", Emotion.Smile),
                        } },
                    new BondChoice { Label = "저도 수아 씨한테 하나 붙여 줄게요.", Like = 0.05f, Trust = 0.02f, Attach = 0.05f,
                        Reply =
                        {
                            N("P12", "대박, 역조공! 그럼 여기요, 리본 옆에.", Emotion.Surprised),
                            N("P12", "떼지 말아야지. 오늘은 이거 달고 다닐래요."),
                        } },
                    new BondChoice { Label = "유스티 씨한테까지 그러면 위험하지 않아요?", Like = -0.03f,
                        Reply =
                        {
                            N("P12", "에이, 무시당하는 것보단 나아요.", Emotion.Blank),
                            N("P12", "…아무 반응도 없는 게 제일 무섭거든요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P12_2", Npc = "P12", Stage = 2, Title = "카메라 없는 곳",
                Lines =
                {
                    N("P12", "앗, 보셨어요? 방금 화분한테 인사한 거. 에헤헤, 연습이에요.", Emotion.Surprised),
                    N("P12", "여긴 카메라가 없잖아요. 어디 보고 웃을지 몰라서 꽃 보고 웃어요."),
                    N("P12", "열네 살부터 연습생이었어요. 카메라 빨간 불이 켜지면 몸이 먼저 웃어요."),
                    M("불이 꺼지면요?"),
                    N("P12", "…음~ 꺼지면요. 꺼지면, 나는 좀 없어지는 것 같아요.", Emotion.Sad),
                },
                Choices =
                {
                    new BondChoice { Label = "지금 여기 있잖아요. 제가 보고 있어요.", Like = 0.06f, Trust = 0.05f, Attach = 0.05f, Reveal = "hint:P12",
                        Reply =
                        {
                            N("P12", "…그 말, 카메라보다 좀 무섭네요. 좋은 쪽으로요.", Emotion.Sad),
                            N("P12", "사실 저 쉬는 거 아니에요. 쉬라고 한 거예요. 누가. …다음에 말할게요."),
                        } },
                    new BondChoice { Label = "그럼 제가 빨간 불 할게요. 삐.", Like = 0.05f, Trust = 0.02f, Attach = 0.04f,
                        Reply =
                        {
                            N("P12", "꺄, 뭐예요! 진짜로 웃음이 나왔잖아요.", Emotion.Laugh),
                            N("P12", "…방금 건 연습 아니었어요."),
                        } },
                    new BondChoice { Label = "억지로 웃는 거 힘들잖아요. 안 웃어도 돼요.", Like = -0.02f, Trust = 0.02f,
                        Reply =
                        {
                            N("P12", "힘들죠. 근데 안 웃으면 다들 걱정해요. 그게 더 힘들어요.", Emotion.Blank),
                            N("P12", "에헤헤, 제 걱정은 마세요. 걱정 안 끼치는 게 제 일이에요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P12_3", Npc = "P12", Stage = 3, Title = "복귀 무대",
                Lines =
                {
                    N("P12", "있잖아요, 제 소원은 팬들한테도 아직 말 안 했어요. 첫 공개예요.", Emotion.Smile),
                    N("P12", "민혁 씨가 첫 관객이니까, 조명 대신 이 손전등으로 할게요."),
                    N("P12", "…음~ 이상하다. 무대에선 안 떨리는데. 한 사람 앞에선 더 떨려요.", Emotion.Fear),
                    M("떨려도 괜찮아요. 편집 없어요."),
                    N("P12", "에헤헤, 편집 없으면 더 무섭죠. 좋아요. 말할게요."),
                },
                Choices =
                {
                    new BondChoice { Label = "무대 목소리 말고, 수아 씨 목소리로요.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P12",
                        Reply =
                        {
                            N("P12", "다시 무대에 서는 거예요. 제일 크고, 제일 화려한 데.", Emotion.Sad),
                            N("P12", "나는 조명 아래서만 숨이 제대로 쉬어져요. 조명이 없으면 세상이 그냥 소리 없는 방 같아요."),
                            N("P12", "…촌스럽죠. 근데 진짜예요. 이건 편집하지 마세요."),
                        } },
                    new BondChoice { Label = "먼저 박수부터 칠게요. 크게.", Like = 0.04f, Attach = 0.04f,
                        Reply =
                        {
                            N("P12", "꺄, 벌써요? 아직 말도 안 했는데!", Emotion.Laugh),
                            N("P12", "…박수 받고 나니까 말 못 하겠어요. 너무 좋아서. 다음에 할게요."),
                        } },
                    new BondChoice { Label = "활동 정지된 건 수아 씨가 뭘 잘못해서예요?", Like = -0.05f, Trust = 0.02f,
                        Reply =
                        {
                            N("P12", "…아니에요.", Emotion.Angry),
                            N("P12", "그건 제가 정한 게 아니에요. 오늘 공연은 취소할게요."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P12_4", Npc = "P12", Stage = 4, Title = "전화 한 통",
                Lines =
                {
                    M("수아 씨, 오늘도 반짝, 내일도 반짝."),
                    N("P12", "…방금 그거, 어디서 들으셨어요?", Emotion.Surprised),
                    N("P12", "그거 우리 팬들 인사예요. 여기선 한 번도 안 했는데. 부끄러워서요."),
                    N("P12", "민혁 씨는 가끔 오래된 팬 같아요. 제가 언제 우는지도 아는 사람."),
                    M("…수아 씨가 좋아하는 걸 알고 싶었어요."),
                    N("P12", "그럼 제일 싫어하는 것도 알려 드릴게요. 음~ 어디에도 안 올린 얘기예요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "어디에도 안 올릴게요. 저만 들을게요.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f,
                        Reveal = "secret:P12;note:수아는 정장 차림의 남자가 다가오면 웃음이 굳는다",
                        Reply =
                        {
                            N("P12", "활동 정지, 제 잘못이 아니었어요. 회사에 전화가 한 통 왔대요. 위에서.", Emotion.Pain),
                            N("P12", "정치하는 사람이래요. 누군지는 몰라요. 대표님도 이름은 말 안 했어요."),
                            N("P12", "그래서 여기서 정장 입은 사람한테 웃을 때마다 생각해요. 이 사람일까.", Emotion.Fear),
                        } },
                    new BondChoice { Label = "혹시 쉬게 된 거랑 관련 있어요?", Like = 0.02f, Trust = 0.03f, Reveal = "hint:P12",
                        Reply =
                        {
                            N("P12", "…네. 쉬는 건 제가 정한 게 아니에요. 누가 정했는지는 몰라요.", Emotion.Fear),
                            N("P12", "모르니까 다 무서워요. 누구한테 웃어야 할지."),
                        } },
                    new BondChoice { Label = "그래도 팬들은 기다릴 거예요. 걱정 마요.", Like = -0.02f, Trust = 0.02f,
                        Reply =
                        {
                            N("P12", "…그 말, 제가 팬들한테 하던 말이에요.", Emotion.Sad),
                            N("P12", "들어 보니까 알겠네요. 별로 위로가 안 된다는 거."),
                        } },
                },
            });
        }
    }
}
