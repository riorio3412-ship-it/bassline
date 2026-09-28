namespace BL23.Sim
{
    public static partial class LineBank
    {
        // Factions, gatherings and house events (SocialEventsDesign.md). Each resident in their own voice (CharacterBible §2:
        // one owner per tic); slots: {t} the faction's leader, {group} what people call it ("시온네"), {n} how many.
        static void Init_Social()
        {
            // ═════════ 무리 — the faction's own people ═════════
            VP("P03", "tt_faction", "정리하면, 요즘 저희 {n}이 같이 다녀요. 명단은 제가 들고 있어요.");
            VC("P03", "tt_faction", "정리하면, 요즘 우리 {n}이 같이 다녀. 명단은 내가 들고 있어.");
            VP("P05", "tt_faction", "자, 자. 요컨대 뜻이 맞는 사람끼리 모인 것뿐입니다. 문은 열려 있어요.");
            VP("P06", "tt_faction", "흠. 몇 분과 협정을 맺었습니다. 조건은 하나, 서로를 지키는 것.");
            VC("P07", "tt_faction", "요즘 우리끼리 모이는 거 알지? 크하하, 들어오고 싶으면 말만 해.", "우리 무리 이름? {group}. 촌스럽다고? 의리는 안 촌스러워.");
            VC("P09", "tt_faction", "{you:아}, 우리 요즘 같이 다녀. 같이 있으면 덜 무서워서.");
            VP("P10", "tt_faction", "요즘 몇 명이서 같이 밥 먹어요. 우선 앉아요. 자리 하나 더 놓을까요?");
            VP("P12", "tt_faction", "요즘 저희끼리 자주 뭉쳐요. 에헤헤, 무대 동기 같은 느낌?");
            VC("P13", "tt_faction", "솔직히 팀 짜는 게 이기는 법이야. 우리 팀 있어. 한 판 붙어 볼래?");
            VC("P17", "tt_faction", "있지있지, 우리 팀 생겼어! 팀 이름은 아직 판정 중.");
            VP("P18", "tt_faction", "허. 몇 명이서 교대로 서로 살핍니다. 네 시간씩입니다.");
            VP("ANY", "tt_faction", "요즘 몇 명이서 같이 다녀요. 그게 덜 무서워서요.");
            VC("ANY", "tt_faction", "요즘 몇 명이서 같이 다녀. 덜 무서워서.");

            VP("P03", "tt_faction_in", "명단에 넣어 뒀어요. 빠지면 제가 찾으러 가요.");
            VP("P05", "tt_faction_in", "자, 자. 서로 챙기는 게 질서의 첫 문장이죠.");
            VP("P06", "tt_faction_in", "협정은 지킵니다. 됐습니다.");
            VC("P07", "tt_faction_in", "의리지. 크하하.");
            VC("P09", "tt_faction_in", "응. 같이 있으면 대사가 덜 떨려.");
            VP("P10", "tt_faction_in", "밥은 제가 챙겨요. 한 명도 안 빠지게.");
            VC("P11", "tt_faction_in", "오, 우리 팀 규격 딱 맞아. 헤헤.");
            VP("P12", "tt_faction_in", "우리 반짝이 팀이에요. 에헤헤.");
            VC("P13", "tt_faction_in", "우리 쪽 건드리면 GG야.");
            VP("P15", "tt_faction_in", "서로 한 말은 제가 적어 둘게요. 틀리지 않게요.");
            VC("P16", "tt_faction_in", "뭐, 어쨌든. 나쁘지 않아.");
            VC("P17", "tt_faction_in", "판정합니다. 우리 팀 최고!");
            VP("P18", "tt_faction_in", "제 순번은 새벽입니다. 끝났습니다.");
            VP("ANY", "tt_faction_in", "저도 같이요.");
            VC("ANY", "tt_faction_in", "나도 같이야.");

            // ═════════ 무리 — the ones outside it ═════════
            VC("P02", "tt_faction_out", "흐응, {group}? 무리 짓는 거 재밌네. 맞혀 볼까, 누가 제일 먼저 배신할지.", "끼리끼리 모이면 편하지. 나중에 서로 찌르기도 편하고. 큭.");
            VP("P04", "tt_faction_out", "그렇군요. 모두 제자리를 찾아가는군요.");
            VP("P05", "tt_faction_out", "자, 자. 무리가 생기면 파벌이 되고, 파벌은 표가 됩니다. 기억해 두세요.");
            VP("P06", "tt_faction_out", "흠. 무리는 셈이 빠릅니다. 빠지는 사람도 빠르고요.");
            VC("P08", "tt_faction_out", "아 뭐, 몰려다니든가. 난 혼자가 편해.", "됐고, {group} 소리 좀 줄여. 박자가 다 똑같아서 시끄러워.");
            VC("P11", "tt_faction_out", "오, 팀? …난 부품 하나 남는 느낌이네. 헤헤, 괜찮아.");
            VP("P14", "tt_faction_out", "그런가요. 무리는 매듭과 같아서, 풀 때 가장 아픕니다.");
            VP("P15", "tt_faction_out", "잠깐만요. 무리 안에서 나온 말은 출처가 흐려져요. 적어 둘게요.");
            VC("P16", "tt_faction_out", "풋. 끼리끼리. 별로.");
            VC("P13", "tt_faction_out", "솔직히 {group} 좀 거슬려. 우르르 다니는 거.");
            VC("P07", "tt_faction_out", "{group}? 레알? 나 빼고? …아니, 안 부러워.");
            VP("P10", "tt_faction_out", "무리가 어떻든 밥상은 하나예요. 다 같이 먹어요.");
            VC("P17", "tt_faction_out", "있지있지, 팀 나누기? 그럼 난 심판 할래. 판정합니다, 반칙 없기.");
            VP("P03", "tt_faction_out", "정리하면, 무리가 몇 개든 식탁은 하나예요. 다들 여기 앉아요.");
            VC("P09", "tt_faction_out", "{you:아}, 우리도 무리 하나 만들까? …농담이야. 반만.");
            VP("P12", "tt_faction_out", "와, 팀이다. 저도 끼면 안 돼요? 에헤헤…");
            VP("P18", "tt_faction_out", "허. 무리는 셈이 편합니다. 저는 혼자 셉니다.");
            VP("P04", "tt_faction_in", "제자리에 있는 기분이군요. 괜찮으시다면, 계속.");
            VP("P14", "tt_faction_in", "후후. 매듭이 단단하군요.");
            VP("ANY", "tt_faction_out", "…무리 짓는 거, 전 좀 불안해요.");
            VC("ANY", "tt_faction_out", "…몰려다니는 거, 난 좀 불안해.");
        }
    }
}
