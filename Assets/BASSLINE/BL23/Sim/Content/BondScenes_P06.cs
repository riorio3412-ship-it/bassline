using System.Collections.Generic;

namespace BL23.Sim
{
    // P06 권태겸 — 사무적 존댓말(기한·수량·조건). 호의를 빚으로 받아들여 도움을 청하지 못한다.
    // 쓴 초콜릿·대여표·첫 로고 포장지. 돈만 말하는 사람이 아니다: 손해를 감수하는 이유가 있다.
    public static partial class BondScenes
    {
        static partial void Init_P06()
        {
            Add(new BondScene
            {
                Id = "B_P06_1", Npc = "P06", Stage = 1, Title = "대여표",
                Lines =
                {
                    N("P06", "대여표 쓰시죠. 품목, 수량, 반납 시각. 셋만 적으면 됩니다."),
                    M("손전등 하나 빌리는 건데 이것까지요?"),
                    N("P06", "계산해 보면 이 저택에서 나는 다툼의 절반은 물건 때문입니다. 적어 두면 줄어요."),
                    N("P06", "…됐습니다. 글씨가 반듯하군요. 반납도 반듯하게 하실 분이네요.", Emotion.Smile),
                    N("P06", "쓴 초콜릿 하나 드시죠. 아, 대가는 없습니다. 견본이에요."),
                },
                Choices =
                {
                    new BondChoice { Label = "견본이면 감상을 드릴게요. 맛있어요.", Like = 0.06f, Trust = 0.03f, Attach = 0.04f,
                        Reveal = "note:빌려준 물건은 대여표로 관리한다. 호의에도 대가를 붙인다",
                        Reply =
                        {
                            N("P06", "흠. 감상은 받겠습니다. 거래 성립이군요.", Emotion.Smile),
                            N("P06", "…사실 견본 아닙니다. 그냥 드린 거예요. 장부엔 견본으로 적을게요."),
                        } },
                    new BondChoice { Label = "반납이 1분 늦으면 어떻게 돼요?", Like = 0.04f, Trust = 0.02f, Attach = 0.02f,
                        Reply =
                        {
                            N("P06", "연체료는 초콜릿 한 조각입니다. 민혁 씨가 저한테 내는 쪽으로요.", Emotion.Smirk),
                            N("P06", "농담 아닙니다. 단 건 안 먹으니 쓴 걸로요."),
                        } },
                    new BondChoice { Label = "이런 데서까지 장사하세요?", Like = -0.03f,
                        Reply =
                        {
                            N("P06", "장사가 아니라 질서입니다.", Emotion.Blank),
                            N("P06", "공짜는 믿지 않아요. 여기선 특히."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P06_2", Npc = "P06", Stage = 2, Title = "첫 로고",
                Lines =
                {
                    N("P06", "이거요? 포장지입니다. 회사 첫 로고가 찍힌 거. 지갑에 넣어 다녀요."),
                    N("P06", "로고는 제가 그렸어요. 삐뚤죠. 디자이너 쓸 돈이 없었습니다."),
                    N("P06", "직원이 셋이에요. 그중 한 명은 이 로고를 보고 입사했대요. 흠, 이상한 사람이죠.", Emotion.Smile),
                    M("보고 싶겠네요, 그 사람들."),
                    N("P06", "…여기 오기 전날이 25일이었어요. 월급날이요. 이체 버튼을 눌렀는지 기억이 안 납니다.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "눌렀을 거예요. 기한 안 넘기는 분이잖아요.", Like = 0.06f, Trust = 0.05f, Attach = 0.04f, Reveal = "hint:P06",
                        Reply =
                        {
                            N("P06", "…그 말은 믿고 싶군요. 계산 없이.", Emotion.Sad),
                            N("P06", "사실 잔고가 모자랐어요. 그래서 다른 데서 돈을 좀… 됐습니다. 여기까지."),
                        } },
                    new BondChoice { Label = "로고 삐뚤어진 거, 전 좋은데요.", Like = 0.05f, Trust = 0.02f, Attach = 0.04f,
                        Reply =
                        {
                            N("P06", "흠. 첫 거래처 사장님도 그 말을 하셨어요. 그래서 계약했습니다.", Emotion.Smile),
                            N("P06", "…칭찬은 받겠습니다. 대가는 나중에."),
                        } },
                    new BondChoice { Label = "월급보다 지금 살아남는 게 먼저 아니에요?", Like = -0.03f,
                        Reply =
                        {
                            N("P06", "계산 순서는 제가 정합니다.", Emotion.Angry),
                            N("P06", "약속한 날짜를 어기는 건, 저한텐 죽는 것만큼 비쌉니다."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P06_3", Npc = "P06", Stage = 3, Title = "손해 보는 계산",
                Lines =
                {
                    N("P06", "소원 얘기를 하죠. 조건은 하나, 금액은 묻지 마십시오."),
                    N("P06", "…농담입니다. 금액이 없어서요. 적을 수 있는 숫자가 아니라서.", Emotion.Smirk),
                    N("P06", "계산해 보면 저는 평생 누군가에게 빚지고 살았어요. 돈이든, 호의든."),
                    M("호의도 빚이에요?"),
                    N("P06", "제일 비싼 빚이죠. 이자가 안 적혀 있으니까요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "그럼 이건 빚 아니에요. 그냥 들을게요.", Like = 0.06f, Trust = 0.06f, Attach = 0.05f, Reveal = "contract:P06",
                        Reply =
                        {
                            N("P06", "…됐습니다. 그럼 공짜로 말하죠. 처음 해 보는 거래네요.", Emotion.Smile),
                            N("P06", "막대한 돈이요. 누구한테도 다시는 손 벌리지 않아도 될 만큼."),
                            N("P06", "빚진 데를 전부 끊고 싶습니다. 안 끊기는 한 곳까지요.", Emotion.Pain),
                        } },
                    new BondChoice { Label = "제가 태겸 씨한테 빚지면 되겠네요.", Like = 0.04f, Trust = 0.02f, Attach = 0.03f,
                        Reply =
                        {
                            N("P06", "흠. 채권자가 되는 건 편하죠.", Emotion.Smirk),
                            N("P06", "…근데 민혁 씨한테는 받고 싶지가 않네요. 계산이 틀렸나."),
                        } },
                    new BondChoice { Label = "돈으로 다 해결될까요?", Like = -0.03f,
                        Reply =
                        {
                            N("P06", "안 되는 걸 말해 보십시오. 대부분 돈이 모자라서 안 되는 겁니다.", Emotion.Blank),
                            N("P06", "…오늘은 여기까지. 이 대화도 장부에 적어 두겠습니다."),
                        } },
                },
            });

            Add(new BondScene
            {
                Id = "B_P06_4", Npc = "P06", Stage = 4, Title = "두 번째 장부",
                Lines =
                {
                    N("P06", "…초콜릿 싼 종이. 삼각으로 두 번, 끝은 안으로. 어디서 배우셨습니까.", Emotion.Surprised),
                    N("P06", "우리 창고에서만 쓰는 포장입니다. 직원 셋한테 제가 가르친 거예요."),
                    N("P06", "민혁 씨는 가끔 제 장부를 읽어 본 사람처럼 굽니다. 다음 줄까지요."),
                    M("…그냥 손이 그렇게 갔어요."),
                    N("P06", "흠. 빚진 기분인데, 이상하게 갚고 싶지가 않네요."),
                    N("P06", "대가로 하나 드리죠. 제 장부는 한 권이 아닙니다. 남들은 대개 한 권이지만요.", Emotion.Blank),
                },
                Choices =
                {
                    new BondChoice { Label = "두 번째 장부 얘기, 대가 없이 들을게요.", Like = 0.05f, Trust = 0.08f, Attach = 0.05f, Reveal = "secret:P06",
                        Reply =
                        {
                            N("P06", "남의 돈을 맡아 관리했습니다. 정치인 돈이요. 어디서 난 돈인지는 안 묻는 조건으로.", Emotion.Blank),
                            N("P06", "월급 날짜를 지키려고 받았어요. 한 번이 열 번이 됐고."),
                            N("P06", "그 사람, 여기 있습니다. 백이현. 제 두 번째 장부의 첫 줄이죠.", Emotion.Pain),
                        } },
                    new BondChoice { Label = "그 장부, 여기서 누가 알아요?", Like = 0.02f, Trust = 0.03f,
                        Reveal = "hint:P06;note:태겸은 이현이 말할 때 회중시계를 본다. 기한을 재듯이",
                        Reply =
                        {
                            N("P06", "아는 사람이 하나. 아는 척 안 하는 사람도 하나.", Emotion.Smirk),
                            N("P06", "…누군지는 계산이 끝나면 말씀드리죠."),
                        } },
                    new BondChoice { Label = "불법이면 지금이라도 털어놓는 게 낫잖아요.", Like = -0.04f,
                        Reply =
                        {
                            N("P06", "조언은 공짜가 아닙니다. 제 손해를 대신 져 주실 겁니까?", Emotion.Angry),
                            N("P06", "…됐습니다. 오늘 건 장부에 안 적겠습니다."),
                        } },
                },
            });
        }
    }
}
