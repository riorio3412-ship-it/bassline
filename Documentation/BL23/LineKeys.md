# BL23 대사 키와 작성 규칙 (LineBank)

대사는 **구조화된 상황 키**로 호출되고, 인물별로 존댓말(P)/반말(C) 변형을 가진다. 논리 판정은 대사 문장이 아니라 커널의 명제가 담당하므로, 대사는 그 명제를 인물의 말투로 "렌더"하는 역할이다. 모르는 사실을 말하게 하거나 정답을 암시하는 문장을 넣지 않는다.

## 파일 형식
`Assets/BASSLINE/BL23/Sim/Content/Lines_<ID>.cs` (ID = P01..P18, NPC00, ANY)
```csharp
namespace BL23.Sim
{
    public static partial class LineBank
    {
        static void Init_P03()
        {
            Add("P03", "greet", P("안녕하세요, {you}.", "{you}, 좋은 아침이에요."), C("안녕, {you}.", "왔어?"));
            Add("P03", "busy", P("미안해요, 지금 {act} 중이라서요."), C("미안, 지금 {act} 하는 중이야."));
            // ...
        }
    }
}
```
- `P(...)` = 존댓말 변형들, `C(...)` = 반말 변형들. 둘 중 하나만 있어도 된다(`C()`만 쓰면 항상 반말 인물).
- 변형은 키마다 **최소 2개**, 성격이 잘 드러나는 핵심 키(small_talk, confess, accuse, object, secret_share 등)는 3~6개.
- 한 줄은 자막 한 칸 기준 **60자 이내**. 긴 말은 `|`로 나눠 연속 자막으로 만든다(예: `"그건…|아니, 역시 말할 수 없어요."`).
- 문장은 **완성된 한국어**. 유아어·과한 이모티콘·밈 남발 금지. 욕설은 인물 성격상 필요한 최소한(시온·라온 정도)만.
- 폭력 묘사는 절제한다. 살해 방법의 현실적 절차를 설명하지 않는다.

## 자리표시자와 조사
- `{you}` 대화 상대(렌더러가 관계에 따라 "민혁 씨" / "민혁"으로 치환)
- `{t}` 제3자 이름, `{victim}` 피해자, `{place}` 장소명, `{time}` "21:40" 형식, `{item}` 물건 이름, `{act}` 활동 이름, `{topic}` 화제, `{sound}` 소리 종류, `{reason}` 이유 구절, `{n}` 숫자
- **조사 표기**: 자리표시자 뒤에 `:조사`를 붙이면 받침에 맞게 자동 선택된다.
  `{t:이}`→이/가, `{t:을}`→을/를, `{t:은}`→은/는, `{t:와}`→와/과, `{t:로}`→으로/로, `{you:아}`→(반말일 때) 아/야 호격, `{t:이랑}`→이랑/랑, `{t:이나}`→이나/나
  예: `"{t:이} {place}에서 {item:을} 들고 있었어."`
- 괄호 안 지문 금지(자막에 그대로 나온다). 감정은 문장 자체로.

## 인물 말투 요약 (03 부록 C.2 + BL23 리디자인)
| ID | 인물 | 말투 |
|---|---|---|
| P01 | 김민혁(플레이어) | 부드러운 존댓말, 합의 후 반말. 다정하지만 책임지려 함. |
| P02 | 김진우 | 짧은 반말·질문·되받기. 상대 반응을 시험. 진심일 때 웃음이 사라짐. 사탕. |
| P03 | 한서윤 | 단정한 존댓말, 합의 후 담백한 반말. 정리·분담·기록. 도움받기 어려워함. |
| P04 | 차도윤 | 대외 낮은 존댓말, 친밀하면 제한적 반말. 예의·세심 아래 통제욕. 복원·무향차. |
| P05 | 백이현 | 공개 존댓말·상대 말 요약("말씀하신 건…"), 사적 친분엔 반말. 공익 언어로 사익 포장. |
| P06 | 권태겸 | 사무적 존댓말, 갈등 땐 짧은 반말. 교환·조건·대가. 쓴 초콜릿. |
| P07 | 유시온 | 큰 친근한 반말(브로), 사과/진심은 작아짐. 매 문장 랩 금지. 허세 뒤 인정 욕구. |
| P08 | 서라온 | 건조한 반말·비꼼, 진심이면 농담 중단. 소리·리듬. |
| P09 | 문재하 | 이름부터 부르는 부드러운 반말, 긴장하면 유창함이 끊김. 무대 어휘. |
| P10 | 강준서 | 초면 존댓말, 친해지면 느긋한 반말. 밥·휴식 챙김, 남 결정을 대신하려 함. |
| P11 | 윤해린 | 빠르고 짧은 반말, 실패 인정 땐 느려짐. 기계·수리·검증. |
| P12 | 오수아 | 밝은 존댓말, 친밀하면 부드러운 반말. 눈치 빠름, 양쪽에 다른 확신. |
| P13 | 정세나 | 직접적 반말·짧은 감탄, 사과는 이름부터. 정의감, 성급한 결론. |
| P14 | 차은결 | 낮고 고른 존댓말, 질문 뒤 기다림. 말장난. 시신의 고요함에 대한 기이한 감상. 도윤 집착. |
| P15 | 남가온 | 짧은 존댓말, 구체적 확인 질문, 침묵. 기자. 공개가 정의라 믿음. |
| P16 | 신채령 | 느긋한 반말, 짧은 병렬 문장, 건조한 비꼼. 경계, 취향 평가. |
| P17 | 송예담 | 높낮이 큰 반말, 놀이·퀴즈·초대. 진심이면 운율이 사라짐. |
| P18 | 임민서 | 짧은 존댓말, 친해지면 짧은 반말. '모름'과 '못 봄' 구분. 과묵. |
| NPC00 | 유스티 | 격식 있는 절차 언어. 감정은 문장의 '멈춤'으로만. 힌트·정답 암시 절대 금지. |

비밀(03 부록 C.1)은 **직접 폭로하지 않는다.** secret_hint는 모호하게, secret_share는 신뢰가 매우 높을 때 "일부"만. 차도윤은 연쇄 살인 과거를 고백하지 않는다(대신 통제·복원에 관한 다른 진심). confess(범행 자백)는 재판에서 범인으로 확정된 경우에만 쓰이며 **계약 소원과 연결된 동기**를 자기 말로 말한다.

## 키 목록
### 일상 (Life)
intro_self, greet, greet_morning, greet_night, greet_close, greet_cold, bye, busy({act}), small_talk(5+), talk_like({topic}), talk_mansion, talk_wish, talk_other_good({t}), talk_other_bad({t}), gossip_saw({t},{place},{time}), ask_player, compliment_react, tease_react, gift_love({item}), gift_like({item}), gift_meh({item}), gift_hate({item}), favor_ask({item}|{place}), favor_thanks, favor_accept, favor_refuse, invite_ask({act},{place}), invite_yes, invite_no, accompany_start, accompany_end, casual_offer, casual_yes, casual_no, secret_hint, secret_share, love_hint, love_confess, love_yes, love_no, love_rejected, argue_open({t}), argue_reply({t}), argue_makeup({t}), argue_stormoff({t}), apology, forgive, not_forgive, warn({t}), fear_general, fear_of({t}), react_rule, meal, doing_act({act}), sleepy, night_walk, grief({victim}), grief_close({victim}), empty_seat({victim}), after_trial_relief, after_trial_guilt, suspicious_of_player, trust_player

### 수사 (Investigation)
scream_discover, discover_shock({victim}), investigate_comment, alibi_where({time},{place}), alibi_with({time},{place},{t}), saw_person({time},{place},{t}), saw_person_unsure({time},{place}), saw_item({t},{item},{place}), heard_sound({time},{place},{sound}), saw_nothing, refuse_answer, suspect({t},{reason}), share_find({item},{place}), keep_find, ask_find, body_exam_comment({victim}), time_pressure

### 재판 (Trial)
trial_first, claim_alibi({time},{place}), claim_saw({time},{place},{t}), claim_heard({time},{place},{sound}), claim_theory({t},{place},{item}), accuse({t}), accuse_player, defend_self, defend_other({t}), agree({t}), object({t}), counter({t}), concede, retract, panic, stay_silent, ask_source({t}), source_direct, source_hearsay({t}), pressure_player, final_defense, confess(3+ 인물 고유), vote_line({t}), verdict_correct_react, verdict_wrong_react, execution_last, escape_line, victim_named({victim})
- break_line (P02 진우, P04 도윤, NPC00 유스티만): BREAK 연출용 짧고 기괴한 대사 3~5개.

### 플레이어 민혁 (P01) — 선택지를 고른 뒤 실제로 말하는 문장
p_greet, p_ask_about, p_ask_like, p_compliment, p_tease, p_gift({item}), p_ask_favor, p_accept_favor, p_refuse_favor, p_invite({act}), p_accompany, p_stop_accompany, p_casual_offer, p_confess_love, p_apologize, p_warn({t}), p_ask_where({time}), p_ask_saw, p_ask_heard, p_ask_suspect, p_ask_share, p_share({item}), p_object({t}), p_agree({t}), p_present({item}), p_accuse({t}), p_ask_source({t}), p_comfort, p_bye, p_mediate({t})

### 유스티 (NPC00) — 공표/절차
y_intro(여러 줄, `|` 연결: 18신의 게임, 소원, 재판, 처형/탈출 규칙), y_rules(규칙 요약 5~8개 문장), y_morning, y_night, y_nightlock({place}), y_body({victim},{place}), y_body_multi({n}), y_invest_start, y_invest_extend, y_invest_end, y_trial_summon, y_trial_open, y_vote_call, y_vote_tie, y_vote_result({t}), y_verdict_correct({t}), y_verdict_wrong({t}), y_draw({t}), y_execution({t}), y_escape({t}), y_loop_reset, y_ability_grant, y_player_dead, y_idle, y_refuse_hint, y_rule_CH01 ~ y_rule_CH23 (각 규칙의 공표문: 이름·효과·기간·예외. 03 부록 E 참고), y_rule_end
### 공용 대체 (ANY) — 인물 전용이 없을 때 쓰는 무난한 문장. 모든 키에 대해 P/C 한 세트씩.
