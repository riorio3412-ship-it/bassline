# BASSLINE 0.23 작업 기록

사람들이 플레이어가 없는 곳에서도 짧게 대화하고 약속을 잡도록 연결했다. 플레이어는 근처에서 실제로 들은 부분만 자막으로 보며, 대화를 확인하려고 메뉴를 열 필요가 없다. 상대에게 직접 이야기할 때는 ‘다른 이야기 → 누구 만나기로 했어?’로 상대가 받은 약속을 물어볼 수 있다.

## 대화와 이동

두 차례 생활 활동을 마친 인물이 선호하는 장소에서 최근 직접 본 가까운 상대에게 제안한다. 상대는 자기 취향·관계 경험·이미 받은 약속을 기준으로 답한다. 다른 인물의 비공개 일정이나 정보를 읽어 제안 상대를 고르지 않는다. 제안과 답변은 실제 시간이 흐르는 발화로 실행하고, 완전히 듣기 전에 약속이 확정되지 않는다.

주변 대화는 플레이어의 이동을 막거나 대화 메뉴를 자동으로 열지 않는다. 도중에 가까이 가면 놓친 앞부분은 생략 표시로 남고 들린 부분만 보여 준다. 직접 말을 걸어 대화를 끊을 수 있으며, 소리가 들리지 않게 되면 인물 간 교환도 중단된다. 글자 크기에 맞춰 자막 공간을 늘리고 조작 안내가 겹치지 않게 이동한다.

약속 장소로 이동할 때 두 사람이 같은 지점 하나를 예약해 한 사람이 못 오는 문제를 수정했다. 같은 장소의 서로 가까운 별도 도착 지점을 찾는다. 실제 만남은 시각·장소·서로의 존재 확인을 통해 판단한다.

## 저장과 호환

NPC 대화도 기존의 개인별 발화 기록과 수신 커서를 사용한다. 발화 중 저장 후 같은 부분부터 이어지며, 중단된 대화는 들은 부분만 남는다. 실제 제안 원문과 맞지 않는 답변 저장을 거절한다. 이전 저장을 불러올 때 과거 NPC 대화를 새로 만들어 넣지 않는다.

## 핵심 시스템 우선 전환

사용자가 핵심 게임 시스템 우선을 요청했다. 새 그래픽/연출 제작과 외관 검토를 뒤로 미루고 재판 표결의 누락된 연결을 수정했다. NPC는 자신의 개인 기록뿐 아니라 실제로 들은 공개 주장·검증 결과를 함께 판단한다. 듣지 못한 검증은 반영하지 않고, 정리해 화면에서 치운 주장도 개인별 수신 이력에는 남는다. 공개 검증을 NPC의 직접 관측으로 바꿔 저장하지 않는다.

동점 재투표에서 후보 명단과 투표자 명단을 혼동해, 후보가 아닌 참가자가 투표하지 못하던 문제도 고쳤다. 재투표의 후보 밖 인물에게 투표한 변조 저장을 거절하며, 이미 기록된 NPC 표를 같은 입력으로 다시 추첨하지 않는다.

## 확인한 검사

- BL22-v23-domain1.xml: 약속 및 기존 지식/사건 규칙 27/27.
- BL22-v23-meetings1.xml: 자율 약속 장면 6/6. 아래 64개에 포함하므로 합산하지 않는다.
- BL22-v23-final1.xml: 대화·대여·카드·자율 약속·구버전 저장 등 PlayMode 64/64. 이후 자막 글꼴/크기 보완 및 핵심 표결 수정이 추가됐다.
- BL22-v23-core-domain2.xml: 공개 근거·개인별 수신·재투표·표결 저장 검증과 기존 재판/정산/사건 예산/장 규칙 검사 55/55.
- BL22-v23-core-play1.xml: 실제 런타임 표결 어댑터 2개와 기존 재판 UI/시험 사건 6개, 총 8/8. 이후 재투표 저장 후보 검증을 강화했고 위 55개에서 재검증했다.

검사 묶음에는 겹치는 항목이 있으므로 수치를 단순 합산하지 않는다. 재판 어댑터 검사는 명시적 시험 재판을 주입하며, 일반 생활에서 본편 사건이 발생해 재판에 도착한 검증이 아니다. 0.23 Windows 빌드·네이티브 화면 검토·배포는 아직 수행하지 않았다. 마지막 검증된 배포본은 0.22다.

## 핵심 조사와 다음 챕터 진행 (023B)

작업실의 고정 촬영 장치와 공용 열람 단말을 본편 장면에 추가했다. 장치는 등록된 방, 거리, 시야각, 실제 가림에 한정해 인물과 물건을 관측한다. 얼굴을 확인할 수 없는 경우 신원을 붙이지 않는다. 소지 기록은 `HeldObject`로 저장하여 소지를 사용으로 단정하지 않는 기존 추론 규칙에 연결했다. 접촉과 쓰러짐 사이에 한 틱이라도 시야/식별이 끊기거나 기록 공백이 생기면 연속 관측 근거가 되지 않는다. 현재 접촉·결과 연속 관측 검사는 명시적 시험 사건/합성 카메라 입력을 사용하며 일반 살인 문법을 새로 열지는 않았다.

촬영 기록은 매체 상태에만 보관하다가 공용 단말을 가까이에서 120틱 조사했을 때 개인 수첩으로 들어간다. 다른 인물에게 자동 전파하지 않는다. 여러 장면과 사본이 같은 매체에서 나온 경우 독립 출처 하나로 유지한다. 재열람은 같은 증거를 중복 추가하지 않는다. 표시 형태는 시각과 관측 내용을 적은 텍스트 증거이며, 영상 파일 저장이나 영상 재생 화면은 미구현이다.

저장은 별도 2048 존재 비트로 구분한다. 구버전 저장은 불러온 시각부터만 기록을 시작하며 과거 기록을 새로 만들지 않는다. 새 회차에는 촬영 기록·개인 지식·진행 중 주변 대화를 초기화한다. 같은 회차의 다음 챕터는 촬영 기록을 보존한다. 재판 후 생존 NPC가 무기한 `WaitForCourt` 상태에 남는 문제를 수정하여 기존 위치에서 생활 계획을 다시 시작하게 했다. 전환 시 대화/카드 존재 비트도 보존한다.

- `BL22-v23-recording-play4.xml`: 촬영/실제 가림/수동 열람/일시정지·중간 저장/구버전 저장/새 회차 초기화/다음 챕터 생활 재개/기존 재판 표결 어댑터 8/8. 판결 전후 연결 검사는 기존 완료 fixture 또는 도메인 판결 프로토콜을 사용하는 경계 검사이며 일반 사건 발생의 완주 검사가 아니다.
- `BL22-v23-recording-domain3.xml`: 기록 및 기존 사건/표결/정산 33/33. 소지 영상은 실제 사용을 입증하지 못한다는 기존 추론 규칙 연결까지 포함한다. 위 PlayMode 이후 `HeldObject` 명칭과 시험 사건의 연속 관측 값 일치를 수정했고 이 검사에서 검증했다.

일반 플레이의 생산용 사건 발생 파이프라인은 아직 미연결이다. 새 Windows 배포본은 만들지 않았다.

## 남은 전체 범위

이번 자율 제안은 IG02 전체 완료가 아니다. NPC가 사적으로 초대장을 고치는 사건, 독립된 참가자의 원문·장소 목격, 미수신으로 생긴 오해와 정정·재협상, 조건이 다른 세 사건 조합은 여전히 남는다. 추가 영상 참고는 Reference_Video_20260926.md에 기록했으며, 전체 모델·스탠딩·표정·연출·배경·나머지 시스템과 콘텐츠 목표를 축소하지 않는다.

## 핵심 구현 묶음 / 검수는 통합 단계로 이동

사용자가 기능을 먼저 모두 만들고 마지막에 한 번에 검수하라고 지시했다. 이미 진행 중이던 신고 검사 이후 새 검사 실행은 중단했다. 이후 코드는 미검수이며 배포본을 갱신하지 않았다.

추가한 기능: 개인 관측에 기반한 NPC 현장 확인·유스티 탐색·신고·통행 양보, 신고자의 실제 관측 위치를 사용하는 현장 이동, 심문 질문/원진술/정정 이력과 실제 수신 후 답변, NPC의 실제 발화 뒤 근거 제출 및 개인별 검토 수신. 새 심문은 숨은 사건 정답을 읽지 않으며 같은 원자료를 반복해도 새 독립 출처를 생성하지 않는다. 원진술을 지우지 않고 정정을 들은 인물에게만 표결 입력의 정정 상태를 적용한다.

심문은 기본 사실 확인·범위 축소·정정 연결이며 거부/기만 목적의 전체 인물 AI는 남아 있다. NPC 반론은 기존 단일 전제 검토기와 연결했으며 TM04 연속 전제 그래프는 남아 있다. 일반 생산용 사건 계획·실행과 나머지 재판 모드도 여전히 미완료다.

## 최종 재구성·방어 기능 묶음 — 소스 구현, 통합 검수 대기

개인 수첩 자료를 사건 A/B에 묶어 확인 사실·원인 행동·결과 관측·발견·이후 행동으로 정리하고, 가정 표시와 책임 대상 선택을 할 수 있도록 연결했다. 원자료 삭제 없이 편집하며, 소지→사용→결과와 관측 시각→실제 발생 시각을 자동 승격하지 않는다. 설명을 실제 발화로 발표한 뒤, 지목된 NPC는 자기 자료나 발표에 드러난 빈 부분으로 답한다. 같은 설명에 같은 반론을 반복 재생하지 않고, 이전에 들은 반론을 남겨 둔다.

자료가 있는 반론은 발화 완료 후 기존 추론 규칙에 제출되고, 해당 설명과 반론을 들은 사람만 검토 결과를 받는다. 설명 수정/논쟁 복귀/불확실성을 인정한 투표와 정리 발표 생략을 연결했다. 원래 대기 중이던 일반 발언은 보류 목록에 저장하고 논쟁 복귀 때 재개한다. 복수 자료를 발언에 연결해도 처음 자료 하나만 전달하던 경로를 모두 전달하도록 수정했다.

재구성 항목·발표 버전·반론 근거·발화 단계·불완전 진행 동의·화면 선택과 보류 발언을 저장 상태에 연결했다. 저장된 표시 문구가 원자료의 신원/시각/명제로 바뀌어 복원되지 않도록 제한한다.

요청에 따라 새 컴파일·테스트·빌드·화면 검수는 실행하지 않았다. TM07의 물리적 가능성 전체 판정, TM08의 모든 방어 전술과 완성도, 생산용 자율 사건 발생 등은 여전히 남아 있다. 이번 변경은 기능 연결 소스이며 전체 구현 완료 또는 검수 통과를 뜻하지 않는다. 배포 실행 파일은 v0.22 그대로다.

## 사건 계획·개입·실행 전 경로 판정 — 소스 구현, 검수 대기

IncidentPlanner는 개인 지식과 작성된 현재 목적/금기/대안만 입력받아 철회·도움 요청·공개·협상·실행을 비교한다. 동점에서만 저장 난수를 소비하며, 같은 근원의 반복 관측이나 평온한 시간 경과를 새 동기로 취급하지 않는다. 목표/판단 근거/대안/대상 후보/자원/중단 조건/개입 지점/버전과 선택 전후 난수를 보존한다. **현재 목적/중대 갈등을 본편 생활에서 만드는 입력 및 생산용 실행 템플릿 등록은 아직 연결되지 않았다. 이 클래스를 추가한 것만으로 일반 자율 살인 발생기를 완성한 것은 아니다.**

현재 MansionIncident 실행에 개인 계획 상태와 재검사 기록을 연결했다. X31은 ExplicitTestReplay로 명시해 자율 판단으로 오인하지 않는다. 이동 경로 중단, 도구 소지권 상실, 실제 대화 개입, 대상을 향한 동행 대화, 실제 수신한 소집을 실행 중단에 반영한다. 철회 때 자신의 이동/문 대기/결과 예약을 해제하고, 대화 종료 뒤 취소된 이전 이동이 재개되지 않도록 복귀 상태를 수정한다. 다른 피해자에게 자동 이전하지 않는다. 계획과 재검사 근거는 저장 검증에 포함한다.

IncidentAdmission은 원인 실행 전에 등록/제작 검토 여부, 실제 기회, 지연 결과 상한, 책임 명제의 두 독립 경로를 확인한다. 두 경로의 원본과 접근 의존이 겹치면 거절하며, 적어도 하나는 보존 가능하고 특정 인물/호감/희귀 도구에 의존하지 않아야 한다. 사전 판정과 선택된 경로를 저장한다. 원인 직전 실행 경계에 연결했으나 **일반 템플릿은 아직 미등록이며 X31 시험은 기존 명시적 재생 경로다. 현재 카메라만으로 공정성 통과를 선언하지 않는다.**

사용자 지시에 따라 새 컴파일·테스트·빌드·화면 검수는 수행하지 않았다. 실제 동기 콘텐츠·생산용 물리 동작/결과·사전 경로 자원 바인딩·위험 해소/구조·최종 자산 검토 등은 남아 있다. 기존 배포 v0.22는 변경하지 않았다.

## 실제 생활 갈등 → 개인 판단 → 이동/설명 연결 — 미검수 구현

기존의 개인 지식 기반 IncidentPlanner를 본편 생활에서 호출하도록 첫 동기 입력을 연결했다. 실제로 수신한 펜 관련 부당한 단정과 그에 따른 본인의 관계 경험만 사용한다. 이 상황의 목적은 자신의 설명을 바로잡는 것이므로 철회·직접 질문·자료 설명·도움 요청을 비교한다. 이를 위해 의도나 살인 성향으로 자동 확대하지 않는다. 현재 살아 있는 심각한 동기 콘텐츠와 생산용 살인 계획 선택은 별도 남은 작업이다.

선택한 행동은 마지막으로 직접 본 상대 위치로 실제 이동하고, 근거리에서 가청 조건이 맞을 때 발화한다. 들은 범위만 기존 발화/전언 원장에 남으며 자료 설명은 실제로 자기에게 있는 자료를 인용한다. 도움 요청을 받은 NPC는 자기 자료가 있으면 범위 안에서 설명하고, 없으면 모른다고 답한다. 설명을 들었다는 이유로 진실 인정·화해·신뢰 회복을 자동 처리하지 않는다. 서로의 숨은 정보나 실제 위치를 참조해 목적지를 정하지 않는다.

플레이어 대화, 현장 대응, 수락한 약속, 소집이 우선한다. 수신한 정정으로 남은 행동을 종료하며, 마지막 관측 장소에 없거나 통로가 막히거나 대화가 반복 중단되면 물러선다. 같은 갈등을 처리했다고 다시 새 동기를 생성하지 않는다. 난수/선택 근거/이동 목표/발화·답변 커서/종료 이유를 저장하며, 챕터 경계에서 과거 갈등 처리를 유지하고 루프 경계에서 지운다. 주변 대화 자막에는 플레이어가 실제 들은 부분만 표시한다.

신규: Core/ResidentIntentState.cs, NPC/ResidentConflictPlanner.cs, Bootstrap/MansionRuntime.ResidentIntents.cs. 생활 루프·일정·실제 대화·현장 대응·재판 입장·저장/회차에 연결했다. 사용자 지시에 따라 아직 컴파일·테스트·실행/화면 검수하지 않았다. 일반 자율 살인 발생기 전체, 생산용 행동/결과/위험 해소, 승인된 두 경로 자원 바인딩은 완료되지 않았다.

## 핵심 기능 우선 추가 — 지연 위험과 구조 행동 (소스 구현, 미검수)
- 사용자 요청대로 이번 작업에서 컴파일·테스트·빌드·에디터 실행·시각 검수를 수행하지 않았다. 통합 검수는 기능 구현 이후에 진행한다.
- MansionIncident.Rescue: 기존 명시적 X31 시험 사건에 등록된 120틱 구조 행동을 추가했다. 실제 2m 이내 도달·시야, 빈손, 동일 자리, 연속 세계 틱과 행동 유지가 필요하다. 중단하면 진행량을 버리고 다시 시작하며, 결과 마감 틱에는 구조보다 결과가 우선한다. 사망을 되돌리지 않는다.
- 성공 시 RiskResolved와 수행자·시각·세계 이벤트를 저장하고 공동 사망 예약을 반환한다. 원인·접촉·표식·기존 목격 기록은 유지된다. 실제 관측한 사람에게만 위급 상태/이후 상태 변화가 수신된다.
- 플레이어의 인물 상호작용에 돕기와 진행 안내를 연결했다. 이동·다른 상호작용·조사는 중단하며 다른 대화/전달/쓰기/시간 보내기와 겹치지 않도록 연결했다.
- 주변 NPC는 자신이 직접 본 위급 상태 기록과 현재 도달·시야를 바탕으로 구조를 시작한다. 아직 먼 곳으로 구조하러 탐색하거나 지원자를 부르는 행동은 없다.
- 현 등록 접촉 사건의 결과 대기 동안 PhysicalBand=Critical, 행동/이동 불가; 구조 뒤 Injured와 행동 재개; 사망은 Dead. 새 저장 필드 중복 없이 기존 결과 장부에서 파생한다. 다른 원인 문법의 잠복 위험, 전체 신체 상태 전이/회복 시스템은 별도 구현 대상이다.
- 구조 중 저장·불러오기 검증 조건을 소스에 추가했다. 중복 구조자와 유실된 구조 작업을 거부한다. 구조된 원인만 있는 사건은 다른 사망 사건의 재판 개시를 막지 않는다.
- 외관은 기존 프록시 기울기만 사용한 임시 표시다. 구조 팔 동작/접촉 궤적, 실제 쓰러짐 물리와 정식 그래픽 완료를 주장하지 않는다.
- 일반 플레이의 자율 살인 생성기나 정식 제작 사건을 활성화한 변경이 아니다. X31 ExplicitTestSession 제한 유지. 배포 v0.22와 작성 장면 023B는 변경하지 않았다.

## 핵심 기능 우선 추가 — TM04 전제 의존 그래프 (소스 구현, 미검수)
사용자 요청대로 기능 작성만 진행했으며 컴파일, 테스트, 빌드, 에디터 실행, 화면 검수를 하지 않았다.
- 주장을 결론/전제 노드로 연결하는 ArgumentChain을 재판에 추가. 실제로 들은 원진술의 Claim/Span만 선택 가능하며 순환·자기 참조를 거부한다. 결론 하나와 전제 최대 다섯 개의 일반 유향 의존 그래프이며, 단순 순서 맞추기가 아니다.
- 기존 재판 화면 28을 ‘설명 연결’ 조작으로 연결. 전제 연결/제거, 노드 선택, 설명하기, 부분 검토, 수정본 작성, 자기 설명 철회, 다른 설명 보기 지원. 기존 화면/버튼 사용; 장면이나 그래픽을 다시 생성하지 않았다.
- 연결은 플레이어의 해석으로 명시한다. 실제 시간 경과 발언으로 공개되고, 설명/철회를 들은 인물만 해당 공개 상태를 읽는다. 인용한 원진술을 못 들은 경우 수신한 설명만으로 원자료가 소급 수신되지 않는다.
- 각 노드의 직접 검토 결과와 의존 상태를 구분. 반증/범위 제한/진술 정정이 있으면 의존 노드만 NeedsReview. 직접 반박되지 않은 결론을 거짓으로 확정하지 않으며, 전제들만 확인됐다는 이유로 결론을 자동 입증하지 않는다. 관련 없는 노드의 Proof와 기존 진술은 유지.
- 설명 공개 후 해당 선택 노드에서 한 번 제시한 자료를 같은 LogicResolver로 각 의무에 적용한다. 여러 의무를 함께 충족 가능하며 정해진 순서를 요구하지 않는다. 동일 주장/범위/행동/규칙/자료 조합은 순서가 달라도 기존 제출 결과를 재사용한다. 새 검토 성취를 반복 생성하지 않는다.
- 실제 NPC 반론 발언에 대상 Claim/Span 연결을 저장한다. 공개한 설명에 필요한 전제가 둘 이상이고 수신한 관련 NPC 반론이 있을 때 ArgumentChain 양상으로 표시. 일반 단독 주장에는 체인을 강제하지 않는다.
- 재판 저장에 그래프·수정본 계보·현재 선택·발언 연결을 저장. 노드의 실제 수신 시각과 의존 그래프 및 발언 체인을 불러오기 조건으로 구현. 논점 이동/화면 이동 후 기존 설명과 해결된 전제를 다시 열 수 있다.
- 아직 모든 NPC가 자신의 복합 논증을 자율 작성하는 기능, 의미적 의존 관계 자체의 완전한 검증, TM05 경쟁 가설/TM06 실제 동의 협업 전체를 끝냈다는 의미는 아니다. 일반 제작 사건/개인 목표와의 전체 연결도 남아 있다. 기존 v0.22 배포/작성 장면023B 유지.

## 핵심 기능 우선 추가 — TM05 공개 설명 비교와 조건부 입장 (소스 구현, 미검수)
- 근거: 통합기획서 27쪽 TM05. 사용자 지시대로 컴파일·테스트·빌드·에디터 실행·시각 검수는 이번에 하지 않았다. 기능 구현 후 통합 검수 대상이다.
- 실제로 발언 완료된 ArgumentChain을 공개 가설로 투영한다. TheoryID=원설명 ID, 수정본 Revision/부모 연결, 필수 전제, 설명 범위는 기존 원진술·발언·그래프에서 나온다. 숨은 실제 범인이나 사건 정답을 이용해 반대 설명을 만들어 넣지 않는다.
- Active/Strengthened/Weakened/Contradicted/Collapsed/Unresolved 구분. 필수 전제의 실제 Contradict만 Collapsed, 범위 제한·정정·의존 재검토는 Weakened, 참고로 덧붙인 진술 일부의 반증은 Contradicted. 확인된 전제가 있어도 미확인 전제를 별도로 표시한다. 경쟁 설명의 상태를 올려 주는 처리는 없다.
- 비교 범위는 현재 공개 재판 안의 명제와 시간 범위다. 현재 Clash 자동 판별은 같은 대상/정확히 같은 시간 범위의 AtPlace·DoorState라는 등록된 배타 명제에 한정했다. 다른 술어·비배타 경로·더 넓은 경쟁 인과 설명은 비교 가능하지만 충돌로 자동 확정하지 않는다. 이 규칙만으로 전체 TM05 의미 추론을 끝냈다고 주장하지 않는다.
- 기존 화면26을 두 설명 비교에 연결. 화면28 공개 설명에서 진입, 좌우 선택·세 번째 설명 전환·전제 선택·기존 FOCUS 검토·조건부 채택·판단 유보·자기 지지 철회·참고 진술 추가·자기 설명 수정본 작성 가능. 기존 UI 구조와 버튼을 사용했고 장면은 재생성하지 않았다.
- 조건부 지지/유보/철회/참고 진술은 실제 시간 경과 발언으로 전달된다. 각 청자가 들은 마지막 입장만 표시하고 미수신/미발화 부분을 보여 주지 않는다. 설명을 듣는 것과 인용된 원진술을 받는 것도 분리한다.
- NPC는 자신이 실제 수신한 공개 검토 결과를 바탕으로 전제가 모두 지지된 설명에 조건부 지지, 반증·범위 제한된 설명에는 판단 유보 발언을 요청한다. 표심/호감/진범 ID는 읽지 않는다. NPC가 스스로 모든 복합 가설을 작성하는 기능까지 완료한 것은 아니다.
- 공개 지지자 수는 정보 표시일 뿐 LogicResolver와 가설 상태에 입력되지 않는다. 같은 입장을 의미 없이 반복 요청하면 새 발언을 추가하지 않는다. 원진술/기존 Proof를 지우지 않고 현재 수신한 결과로 재평가한다.
- 저장에는 입장 발언 요청·실제 발언 연결·참고 원진술·시각·좌우 설명·선택한 전제를 추가. 가설 상태는 저장된 그래프/수신 원진술/검토 영수증에서 청자별로 재계산해 비공개 정보 유출을 막는다. 기존 저장의 새 배열은 비어 있는 상태로 복원.
- 전체 제작 사건/자율 목표·범행 실행, TM06 동의 협업, 전체 물리/그래픽/3D 자산 등 남은 목표는 유지한다. 배포 v0.22 및 작성 장면023B unchanged. FullScope 완료 표시 없음.


## 2026-09-26 — TM06 공동 자료 설명: 기능 소스 추가, 통합 검수 대기
- 사용자 지시: 기능을 먼저 모두 만들고 검수를 한 번에 진행. 이번 작업에서 컴파일, 테스트 실행, 에디터 실행, 빌드, 시각 검수는 수행하지 않았다. 아래는 소스 구현 범위이며 실행 확인 결과가 아니다.
- 공유받은 자료만 참여 요청 후보로 만든다. 플레이어 B의 실제 전달본(부모·출처·근원 연결 필요)을 사용하고 NPC 비공개 기억을 후보 목록에 추가하지 않는다. 한 구성에 최대 세 자료, 동일 근원 중복 배제, 요청 전에 순서와 공개 범위를 선택한다.
- 요청 → 실제 수신 → NPC 자신의 B로 동의 판단 → 답변 실제 수신 → 동의한 자료만 순서대로 설명. 요청에는 자료 원문을 미리 공개하지 않는다. 내용과 한계 공개 / 시간 범위와 일반적인 한계만 공개를 구분하며, 요청 이후 공개 범위를 바꾸려면 중단 후 다시 동의를 구한다.
- 현재 NPC 참여 정책: 자신의 공유 원본이 있으면 동의, 원본이 없으면 거절, 전체 내용 요청인데 전언만 보유한 경우 판단 유보. 범위만 설명하도록 다시 요청할 수 있다. 관계 점수로 자료나 동의를 생성하지 않는다. 개별 성격·위험에 따른 더 복잡한 협상 정책은 아직 미완이다.
- 전체 설명은 동일 원본과 근원으로 기존 Knowledge.Deliver 경로를 사용한다. 등록 술어에는 원본과 정확히 같은 범위의 검토 가능한 Claim을 만든다. 기존 LogicResolver/출처 조건을 적용하며 협업 인원에 따른 신뢰 보너스는 없다.
- 범위만 공개한 발언, 동의 대화, 중단된 발언 조각은 실제 말한 문자열에 대한 SaidStatement 영수증이다. 원자료 전체나 미발화 Claim을 전달하지 않는다. 발언 끝까지 실제로 들은 청자에게만 해당 영수증이 생긴다.
- 전체 중단은 해당 구성의 대기 발언만 취소하고 현재 말한 접두부를 별도 발언 조각으로 보존한다. 원자료와 이미 완결된 설명은 유지한다. 참여자 단독 철회용 실제 발언 경로도 추가했으며 다른 참여자의 동의는 없애지 않는다. 현재 런타임에서 단독 철회 요청은 원본 재확인 실패에 연결했다.
- 화면27의 기존 교차 논쟁 자리 표시자를 ‘함께 설명하기’로 연결했다. 기존 화면23에서 진입하며 이전/다음 자료, 목록 추가·제거, 먼저 설명할 자료, 공개 범위, 동의 요청, 설명 시작, 중단이 동작하도록 작성했다. 기존 버튼/스크롤 구조 사용. 화면/씬/아트 재생성 없음.
- 진행 상태는 TrialSnapshot의 JointArguments에 저장: 구성 ID, 원본 전달 영수증, 근원, 참여자, 순서, 공개 범위, 요청·답·설명·철회 ID, 완료 시각, 중단 이유. 재판의 음성 커서/실제 청자/발언 기록도 기존 저장 경로에 포함된다. 새 배열 없는 구버전 저장은 빈 목록으로 복원한다. 공유 경로·공개 범위에 대한 저장 검증 코드도 추가했지만 실행하지 않았다.
- 공동 설명이 진행/대기 중일 때 최종 발표·투표로 건너뛰려면 먼저 마치거나 중단한다. 공동 제시는 선택 기능이며 거절이 혼자 제시하는 기존 경로를 막지 않는다. 새 근거의 검토·반박은 기존 포커스/증거 제시 경로를 사용한다.
- 제한: 자유로운 복합 인과 연결 설명 작성, NPC의 본격적인 협상·협업 제안, 전체 TM06 콘텐츠/평가, 생산용 중대 목표·사건 실행·물리·3D/그래픽은 남아 있다. 전체 게임 목표/FullScope는 완료하지 않았다. 배포 v0.22와 작성 장면023B 그대로.


## 2026-09-26 — 인물 계획에서 일반 사건 실행으로 연결하는 소스 경로
- 직전 TM06 작업은 소스 변경이 있는 진척이었다. 이번에도 사용자 지시에 따라 기능 작성만 했고 컴파일·테스트·빌드·에디터·시각 검수를 실행하지 않았다.
- 근거: 통합기획서 §7.11, §8.1, §8.4, §8.7. 시험의 고정 배역을 본편 범인으로 승격하지 않고, 인물 자신의 B에 근거한 Deliberated/Execute 계획을 별도 입력으로 받는 ConfigurePlanned 경로를 추가했다.
- IncidentExecutionDefinition: 문법 IG01–12, 등록된 ContactOutcome 동작, 개정·출처·공개 규칙·대사·관측 문장·흔적 값, 동기/금기, 제작 자산·최소 세 변형, 접촉/의도/결과/구조 시간. 이는 12개 문법 콘텐츠가 완성됐다는 뜻이 아니다. 현재 실행기는 근접 접촉→위급 상태→유예 결과 형태만 지원한다.
- IncidentExecutionBinding: 원래 계획, 직접 읽은 공개 규칙의 영수증, 동기 태그, 금기, 맵 버전, 도구/목표 접근 노드, 실제 바인딩 시각을 저장한다. 규칙을 읽지 않은 인물, 다른 회차의 자료, 타인의 계획, 다른 목표·자원, 금기 또는 작성만 된 미검수 정의는 등록하지 않는다. Reviewed 상태는 이번에 임의 부여하지 않았다.
- 일반 실행은 인물의 선택된 계획을 그대로 복사하여 기존 이동·문·실제 소지·접촉·피해 예산·결과·발견·신고·재판 경로로 넘긴다. 실행 도중 잃은 목표나 도구 대신 다른 대상을 선택하지 않는다. 계획 근거/레이아웃/기회가 없어지면 취소한다. X31 명시적 시험은 기존 별도 경로를 유지한다.
- 공개 규칙을 실제 조사할 수 있는 MansionIncidentRulePlate 컴포넌트와 기존 조사 완료 경로를 연결했다. 실제 등록된 조사 대상, 근접·시야 조건을 거쳐 읽은 내용만 B에 들어간다. 보이지 않는 전역 규칙 주입은 없다. 이 컴포넌트를 사용하는 본편 소품/방 배치는 아직 제작하지 않았다.
- 접촉 누적 도중 실제 접촉·소지·현재 관측 조건이 끊기면 누적과 피해 예약을 해제한다. 월드 틱이 건너뛰면 연속 접촉/의도 발언을 이어 붙이지 않는다. 아직 원인이 성립하지 않은 계획이 다른 실제 계획의 피해 예산을 계속 점유하지 않도록 했다.
- 흔적/접촉/연속 관측 문장은 정의에서 가져오며 시험 코드명 P31/X31을 새 본편 문장에 강제로 넣지 않는다. 결과 인과 값에 실제 템플릿 ID를 사용한다. 구조의 완료 시간과 진행 표시도 개별 등록 정의를 따른다.
- 저장 복원 시 등록 정의·선택 계획·공개 규칙·맵/노드·결과 시간을 연결하는 무결성 코드를 추가했다. 구 시험 저장은 Execution을 null로 유지한다. 새 코드 검수는 미실행.
- 남은 연결: 실제 중대 목표/갈등 36종 콘텐츠, 그 목표의 실행 후보 선택, 등록 소품과 시야·접촉 동작, 카메라/목격/결과 기록의 독립 경로를 실제 맵에서 인증하는 어댑터, 제작 자산과 콘텐츠 검수. BindIncidentPlan은 작성됐지만 현재 생활 오해 계획이 이를 호출하도록 살인 의도를 임의 추가하지 않았다. 일반 플레이 살인 자동 생성은 아직 켜지지 않았다.
- 배포 v0.22, 작성 장면023B unchanged. FullScope/전체 목표 미완료. 다음 작업은 선언을 더 늘리기보다 실제 위험 고지 동작 및 보존되는 독립 기록 경로를 구현해 본편 후보가 실행될 조건을 채운다.


## 2026-09-26 — 실제 경고·접촉 유지와 장치 상태 기록
- 기능 우선/마지막 통합 검수 지시 유지. 소스 변경만 수행했으며 컴파일·테스트·빌드·에디터·시각 검수는 하지 않았다.
- 일반 ContactOutcome 실행에 RiskNoticeTicks를 추가했다. 실제 접촉 중 현장 WarningLabel이 있고 행위자 시야에 보여야 경고 시각이 시작된다. 취소 가능한 경고 시간이 지난 뒤에도 접촉을 유지해야 IncidentRiskContinued를 남기고 기존 접촉 누적에 들어간다. 기억 속 실행 계획만으로 원인을 확정하지 않는다.
- 접촉/경고 표시/연속 틱 조건이 사라지거나 개입으로 취소되면 해당 시도의 경고 상태와 접촉 누적을 초기화하고 IncidentContactAbandoned를 남긴다. 다음 시도는 새로운 경고부터 시작한다. 시험 X31의 기존 입력 재생은 이 새 본편 경로로 임의 바꾸지 않았다.
- MansionIncidentDeviceSignal은 실제 조사 대상 소품의 TextMesh 경고 표시를 사용한다. 글꼴과 라벨, 시야가 없는 장치로는 새 원인 실행을 진행하지 않는다. 저장 복귀 시 표시는 사건 상태에서 복원한다. 아직 본편 소품에 배치하거나 최종 한글 가독성을 검수하지 않았다.
- MansionActionJournal: 실제 설치된 단말과 연결된 하나의 DeviceId에 대해 현재 틱의 경고 표시, 접촉 유지, 접촉 중단, 원인 확정, 결과/구조만 저장한다. ActorId·개인 계획·동기·다른 장치 사건을 공개 기록에 넣지 않는다. 같은 작동 번호로 순서를 묶는다.
- 전원 꺼짐/틱 누락은 진행 중 기록 연결을 끊는다. 설치 전·불능 중 사건을 사후 복원하지 않으며 원인 없이 결과 연결을 만들어 주지 않는다. 이미 쓴 기록은 남고 새 시도는 별도 작동 번호를 사용한다.
- 기존 가까이서 조사(120틱) 기능으로 단말 안내와 실제 기록을 수첩에 옮긴다. 기록은 읽기 전 NPC/플레이어 B에 들어가지 않는다. 같은 단말의 사본·여러 줄은 같은 원본 근원을 유지한다. 단말은 행위자 신원을 단독으로 알려 주지 않는다.
- 세션에 ActionJournal과 선택 객체 비트16384 추가. 최초 설치/옛 저장 업그레이드는 현재 시각부터 기록, 같은 루프의 다음 챕터에서는 기록 보존, 새 루프에서는 새 매체로 초기화한다. 상태 전이·시간·작동 번호·대기 연결 저장 무결성 코드 추가. 모두 아직 미검수.
- 한계: 이 기록만으로 책임/고의가 증명되지는 않는다. 얼굴/동작의 독립 관측, 장치 표시의 실제 작동 번호·대상 연결, 공개 규칙을 함께 검토하는 LR07 확장과 맵의 두 독립 경로 어댑터는 남아 있다. 기록 정의·경고 소품은 코드 컴포넌트만 추가했고 현재 장면에 설치하거나 Reviewed 콘텐츠로 승격하지 않았다. 일반 사건 생성/중대 목표 연결은 여전히 미완이다.
- 전체 기획/그래픽/3D/최종 검수 목표는 active. 배포 v0.22·작성 장면023B 변경 없음.


## 2026-09-26 — 읽힌 작동 표시와 장치 기록의 인과 연결
- 직전 경고/장치 기록 작업은 진척. 이번 작업도 기능 소스 작성만 수행했으며 컴파일·테스트·빌드·에디터·시각 검수는 하지 않았다.
- 경고 시도의 공개 작동 번호를 IncidentSnapshot에 추가했다. 번호는 회차와 실제 경고 이벤트 순번으로 생성하며, 범인/목표 이름이나 사적 계획 ID를 인코딩하지 않는다. 접촉 중단 후 재시도에는 새 번호가 생긴다. 장치 로그도 이 번호를 사용한다.
- 현장 신호 표시가 작동 번호·등록 대상 이름·안내 개정을 실제 텍스트로 보여 준다. 원인 확정 후에는 ‘작동 중’으로 바뀌며 원인 이전의 접촉 취소 문구를 계속 보여 주지 않는다. 실제 장면 소품 배치/최종 한글 표시 검수는 아직 남아 있다.
- 카메라는 얼굴·대상·도구가 보이고 표시 라벨도 가까운 범위에서 가리지 않고 읽을 수 있는 경우에만 작동 번호/대상/규칙 개정을 얻는다. 해당 틱의 경고 후 접촉 유지와 원인 동작을 별도 UsedObject 영상 관측으로 기록한다. 표시를 읽지 못한 영상/일반 목격에는 엔진이 아는 번호를 넣지 않는다.
- KnownRecord에 관측된 ActivationId/DeviceId/OutcomeTarget/ActionDefinition/ActionRevision/CausalStage를 추가했다. 복사·전언은 그대로 보존하되 Direct를 새로 올리지 않는다. 저장의 부모 전언과 해당 바인딩이 달라지는 경우를 거절하도록 작성했다. 예전 자료의 빈 바인딩은 미관측으로 남긴다.
- 새 번호를 직접 읽은 연속 촬영의 인과 명제는 작동 번호와 대상까지 포함한다. 단순히 접촉에서 쓰러짐까지 본 일반 목격/번호 없는 영상은 종전의 관측 범위만 유지한다.
- LR07에 선택한 현재 B 자료만 사용하는 CausalEvidence 결합 경로 추가. 정확한 원인·결과 시간, 동일한 작동 번호/도구/대상/규칙 개정, 얼굴이 확인된 원인 및 경고 후 유지 영상, 장치의 경고/유지/원인/결과 로그, 직접 읽은 공개 규칙이 모두 있어야 해당 하나의 명제 범위를 지지한다. 사적 동기나 다른 시각·다른 대상까지 확정하지 않는다.
- 누락된 근거는 ‘원인 기록’, ‘같은 대상의 결과’, ‘경고 후 유지 모습’, ‘공개 작동 규칙’ 등 구체적인 짧은 문장으로 안내한다. 시간만 같은 다른 기록을 묶거나 텍스트를 추측해 인과를 만들지 않는다. 취소/위험 해소 기록이 같은 건에 있으면 자동 확정하지 않는다.
- 영상/장치 열람 문장에 실제 읽힌 대상과 개정도 표시한다. 사용자는 기존 자료 선택·LR07 제시 경로를 사용하며 새 화면을 추가하지 않았다.
- 제한: 새로운 증거 결합은 소스 구현이며 실행 확인 전이다. 독립 목격 경로 B의 위험 고지/대상 연결, 실제 맵 두 경로 인증, 중대 목표/생산용 사건 콘텐츠와 소품 배치, 전체 그래픽/3D는 남아 있다. 어떤 정의도 Reviewed로 승격하지 않았고 일반 사건 자동 생성은 아직 미완이다. 배포 v0.22와 장면023B unchanged; 전체 목표 active.

## Continuation — independent observed witness chain and grouped notebook selection; unverified
Implemented source only; user requires batch QA after functionality. No compile/test/build/editor/visual runs. Released v0.22 and scene023B unchanged. No agents or background processes.

New World/Mansion/MansionIncident.Witnesses.cs (+meta): MansionRiskWitness tracks per-observer actual warning receipt, continuation receipt and last tick. New IMansionWitnessDisplay runtime adapter reads only active readable device labels within 2.5m, observer view cone, opacity/font/renderer and sight checks. Contact stage requires actual actor/target/device visibility and identities plus rendered activation/target/definition/revision. Tracks drop when any condition fails; new tracks only before warning interval ends. Records WarningShown, ContactContinued, Cause separately. CaseWitness holds CauseReceiptId from actual observed continuation. Result ACT-target record generated only from that observed cause receipt when observation remained uninterrupted and target is identified at outcome. No hidden activation injection into ordinary sightings. Tick gaps after cause invalidate continuous witnesses. Save deep copies risk tracks; restore checks receipt bindings/predecessors/time/continuity. Legacy snapshots have empty risk tracks.

All new Visual receipts per case/observer now share WITNESS_L<loop>_<case>_<observer> provenance, including ordinary contact/trace/result observations, preventing one witness's multiple stages from looking like independent witnesses. Previous already-saved receipts are not rewritten.

Core/CausalEvidence.TryLinkWitness joins only caller supplied own records: same observer provenance+observed activation binding; earlier warning, continued action, cause, continuous outcome, separately directly read public rule. Direct Visual chain supports within scope; delivered Statement chain with parents returns Conditional, never promoted to direct. LogicResolver LR07 invokes after recorded-media route fails; states concrete missing witness premise. Existing camera+device route unchanged.

Core/CausalEvidence.RelatedRecords groups only own notebook records by exact activation/device/target/definition/revision plus matching public rule. Existing UI25 adds connected-materials selection button (8-button capacity remains); replaces selection only after user click, no submission or conclusion automation, existing comparison choice preserved. This makes causal bundles selectable without cycling/selecting seven separate records. Unity UI/uGUI skills consulted; user's defer-QA overrides their per-edit verify cadence.

Important remaining: actual scene placement, authored production definitions/content, serious-goal live caller and actual independent fair-access admission adapter remain missing. This witness chain is a real source implementation but not full production integration or verified functioning. Do not mark goal complete/Reviewed or start QA next; continue core systems/integration.

## Continuation — authored personal purposes connected to existing resident behavior and incident caller; NO QA
Previous turn was progress (witness source edits). This turn again changes authoritative source. User core-first and implement-all-before-batch-QA remain authoritative. No compile/tests/build/editor/visual runs, no agents/background processes. v0.22 and scene023B unchanged. Full goal ACTIVE, incomplete.

New Core/ResidentPurpose.cs (+meta): explicit authored PurposePremise exact predicate/value with subject Owner/Target/Any, source Target/Any, direct/identity/kind constraints; per-character ResidentPurposeDefinition includes revision/source/motive/title, required own-B premises, known resolutions/taboos, five authored scored responses, bounded opportunity/freshness. Target must be bound through required personally known subject/source; no global selection of culprit. ResidentPurposeBinding retains target/site/map/public-rule/proof/actual reason IDs, validates owner-bound plan and original decision-time premises. No petty misunderstanding is auto-promoted to a lethal goal.
New AuthoringData/MansionResidentPurpose.cs (+meta): component on FixtureActorBody stores authored Goals and applicable PersonalTaboos plus physical site IDs. Not attached to scene yet; no actual serious-goal content authored/approved in this turn.
New NPC/ResidentPurposePlanner.cs (+meta): only own knowledge and received social experiences. Matches actual reasons and resolutions, builds Withdraw/AskForHelp/Disclose/Negotiate/Execute alternatives; requires known position, actual directly held proof for disclosure, personally trusted known helper for help, authored harm permission/taboo/action/rule/observations for Execute. New rule knowledge is a meaningful additional reason; same-root repeated observation/time alone does not reconsider an ended purpose. Nonviolent chosen actions use actual existing conversation flow with authored opening and scoped received text; reply acknowledges receipt without inventing consent. Not full negotiation outcomes/content yet.
New Bootstrap/MansionRuntime.ResidentPurposes.cs (+meta): cache author components at Awake; called from resident intent cadence (30 world seconds), candidate IDs only from own B, compare scored drafts across authored goals/known targets, deterministic saved RNG only ties; commits only selected draft, no random cast reassignment. Actual BindIncidentPlan caller for Execute -> task phase Executing, syncs bound case progression/withdrawal/risk resolution/result. Nonexecute reuses existing actual travel/speech/interruptions. Doesn't auto-place sites or bypass Reviewed/admission.

ResidentIntentState now optional PurposeId/Purpose binding; Copy uses PurposeId to avoid Unity null-inline confusion. Snapshot contains PurposeReadingProgress[] with actual continuous read cursor. Existing save/copy/chapter preservation/loop reset applies through same snapshot; no separate new storage. ValidateResidentIntents branches old loan-specific invariants vs purpose-specific invariants, supports Executing and authored deadline, retains shared speech/location checks. ValidateResidentPurpose ties task/proof/recipient/binding/map/case and known resolution. SourceExperienceId for authored purpose is unique PURPOSE_<planid>.
AdvancePurposeRuleReading: NPC already physically performing Examine at authored plate, reachable/visible, actually spends max(180,text-length*5) continuous ticks reading; interruption/gap/text revision discards unfinished read with no receipt. Completion uses existing ReadIncidentRule -> own document. No telepathy or automatic rule receipt on initialization. Movement to this plate/content routine still needs actual authoring. Reading cursors deep-copy/save; restore shape/time checks. Player inspection path unchanged.
IncidentExecutionBinding embeds optional PurposeId/Purpose; validates exact original goal/motive/rule/map. BindIncidentPlan optional purpose passes original owner plan. Production pre-cause recheck cancels on actually received resolution/taboo, after actual cause cannot undo history. Nonviolent goal ends Resolved only on its own matching known resolution, otherwise conversation completion is merely delivered.

IMPORTANT remaining: no MansionResidentPurpose components/goals or rule/device/journal sources placed into scene; full 36 personal long-term goals and serious motive content remain. Production execution still requires actual reviewed definitions and IMansionIncidentAdmission physical independent routes (not implemented); therefore ordinary game murders are NOT enabled/complete. Independent witness and recorded causal systems are source code, no full scene integration or verified function claim. Advance actual scene/content/admission/action integration next, no QA yet. Do not mark full scope or goal complete. No status upgrades to Reviewed/SystemDone/ContentDone.


## 2026-09-26 — 사용자 설정 정정: 촬영 카메라 없음 / 신들의 눈
최신 사용자 직접 지시가 이전 PDF 예시와 개발 메모보다 우선한다. 게임 속 참가자용 촬영 카메라·영상 열람 단말은 없다. 신들은 어두운 구석의 눈알을 통해 관찰한다. 눈은 플레이어가 보지 않을 때 활성화되며 참가 NPC는 이용하지 않는다. Unity의 플레이어 화면 렌더링 Camera와 혼동하지 말 것.

이번 소스 변경: MansionRuntime에서 촬영 생산/프레임 갱신/열람/상호작용/저장 복원을 제거. 기존 장치 루트는 상호작용 대상 검색 전에 비활성화하고 기존 MansionRecordingStation도 Awake에서 비활성화한다. ProductionGameSetup의 장치 생성 호출 제거, 이전 생성기는 호환용 no-op. 촬영 자료의 LR07 연결 제거, 신규 촬영 지식 수신 금지, 옛 영상 및 전언의 수첩 목록/최근 관측/재전달/추론 사용 차단. 기존 저장 파일은 직접 수정하지 않으며 로드 시 옛 녹화 페이로드와 선택 UI만 폐기한다. 수신 번호와 과거 참조의 구조 보존을 위해 원본 기억은 보관하며 Find는 구조적 복원에 남아 있다. 과거 가설/대화에 저장된 서술 자체를 새 설정의 사실로 변환하지 않는다. 이 오래된 서술 처리 범위는 최종 호환성 검수에서 확인할 것.

신규 MansionDivineEye: 명시적으로 지정한 어두운 코너에서만 활성화. 플레이어 화면 시야 + 시선 차폐를 검사하고, 안 보는 시간이 0.2초 지나면 열림. 시야에 들어오면 즉시 닫힘; 렌더 직전 재검사; 플레이어 시점 미설정/비활성/시야 불확실 때 닫힘. 다른 카메라나 NPC 시선에는 반응하지 않음. FixtureTarget/Knowledge/기록 매체/상호작용/인물용 수신 기능 없음. 새 게임 Awake에서 플레이어 화면 시점만 연결. MansionDivineEyeBuilder.BuildAt은 명시적인 어두운 코너와 재질을 받아 임시 눈 모양을 만드는 저작 도구이며 실제 씬 배치/최종 모델은 아직 하지 않음. 눈의 상태는 시선으로 다시 계산하고 사건 증거로 저장하지 않음.

직전 작업 중 추가한 MansionIncidentSite/IncidentAdmission에서 카메라 기반 입증 경로를 제거했다. 실제 목격과 물리적 접근 경로만 남겼다. 참가자가 독립적으로 확보할 수 있는 보존된 비영상 증거 경로는 아직 미구현이다. 신들의 관측을 여기에 대입하거나 사건 승인 조건을 완화하지 말 것. 따라서 일반 제작 사건이 완성·활성화됐다고 주장하지 말 것.

검수는 전체 기능 구현 후 한번에 하라는 사용자 지시 유지. 컴파일/테스트/빌드/에디터/시각 검수 실행 없음. 배포 v0.22와 직렬화된 Mansion_Playable 씬은 이번에 수정하지 않음. 기존 씬의 촬영 장치 데이터는 다음 에디터 저작 단계에서 제거 필요(현재 소스 실행 시 비활성화). 전체 목표 미완료. 다음은 카메라 없는 실제 물증 경로, 사건/목적 콘텐츠 및 핵심 시스템 연결을 계속한다.


## Continuation — non-camera physical pigment transfer and personal inspection (source only; no QA)
Previous turn is PROGRESS: removed live surveillance paths and added isolated deity-eye behavior. This turn changes authoritative source again. No subagents, no background processes. Full goal remains ACTIVE and incomplete. User core-first and all-functionality-before-batch-QA instructions remain in force. No compilation/tests/build/editor or visual QA performed; v0.22 and serialized Mansion_Playable unchanged.

Added Core/SurfacePattern.cs: visible 4x4 pigment mask, palette and physical width; immutable copies, shape validation, human-readable Korean grid. SurfacePatternComparison compares only directly observed current-loop Visual SurfacePattern/ContactPattern records; same physical root is not a second trace. Rotation and mirror matching allowed; same visible shape never proves exclusive source, actor, deposition time, intention or murder. Comparison is a read-only interpretation, no synthetic observation or causal conclusion. No evidence from deity eyes.

Added World/Mansion/MansionSurfaceTraces.cs: deposits only on runtime physical adapter contact, outside incident state. Persistent marks contain authority-only source id, actual receiving surface, local position/normal/up, observed pattern and actual deposition tick. The player observation omits source id and deposition tick. A stationary repeated print at the same point/pattern returns the same physical mark root, even if a second identical implement touches it; copies do not inflate independent clues. Restore validates sequence/loop/time/surface/source references, finite unit normal/tangent and pattern. Marks survive chapters and reset at loop. No decay/cleaning/transfer contamination mechanic yet; do not claim forensic completeness.

New author components: MansionTraceSource (held object with pigment face), MansionTraceSurface (explicit receiving collider/material and FixtureTarget), MansionTracePatternVisual (same mask geometry is actually drawn with lit pigment, no collider). Actual contact uses forward short ray and four corner support rays, first obstruction, aligned normal; no incident lookup or automatic culprit marking. Mirrored imprint uses actual face tangent. Runtime applies marks on the receiving transform, so movement carries the mark. Author components should be on unscaled roots; arbitrary nonunit authored scale has not been calibrated. Final artwork is still pending.

New Bootstrap/MansionRuntime.SurfaceTraces.cs connects init, ordinary simulation after physical presentation, object/surface inspection, save migration/capture/restore and chapter-loop transitions. Knowledge is written only for actual active rendered pattern at <=1.5m, observer view cone, front-facing surface and unobstructed ray. Repeated observations share the physical root. Inspection currently uses existing player 120-tick inspection flow and checks pattern visibility on completion; NPC inspection requires 120 consecutive visible ticks while actually Performing Examine, with interruption restart, no omniscient target search. NPC chooses only among visible not-yet-read features. Core SurfaceReadingProgress and session SurfaceReadings preserve pending NPC cursor; old saves start at current tick with no retroactively created marks. New optional bit32768 distinguishes absent content. Scene refs must exist for restored marks.

UI change (Unity UI/uGUI skills read; user overrides per-edit QA): existing '살펴본 것' list offers '무늬 비교' only after two direct observed physical roots exist. Uses existing clickable scroll rows: choose A/B by clicking full texts; no next-record carousel, no automatic guilt or notebook claim. Context displays only the two known observations and concrete limits. No new screen hierarchy. Source list renderer and saved UI need final batch verification with typography/layout.

Editor/Authoring/MansionTraceToolsBuilder adds workshop marking supplies via ProductionGameSetup.MakeObjects: grooved blue chalk and a receiving board, lit pigment material, collider/visual/source ids. This is source authoring integration ONLY, not applied to existing scene. It supplies benign-use transfer rather than a case-specific magic clue. Held-face orientation follows existing +90-degree item pose. IMPORTANT next work: actual tool-use/reach action and board placement must be integrated so ordinary users can press the chalk against the board without awkward movement; current only collision-driven deposition should not be described as a finished interaction. No final contact/placement QA done per user.

Production incident admission still lacks a preserved independently accessible non-camera proof route. This generic physical pattern mechanic alone DOES NOT satisfy Identity/KnowingAction/Cause/ResultLink and is intentionally NOT counted as such. Existing case TracePresent flag/InspectTrace recipe remains separate and needs replacing/binding to actual authored physical traces; do not claim it was removed this turn. Next proceed to purposeful physical tool-use/reach, evidence composition (no identity from pattern alone), real authored case/goals and scene/content wiring. Do not mark content Reviewed or whole system complete.


## Continuation — player tool press connected to physical arms/contact; NO QA
Previous goal turn is PROGRESS (persistent surface traces, observations, comparison UI and authoring source). This turn again edits authoritative game source. User core-first, no surveillance cameras, no NPC access to deity eyes, and batch QA after functionality still govern. No subagents/processes/editor/compile/test/build/visual checks. Released v0.22 and serialized scene unchanged; full scope ACTIVE, incomplete.

New Core/ToolPressState.cs and Bootstrap/MansionRuntime.ToolPress.cs (+meta): player holding an authored pigment tool can select a compatible surface once, using existing interaction action '표면에 도구 대기'. No extra menu or record selection. Uses real ActorArmRig; 42 ticks reaching, 18 contact, 36 returning. Keeps custody and fixed body facing while allowing limited head look. Actual first surface hit determines normal and source-face orientation; no teleport of player or stretch of arm lengths. Preflight tool path + per-step oriented box overlap/sweep with rotation padding and upper/forearm capsule checks. Target is not excluded from collisions. Invalid reach/obstruction/changed surface/tool/pattern/lost visibility/movement/custody or >50-degree look cancels; a pressed mark stays physical after cancellation. Source transform/face and surface roots require unit scale. Tool scale support remains limited by explicit requirement, not an incorrect millimeter observation.

Integrated AdvanceToolPress before other hand actions and PresentArms, with press object position/rotation override. Fixed regular held pigment-tool rotation too: earlier generic held-item pose rotated only LoanPen. Movement and another interaction cancel the press. Examine/Talk cancel before switching; writing, waiting, handoff, return transfer, conversation start guard against concurrent press. Court start cancels press before automatic save. RescueFeedback shows simple current action/progress. Existing world pause suspends the motion. Save DTO includes copy and optional bit65536; decode old sessions to Idle; capture/restore saves cursor/pose/contact/mark reference; chapter and loop transitions reset motion. ValidateToolPress checks registered refs, phase/timing, unit pose axes, finite positions, final contact-face relation, hand ownership and physical mark compatibility; remains unexecuted code pending final QA.

Surface contact sampling corrected: pigment only transfers within 2 mm of the face (ray origin is 10 mm behind it), with all four corners supported within the same tolerance. Previous 35 mm query alone was too permissive; broad query remains but distance is now constrained. Matching existing mark can be referenced for action feedback without claiming it was newly made by this source; source identity remains absent from public mark observations. Pattern/surface compatibility and prior deposition bound validated, not a fabricated new depositor.

At actual supported contact during the player's press, living capable observers receive UsedObject only if they really see the tool and contact and identify the actor (self does not need to see own face). One root per action/per witness; repeated ticks don't inflate witnesses. Text covers the actual person/tool/surface touch at the observed tick, not the actor's private goal, old stains, death or intentional cause. This is ordinary personal knowledge, not an eye/camera recording. Existing recent-observation dialogue can deliver that observed use normally. NPC tool-use choice/press execution remains unimplemented; do not infer that new read/observation support means NPCs now intentionally use chalk.

MansionTraceToolsBuilder moves the board to the workbench front based on collider bounds and slightly above the top, rather than placing it deep behind the bench collider. This is authoring source only; actual playable placement and avatar height/reach/occlusion remain unverified. No claim that old v0.22 includes this mechanic or that all full-game systems are complete.

Next continue non-camera incident integration: bind actual physical trace production and scoped witness/use/rule evidence instead of the old incident TracePresent flag/InspectTrace recipe; implement legitimately independent preserved proof composition without making pigment pattern equal unique actor identity. Production admission still lacks complete non-camera independent proof routes; keep conservative. Broader reviewed case content, full personal purposes, scene application, graphics/models and final QA still outstanding. Do not mark Reviewed/SystemDone based on this source change.


## 2026-09-26 — 카메라 없는 물증 연결과 관측 범위 (소스 구현, 검수 보류)
사용자 설정: 참가자용 카메라/녹화 열람 없음. 어두운 코너의 눈은 플레이어가 보지 않을 때만 신들이 관찰하는 장치이며 NPC 지식·증거 경로에 연결하지 않는다. 이미 반영한 촬영 경로 폐기와 눈의 격리를 유지한다.

일반 제작 사건은 원인이 확정됐다는 이유만으로 TracePresent/가상 흔적 관찰을 생성하지 않는다. 기존 InspectTrace는 명시적 시험 장면에만 남기고 일반 장면은 실제 표면 검사로 보낸다. 실제 안료 접촉으로 생긴 물리적 자국만 같은 시각·도구·대상·접촉 위치의 사건에 내부적으로 연결한다. 저장 복원은 원래 자국의 ID/도구/표면/발생 시각/표면 내 위치를 대조하며, 연결 자체가 관측 자료를 발급하지 않는다. 새 IncidentPhysicalTrace 및 PhysicalEvidenceScope 소스와 Unity 메타데이터 추가.

직접 읽은 표면 무늬는 대화와 사건 자료 선택에 사용할 수 있다. 표면의 주체와 자국을 남긴 행위자를 분리하고, 얼굴을 식별하지 못한 인물의 표면 관측은 UNKNOWN_SURFACE로 남긴다. 재구성에서도 무늬만으로 행위자나 발생 시각을 확정하지 않으며, 전달받은 관측은 별도 전제가 필요한 자료로 표시한다. 추론기는 표면 상태만으로 사용/원인 행동을 입증하지 않는다. 신들의 눈에서 나온 자료는 없다.

이 연결은 완전한 독립 입증 경로가 아니다. 보존된 비영상 자료로 신원·인지한 실행·원인·결과를 모두 입증하는 경로, 실제 사건/개인 목적 콘텐츠와 씬 배치는 계속 미구현이며 승인 조건을 완화하지 않는다. 제작 사건 완성이나 콘텐츠 Reviewed를 선언하지 않는다.
요청대로 컴파일/테스트/에디터/시각 검수/빌드 실행 없음. 배포 v0.22와 직렬화된 Mansion_Playable 씬은 변경하지 않음. 전체 게임 목표는 미완료.


## 2026-09-26 — 직접 살펴보기의 연속 관찰과 저장 연결 (소스 구현)
살펴보기를 시작할 때 실제로 보이는 표면 특징을 고정한다. 같은 대상의 여러 무늬는 아직 직접 읽지 않은 것, 가까운 것 순으로 선택한다. 추가 선택 메뉴 없이 기존 살펴보기 동작을 이용한다. 120틱 동안 같은 자국·무늬를 실제 시야 안에서 계속 읽어야 수첩 기록을 얻으며, 마지막 순간 새로 보인 다른 자국은 함께 관측한 것으로 처리하지 않는다. 일반 대상도 조사 중 시선 범위를 계속 확인한다.

이동, 대화 시작, 재판 진입, 손동작 경합, 행동 불가, 대상 접근 불가, 시야 차폐, 무늬 변경은 조사를 중단한다. 일시정지 시간은 진행에 더하지 않는다. 같은 대상의 조사 버튼을 반복해도 진행도를 초기화하지 않는다. 관찰 처리는 실제 몸/물건 위치와 표면 표현을 갱신한 뒤 실행한다. 자료 읽기 함수가 관측을 발급하지 못한 경우에는 빈 자료로 조사 완료를 표시하지 않는다.

InspectionState에 초점 종류·물리적 뿌리·무늬·시작/마지막 시각을 보존한다. 저장은 131072 존재 비트를 사용하며 옛 진행 중 조사는 중단 상태로 가져와 새 관측을 발명하지 않는다. 자동 저장이 틱 중간에 발생하면 저장본의 미완료 조사만 중단 상태로 둔다. 실행 중인 조사 자체는 계속 진행한다. 복원은 대상/물리적 무늬/시각/진행도의 일치를 요구한다. 시야는 다음 실제 틱에서 재확인한다.

플레이어와 NPC가 같은 표면 후보 목록을 사용한다. 인물의 표면에서 얼굴을 식별하지 못했을 때와 나중에 직접 식별했을 때의 관측을 구별하되 물리적 자국의 뿌리는 유지하여 독립 증거 수를 부풀리지 않는다. 새 관측은 해당 표면의 정체를 나타낼 뿐 자국을 남긴 행위자를 확정하지 않는다.

검수는 전체 기능 뒤에 하라는 지시 유지: 컴파일/테스트/에디터 실행/시각 검수/빌드 없음. 배포 v0.22와 직렬화 씬 변경 없음. 핵심 사건의 비영상 독립 입증 경로, 실제 제작 콘텐츠와 씬 배치, 모든 그래픽/모델링을 포함한 전체 목표는 여전히 미완료.


## 2026-09-26 — 실제 안내문 표시와 개인별 읽기 (소스 구현, 일괄 검수 대기)
장치 안내문은 더 이상 내부 Definition 데이터만으로 읽을 수 없다. MansionIncidentRulePlate는 명시적인 현장 TextMeshPro 글자를 참조하고, 실제 표시 문구가 공개 안내와 개정 번호에 일치해야 한다. 비활성/투명/미표시/잘린 글자, 대체 글리프, 너무 작은 문자, 뒷면, 실제 차폐를 통과해 읽은 것으로 처리하지 않는다. 플레이어는 실제 화면 안의 글자만 읽으며 NPC는 자신의 위치와 시야로 읽는다. 이 판정은 조명/명암/디스플레이 해상도에 따른 모든 가독성을 증명하는 완성된 시지각 모델은 아니며 실제 씬의 최종 가독성 검수가 남는다.

플레이어의 안내문 조사는 공개된 본문 길이에 따른 읽기 시간(최소 180틱, 문자당 5틱)을 NPC와 공유한다. 시작할 때 안내 ID·개정·원문을 고정하여 중간 변경 시 취소한다. 저장된 진행도도 해당 원문/개정/시간에 묶는다. 정상적으로 끝난 읽기만 자신의 수첩에 Document 관측을 발급하며 다른 인물의 수신은 생성하지 않는다.

자기 목적 저작 컴포넌트가 있는 NPC만 읽던 제한을 제거했다. 살아 있고 실제 Examine/Performing 중인 참가자가 가까이 보이는 미독 안내문을 하나 선택해 연속으로 읽는다. 기존에 읽던 문서 우선, 그다음 가까운 문서를 고르며 보이지 않는 문서의 위치를 지식에 넣지 않는다. 안내문에 집중한 틱에는 표면 자국 읽기를 진행하지 않는다. 읽기는 몸과 물건의 표현을 갱신한 이후에 실행된다. 같은 문서의 다른 게시 위치는 기존 의미 뿌리를 유지하며 독립 증거로 부풀리지 않는다.

사건의 접근 가능 경로 심사에서도 안내문은 빈 FixtureTarget의 위치가 아니라 실제 글자면을 읽을 수 있어야 한다. 새 MansionRulePlateBuilder는 저작자가 지정한 장소/정의/글꼴/재질로 현장 안내판을 만든다. 기존 한글 TMP 자산을 받으며 고정 글자 크기와 본문 길이에 맞는 판 높이를 사용한다. 자동 사건 생성, 콘텐츠 Reviewed 승격, 신들의 눈 정보 연결은 없다.

컴파일/테스트/에디터 실행/시각 검수/빌드 없음. 사용자 요청대로 기능 구현 뒤 일괄 검수한다. 직렬화된 Mansion_Playable과 배포 v0.22는 그대로이며 새 안내판의 실제 씬 배치/최종 아트도 아직 하지 않았다. 비영상 독립 입증 경로, 전체 제작 사건/목적/대사 콘텐츠와 그래픽·3D 모델을 포함한 전체 목표 미완료.


## 2026-09-26 — 문 상태·움직임·직접 열기 결과의 분리 (소스 구현)
통합기획서 69~75쪽과 103쪽을 다시 읽었다. 카메라를 뺀 상태에서도 보존된 비인물 경로가 필요하며, 일반 접촉이나 문 상태를 행위자·고의·결과의 완전한 증거로 대체할 수 없다. 기존 책임 입증 승인 조건은 완화하지 않았다. 이번 구현은 IG04/IG05의 문 상태와 과거 시각 구분에 필요한 관측 기반이며 완전한 비영상 책임 입증 경로를 완성했다고 주장하지 않는다.

실제 문짝 회전과 가까운 시야를 관측해 열린/닫힌/일부 열린 현재 상태를 자신의 지식에 남긴다. 양개문은 양쪽 문짝을 실제로 볼 수 있어야 전체 상태를 관측한다. 연속된 두 틱에서 같은 문을 보았을 때만 열리는/닫히는 움직임을 기록하며 시야가 끊긴 구간을 이어 붙이지 않는다. 화면 밖·숨긴 레이어·다른 장애물 뒤의 문은 플레이어 관측을 생성하지 않는다. 자동 관측은 실제 몸/물체 표현 이후 실행한다. 오래된 World.Door.Locked 내부값을 보고 조사 결과로 잠김을 단정하던 경로는 제거했다.

권한 심사를 통과한 실제 문 열기 시도가 잠금 저항으로 실패한 경우, 본인에게만 DoorAttempt/WouldNotOpen 촉각 기록을 전달한다. 허락 거부는 이 촉각 기록과 구분한다. 자동 재잠금 이벤트에 붙은 소유자 이름을 목격자 지식으로 전달하지 않는다. 문 관찰의 원본 그룹은 회차/관측자/문 기준으로 고정하여 반복 시도나 시각·촉각 자료를 여러 독립 증인으로 세지 않는다. 신규 이벤트는 순번으로 찾아 처리하며 옛 저장의 이벤트를 소급 관측하지 않는다.

조사 자료는 대화로 전달하고 재구성에 놓을 수 있다. 표면/문 같은 물체의 주체와 행위자를 구분한다. 문 상태·움직임·열기 실패만으로 통과자/사용자/치명 원인을 확정할 수 없다는 추론 제한을 추가했다. 현재 관찰 시각 밖에 대해서는 기존 LR08의 나중 상태 제한을 유지한다. 기존 저장에 이미 들어 있는 옛 서술과 재구성은 구조 호환을 위해 남기되 새 관측으로 재작성하지 않는다.

저장에는 문별 연속 관찰의 마지막 실제 상태/시각과 처리 이벤트 순번을 넣는다. 옛 파일은 현재 이벤트 순번부터 새로 시작한다. 챕터/루프 전환은 관찰 진행도를 끊고, 루프 지식 초기화는 기존 절차를 따른다. 소스 구현만 했으며 컴파일/테스트/에디터 실행/시각 검수/빌드 없음. 직렬화 씬과 배포 v0.22 그대로. 전체 사건/개인 목적/대사 콘텐츠, 비영상 완전 입증 경로, 그래픽·3D 모델 및 일괄 검수 등 전체 목표는 미완료.


## 2026-09-26 — 연속 목격에 기반한 실제 문 통과 (소스 구현)
문 열림 관측과 분리된 PassedDoor 관측을 연결했다. 목격자는 실제 문틀과 이동하는 인물의 몸을 가까운 시야에서 계속 보아야 한다. 문 폭/높이와 참가자 몸 크기를 반영한 통과 구간 안에서, 한쪽에서 시작해 몸이 반대편으로 완전히 빠져나온 뒤에만 출입 기록을 받는다. 중간 시야 차단·대상 소실·틱 공백·급격한 위치 변경은 연속 추적을 끊는다. 문 앞에서 돌아선 경우나 중간부터 보기 시작한 경우에는 완료된 출입을 만들어내지 않는다.

연속 목격 중 얼굴을 실제로 확인한 경우에는 그 직접 관측 원문을 따로 보존하고 신원을 연결한다. 얼굴 확인이 없으면 공개 기록의 주체는 UNKNOWN_ACTOR이며 내부 추적용 Subject를 노출하지 않는다. 문턱을 넘는 것을 본 틱과 반대편으로 완전히 넘어갔다고 확인한 틱을 구분하며, 전자를 출입 관측 시각으로 쓰고 후자에 지식을 수신한다. 통과 준비 구간 전체를 매 순간 출입한 것으로 간주하지 않는다.

문 상태·조작 저항·얼굴 확인·출입은 같은 목격자/문/회차의 근원 그룹을 공유한다. 여러 문장과 전언이 독립된 여러 증인으로 부풀려지지 않는다. PassedDoor는 통과한 문과 신원(확인된 경우)만 지지하며 잠금 조작·유일 출입자·물건 사용·고의·살인의 원인은 증명하지 않는다. 대화의 최근 직접 관측으로 전달할 수 있고 기존 재구성 자료 참조를 이용한다. 다른 참가자의 통과를 보는 기능이며 자신의 통과는 이 목격 모듈에서 자동 발급하지 않는다.

저장에는 출입 추적 위치/시작 측/문턱 통과 시각/신원 확인 원문을 보존하고 원래 목격자의 지식과 대조한다. 예전 파일은 추적 없음으로 시작하며 과거 이동 경로를 소급 복원하지 않는다. 이전 턴의 챕터/루프 관찰 초기화가 출입 추적에도 적용된다. 한국어 인물 주격 조사도 이름에 맞춰 사용한다.

소스 구현만 완료. 컴파일/테스트/에디터 실행/시각 검수/빌드 없음. 배포 v0.22와 직렬화 씬 그대로. 실제 인물 모델의 얼굴 방향과 문틀 시야/규모/성능은 최종 검수에서 확인해야 한다. 이 목격은 보존된 비인물 책임 입증 경로를 대신하지 않으며 전체 사건 콘텐츠·핵심 시스템·그래픽·3D 모델 및 최종 검수까지의 목표는 미완료.

## 2026-09-26 — 연속 체류 목격과 알리바이 범위 (소스 구현)
카메라 없는 세계 설정을 유지한다. 신들의 눈은 어두운 구석에서 플레이어가 보지 않을 때 활성화되는 연출이며, 참가자의 사용 장치나 지식/증거 공급원이 아니다. 이번 변경은 참가자 자신의 실제 시야와 수첩에만 연결했다.

기존의 30틱마다 몸통을 보고 바로 실명을 부여하던 인물 위치 관측을 연속 시야 기반 관측으로 교체했다. 실제 몸의 가시성, 관측 틱의 연속성, 같은 장소, 연속적인 이동을 요구한다. 시야가 끊기거나 장소가 바뀌거나 급격히 이동하면 기록을 마지막으로 본 틱에서 닫는다. 실제 얼굴 확인 전에는 신원 미확인 인물로 남기며, 얼굴을 확인한 원문과 끊기지 않은 몸의 추적이 있을 때만 신원을 연결한다. 위치 좌표는 관측 구간의 마지막 지점이며 전체 이동 경로를 뜻하지 않는다.

관측 시작/신원 확인/5초 간격/관측 종료에 불변 원문을 발급한다. 최근 목격을 물으면 그 인물이 실제로 본 마지막 틱까지만 정리하고, 재판 시작 전에도 남은 관측을 닫는다. 이미 전달한 원문을 나중 관측으로 덮어쓰지 않는다. 수첩 목록은 같은 연속 관측의 최신 원문을 대표로 표시하되, 선택 중이거나 가설에 인용한 원문은 유지한다. 원본 저장소는 지우지 않는다.

개인 지식에 들어 있는 신원 확인 직접 관측들이 선택된 시간 전체를 빈틈없이 덮을 때만 연속 체류/다른 장소의 알리바이를 지지한다. 부분적으로라도 서로 다른 장소의 직접 관측이 동시에 겹치면 먼저 충돌 확인을 요구한다. 관측 공백을 보간하지 않는다. 위치 지도의 마지막 확인 시각은 구간의 마지막 틱을 사용한다.

원 관측 종류와 관측자를 자료/전언에 보존한다. 기존 저장은 이미 들어 있던 원문 연결만 따라 출처를 복원한다. 논리 판정의 독립 근원 계산에서 같은 관측자의 시각/촉각 원문과 그 전언을 여러 독립 증인으로 세지 않는다. 실제 표면 자국은 기존의 물리적 근원을 유지한다. 이 변경은 LogicResolver에 적용되며 전체 사건 승인 규칙을 완화하지 않는다.

진행 중 관측은 저장/복원과 챕터·회차 초기화에 연결했다. 옛 저장은 연속 관측 없음으로 시작하며 과거 체류를 새로 만들어 내지 않는다. 챕터 전환은 진행 관측을 지우되 같은 회차의 순번을 보존한다. 저장 자료의 대상/장소/시각/얼굴 원문/발급 원문 일치 조건을 추가했다.

컴파일·테스트·에디터 실행·시각 검수·빌드 없음. 사용자 요청에 따라 기능 구현 뒤 일괄 검수한다. 직렬화 씬과 배포 v0.22는 변경하지 않았다. 실제 모델의 얼굴/시야 판정과 매 틱 관측의 성능, 최종 UX는 일괄 검수 대상이다. 보존된 비인물 입증 경로, 실제 전체 사건/개인 목적/대사 콘텐츠, 그래픽·3D 모델을 포함한 전체 목표는 미완료.

## 2026-09-26 — NPC의 실제 표면 도구 사용 (소스 구현)
직전 목표 턴은 연속 목격/알리바이 원문·저장 연결로 PROGRESS. 이번 턴은 플레이어 전용이던 표면 도구 동작을 참가자 공통으로 확장했다. 신들의 눈·카메라·녹화 지식은 사용하지 않는다.

준비 단계는 실제 손의 소유 물건, 가시적인 표면, 팔 길이와 팔 공간, 도구의 이동 경로를 확인한다. 플레이어도 같은 준비 함수를 사용한다. NPC에게 저작된 MarkSurface:<표면ID> 생활 행동이 있을 때만 작업을 시도한다. 그 ID는 원하는 작업 대상일 뿐 현재 위치/상태의 관측 자료를 부여하지 않는다. NPC의 다른 목적·구조·대화·약속·소집·물건 전달과 충돌하면 실행하지 않거나 중단한다. 한 번의 생활 방문에서 성공/취소한 작업을 매 틱 다시 시작하지 않도록 행동의 종료 시각과 노드를 묶는다.

NPC도 42틱 뻗기, 18틱 접촉, 36틱 거두기의 기존 동작과 실제 팔 자세를 사용한다. 손이 실제 물건을 소유하고 있어야 하며, 움직임/시야 상실/대상 이동/장애물/행동 변경 시 중단한다. 표현 단계에서 NPC의 손과 물건에 같은 동작을 적용하고, 기존 실제 표면 접촉기가 완전히 지지된 접촉을 찾았을 때만 자국과 사용 관측을 연결한다. 관측자에게는 실제 인물의 얼굴·도구·접촉 장면을 본 범위만 남긴다. 의도나 사건 원인을 자동으로 부여하지 않는다. 도구나 자국의 숨은 소유 이력을 증언으로 공개하지 않는다.

이미 남긴 물리적 자국은 작업 취소로 되돌리지 않는다. 동일한 자리에 같은 무늬가 이미 있으면 기존 자국 합침 규칙을 유지하며 독립 단서를 늘리지 않는다. 플레이어와 NPC의 서로 무관한 작업은 병행할 수 있고, 작업 중인 손으로 물건을 건네는 요청은 거절한다. 직접 대화를 걸면 NPC의 작업 동작을 먼저 중단한다. 재판 시작과 챕터/회차 전환도 진행 중 작업을 닫는다.

진행 중 도구 자세/주인/활동 방문 정보를 저장하고 검증하는 코드를 연결했다. 과거 파일의 플레이어 작업은 CH_01로 이관하며 NPC 작업은 빈 상태로 시작한다. 작업을 중단한 직후의 자동 저장은 사본의 작업도 취소 상태로 보존한다.

차기 씬 생성 코드에서 기존 작업실 Craft 생활 방문을 작업대 표식판 사용으로 연결했다. 손에 적절한 도구가 없으면 사용하지 않는다. 도구를 자동 지급하거나 멀리 있는 물건을 집게 하지 않았다. 가까운 물건을 향한 NPC의 별도 집기/반납 동작과 전체 생활 요청·목적 콘텐츠는 후속 구현이 필요하다. 이번 행동은 비폭력 생활 사용이며 치명 원인 실행/보존된 완전 책임 입증 경로를 완성한 것은 아니다.

컴파일/테스트/에디터 실행/씬 생성/시각 검수/빌드 없음. 기존 직렬화 씬과 배포 v0.22 그대로. 기능 구현 후 일괄 검수한다. 전체 사건·목적·대사 콘텐츠와 그래픽·3D 모델 등 최종 목표는 미완료.

## 2026-09-26 — 생활 작업에 필요한 도구를 직접 집기 (소스 구현)
직전 턴은 NPC 표면 도구 사용과 저장 연결로 PROGRESS. 이번 턴은 손에 도구가 없을 때의 실제 집기→손으로 가져오기→작업 흐름을 추가했다. 카메라/신들의 눈/비공개 사건 정답을 읽지 않는다.

개인 생활 일정에 원하는 도구 ID를 저작할 수 있다. 일정의 도구 ID는 물건의 현재 위치를 알려 주지 않는다. NPC가 최근 직접 위치를 본 물건이어야 하며, 현재 실제로 보이고 가까이 닿을 수 있는 물건에만 손을 뻗는다. 다른 사람이 들고 있거나 서랍 안에 있는 물건을 공중에서 가져오지 않는다. 이번 범위는 작업 장소 가까이 있는 물건 집기이며, 모르는 장소를 자동 탐색하거나 멀리 떨어진 물건을 추적하는 새 계획은 아직 구현하지 않았다.

집기는 손 뻗기 60틱→손가락 닫기 12틱→물건 들어 올리기 30틱→손으로 가져오기 42틱의 동작이다. 도구를 잡는 순간까지 손은 비어 있으며, 실제 손 위치와 소유 가능 상태를 다시 확인한 뒤 한 번만 소유권을 바꾼다. 두 사람이 같은 도구를 향해 손을 뻗어도 먼저 실제로 잡은 한 사람만 소유한다. 나머지는 다음 물리 상태 확인에서 중단한다. 손/팔 경로와 소유 후 도구 경로를 검사한다.

물건을 집기 전에 이동·시야 상실·다른 사람의 집기·장애물·행동 변경이 있으면 집기가 중단된다. 이미 잡은 뒤 중단된 경우에는 소유 상태를 취소하지 않는다. 그 시점의 직접 집기/목격 기록만 개인 지식에 남기며 사용 목적이나 사건 원인을 자동으로 추가하지 않는다. 표현 단계는 손과 물건에 같은 집기 자세를 사용하고, 집기를 마친 뒤 기존 표면 사용 흐름으로 넘어간다.

대화·소집·챕터/회차 전환의 취소 처리와 저장/복원을 연결했다. 저장에는 당시 위치를 직접 본 원문, 작업 방문, 자세, 손의 소유권이 바뀐 틱을 묶는다. 옛 파일은 진행 중 집기 없음으로 시작한다. 작업실 Craft 일정의 차기 생성 코드에는 표식 분필을 원하는 도구로 지정했다. NPC용 도구 반납, 다른 생활 도구/상황, 기존 일반 집기 및 취소 후 팔 복귀의 자연스러운 연출은 추가 구현/최종 검수가 남는다.

컴파일/테스트/에디터/씬 생성/빌드/시각 검수 없음. 직렬화 씬과 배포 v0.22는 그대로다. 실제 모델/작업대와의 손 접촉·도구 각도·방해 시 복귀는 기능 완성 후 일괄 검수해야 한다. 전체 시스템/사건·목적·대사 콘텐츠/그래픽·3D 모델 목표 미완료.

## 2026-09-26 — NPC 작업 도구의 실제 내려놓기 (소스 구현)
직전 목표 턴은 NPC 집기·소유권·표현·저장 연결로 PROGRESS. 이번 턴은 직접 집어 작업에 쓴 도구를 원래 자리로 가져가 내려놓는 흐름을 연결했다. 카메라나 신들의 눈의 관측을 이용하지 않는다.

NPC의 완료된 집기 이력에 남은 위치/방향을 돌아갈 후보로 삼는다. 같은 생활 방문의 작업이 완료/취소되었을 때 또는 작업을 시작할 수 없어 짧게 기다린 뒤에 반환을 시도한다. 현재도 같은 도구를 실제로 들고 있어야 하며 대화·긴급 행동·약속·소집과 충돌하면 진행하지 않는다. 남은 생활 시간에 집기/사용/반환이 들어갈 수 있도록 새 집기 시작 조건에 반환 시간을 포함했다. 다른 곳으로 이동하는 새로운 반환 경로 계획은 이번 범위에 포함하지 않았다.

손을 뻗기 54틱→받침 쪽으로 낮추기 30틱→손가락 열기 12틱→손 거두기 42틱으로 나누었다. 손/팔/소유 중 물체 이동 경로를 확인한다. 바닥 모서리와 중앙의 다섯 지점이 현재 같은 수평 받침에 놓이는지 확인하고, 기억한 좌표에서 작은 높이 차이만 실제 받침 위로 조정한다. 받침이 없거나 기울어졌거나 다른 인물/물건이 받침을 대신하는 경우에는 내려놓지 않는다.

실제로 손을 여는 순간에도 받침 ID·도구 위치·손의 도달·소유 상태를 다시 확인하고 PlaceAt으로 한 번만 소유권을 풀어 준다. 반환된 물건의 충돌체를 즉시 활성화하여 같은 틱의 다른 반환이 그 자리를 빈 공간으로 취급하지 않게 한다. 소유권이 풀린 뒤에는 손 거두기만 진행하며 다른 사람이 물건을 집더라도 원래 자리로 되돌리지 않는다. 중단은 이미 완료한 내려놓기를 취소하지 않는다.

직접 내려놓은 본인과 실제 인물·도구·장면을 본 관측자에게만 PlacedObject 원문을 발급한다. 실제로 물건이 보이는 경우에는 새 AtPlace 원문도 남긴다. 추론/최근 관측 대화에 내려놓기와 집기 자료를 연결했다. 물건의 용도, 이전 소지자, 사건 원인은 자동 확정하지 않는다.

저장에는 반환 동작, 원래 집기와의 연결, 소유권 해제 틱과 실제 ObjectPlacedAt 사건 순번을 보존한다. 아직 같은 반환 위치에 있고 이후 다른 물건 사건이 없을 때에만 저장된 내려놓기 방향을 물체 표현에 적용한다. 새 집기를 시작하면 그 인물의 이전 반환 진행 자료를 정리해 끊어진 참조를 남기지 않는다. 대화·재판·챕터/회차의 중단 및 옛 저장의 빈 반환 상태 이관을 연결했다.

컴파일/테스트/에디터 실행/씬 생성/시각 검수/빌드 없음. 직렬화 씬과 v0.22 배포는 그대로. 실제 모델의 손끝/물체/받침 간격과 중단 후 팔 복귀는 최종 일괄 검수 대상이다. 다른 생활 도구·수리·요리·원거리 도구 획득/반환, 전체 사건/목적/대사 및 그래픽·3D 모델 목표는 미완료다.


## 2026-09-26 — 인물별 물건 대여·실제 수령·반환 (소스 구현)
펜 하나에 고정되어 있던 대여 코드를 인물/물건별 저작 정의로 확장했다. ItemLoanTerms는 대여 의향과 대사이며 현재 소유 상태나 지식 자료가 아니다. 한 인물은 현재 한 대여 물건을 담당한다. 다른 인물의 물건을 동시에 빌려 두는 것은 가능하지만 같은 물건의 대여 구간은 겹치지 못한다.

허락은 주인이 플레이어에게 실제로 끝까지 말하고, 플레이어가 그 원문을 끝까지 들었을 때만 유효하다. 발화자의 Speech 원문·부모 참조·수신 시각을 묶어 다른 사람이 전한 말이나 오래된 원문의 재전달을 새 허락으로 쓰지 않는다. 반환 뒤에는 새 허락이 필요하고, 이번에는 빌리지 않겠다는 선택에 대한 상대의 답을 끝까지 들으면 이전 제안은 닫힌다. 중도에 끊긴 말로 전체 합의를 만들지 않는다. 거절/다음에 다시 물어보기에는 관계 보상·물건 지급이 없다.

실제 전달은 기존 접근→양팔 뻗기→90틱 수령→120틱 종료를 사용한다. 소유 중 물건의 크기로 장애물 경로를 확인하며, 수령 전 중단은 주인에게, 수령 후 중단은 받은 사람에게 물건을 남긴다. 반납대에 놓기와 주인이 받는 것은 구분한다. 다른 목격자에게는 실제로 두 사람의 얼굴과 전달 장면을 연속해서 본 경우에만 기록을 남긴다. 카메라/녹화/신들의 눈에서 지식을 얻지 않는다.

기존 대화 버튼을 물건 이름에 맞춰 바꾸고 받기/반환/이번에는 안 빌리기와 짧은 다음 행동 안내를 연결했다. 실제 반환이 가능한 경우에는 대화 첫 화면에서 바로 돌려줄 수 있다. 기존 AskToBorrowPen/ReceivePen 이름은 호환용으로 남았으며 실행은 저작된 물건을 선택한다. 태겸의 펜 분실·정리함 오해·배운 점은 해당 펜 대여만 참조하게 분리했다.

차기 씬 생성 코드에 진우의 작은 수수께끼 책과 서윤의 눈금 자를 물리 크기/충돌체/구분 가능한 기능용 외형 및 제안·미소지·반환 요청·반환 장소·거절 응답 대사와 함께 추가했다. 둘은 새 게임에서 각 소유자가 들고 시작한다. 이전 저장에 추가될 때에는 새 도입 사건만 남기며 과거 대여나 목격 사실을 만들지 않는다. 기존 손이 차 있거나 주인이 없으면 저작된 휴지 위치에 놓인다. 저장 검증은 허락 원문과 실제 양방향 전달 사건, 물건별 대여 구간을 연결한다. OptionalObjects 16777216은 추가 대여 물건이 있는 씬에만 기록한다.

이것은 일반 대여 기반과 기능용 2개 물건의 소스 연결이다. 수수께끼 풀이/측정 도구 사용, NPC가 플레이어에게 물건을 요청하는 모든 변형, NPC 간 대여, 소유자 사망 후 처리, 전체 36개 생활 요청·36개 장기 목표·관계/대사 콘텐츠를 완성한 것이 아니다. 새 외형도 최종 아트가 아니다.

컴파일/테스트/에디터 실행/씬 생성/시각 검수/빌드 없음. 사용자의 요청대로 기능 완료 후 일괄 검수한다. 현재 직렬화된 씬과 v0.22 배포에는 아직 반영하지 않았다. 전체 목표 계속 진행 중.


## 2026-09-26 — NPC의 생활 부탁·실제 답변·직접 전달 (소스 구현)
직전 인물별 대여에 이어, NPC가 플레이어에게 공용 물건을 가져다 달라고 부탁하는 흐름을 구현했다. 저작 정의는 원하는 물건과 동기/대사만 담으며 보이지 않는 물건의 현재 위치를 알려 주지 않는다. 현재 한 인물의 한 부탁을 지원한다. 차기 씬 생성에 라온(CH_08)이 공용 책(M_BOOK)을 부탁하는 신규 콘텐츠 초안을 연결했다. 이미 만들어진 책의 물리적 실체를 사용하며, 부탁을 수락한다고 책을 지급하거나 위치를 바꾸지 않는다.

NPC의 요청을 플레이어가 끝까지 직접 들으면 Offered가 생긴다. 도와줄게/나중에/어려울 것 같아/맡은 일을 그만둘게는 플레이어가 실제로 말하는 대화다. 상대가 끝까지 들었을 때만 Accepted/Deferred/Declined/Cancelled로 바뀌고, 이어서 상대의 응답이 재생된다. 발화가 중간에 끊기면 전체 답변으로 처리하지 않는다. 미룸/거절/취소는 관계 벌점이 없고 18000틱 동안 같은 요청을 다시 제안하지 않는다. 자동 재권유도 없다. 수락 이후에도 취소할 수 있다.

플레이어가 실제 물건을 집어 들고 다가가며 상대의 손이 비어 있어야 전달할 수 있다. 기존 팔/접근/장애물 경로 동작을 공유하되 Request 전달을 대여/반환과 구별한다. 90틱 수령 시 World.HandOver가 실제로 성공해야 Completed가 된다. 양쪽의 직접 전달 기록, 부탁한 사람의 직접 수령 원문(RequestFulfilled), 일회 관계 경험을 남긴다. 대여 중인 다른 사람의 물건은 이 완료 경로로 넘길 수 없다. 물건을 찾은 경로·보지 못한 행동은 알려 주지 않는다. 전달 이전 중단에는 완료/보상이 없고 이후 중단에는 이미 전달된 물건을 되돌리지 않는다.

요청자의 사망/이탈은 내부 부탁 상태를 중단시키고 진행 중 전달도 닫는다. 이 내부 상태 변경으로 플레이어/NPC에게 원격 사망 알림·관측·물건 회수를 만들지 않는다. 완료된 일은 이후 이탈로 취소하지 않는다. 다음 챕터에는 부탁을 유지하고 회차 리셋에서는 비운다.

대화의 ‘도와줄 일 있어?’에서 요청을 듣고 간단한 답을 고른다. 맡은 일이 있으면 첫 화면에서 다시 이야기할 수 있고, 실제로 건넬 수 있을 때는 전달 버튼이 바로 나온다. 미룬 요청은 재요청 대기 동안 기본 화면을 차지하지 않는다. 완료 뒤 상대에게 수령 답변을 들을 수 있다. 플레이어 노트의 관계 경험은 단순 대화가 아닌 실제 부탁 물건 전달로 표시한다.

저장은 ItemExchange.Requests와 Handoff.RequestId를 보존하며 OptionalObjects 33554432로 옛 파일의 빈 부탁 상태를 이관한다. 들은 요청 원문, 양쪽이 들은 실제 답변, 세계 사건, 재요청 시각, 완료 전달 ID/시각을 묶는 저장 검증을 연결했다. 일반 발화 허용 목록과 펜 전용 발화 검증도 새 응답 종류를 구분한다.

컴파일/테스트/에디터 실행/씬 생성/시각 검수/빌드 없음. 사용자 요청대로 기능을 구현한 후 일괄 검수한다. 현재 직렬화 씬과 배포 v0.22 그대로. 이것은 생활 부탁의 첫 실제 물건 전달 경로이며 전체 36개 요청/분기, NPC 간 요청, 공동 작업, 장기 개인 목표 36개, 기본 1개 목표 고정 표시, 실제 책 읽기/도구 사용/그래픽 최종화 및 사건 전체 목표는 미완료다.


## 2026-09-26 — 현재 할 일 하나 표시·부탁/대여 노트 (소스 구현)
생활 부탁과 대여의 실제 기록을 읽는 할 일 노트를 연결했다. 새로운 자동 퀘스트/정답 목록을 생성하지 않는다. 직접 들은 요청/허락이 있는 부탁과 대여만 목록에 나오며, 아직 답하지 않은 부탁·맡은 부탁·미룬/거절한 부탁·빌린 물건·실제 전달/반환 완료를 구분한다. 현재 일과 지난 일을 나누고 항목을 고르면 직접 들은 원문과 다음 행동을 보여 준다.

기본 화면에는 맡은 부탁 또는 반환할 물건 중 하나만 표시한다. 별도 선택 전에는 먼저 맡은 일을 사용하며 노트에서 다른 일로 바꿀 수 있다. 숨기기/다시 켜기를 제공하며 표시를 숨겨도 실제 부탁이나 대여는 취소하지 않는다. 완료 뒤에는 남은 일로 넘어가고 없으면 표시가 사라진다. 노트/대화/메뉴/일시정지/실제 전달 동작 중에는 이 표시를 숨겨 현재 행동을 가리지 않는다. 글자는 사용자 크기 설정에 따라 커지며 패널 높이는 필요한 텍스트 높이에 맞춘다. 새 버튼용 캔버스나 별도 복잡한 메뉴를 만들지 않고 기존 노트의 목록/스크롤/포커스 구조를 사용했다.

위치 안내는 플레이어가 직접 확인한 AtPlace 원문의 장소와 마지막 관측 시각만 사용한다. 현재 실제 물건 좌표, 상대의 이동 목적지, 숨은 소유 이력은 읽지 않는다. HUD에는 위치 조회를 넣지 않고 간단한 다음 행동만 표시한다. 노트 상세에는 ‘마지막으로 본 곳’과 이후 이동 가능성을 명시한다. 요청자가 화면 밖에서 사망/이탈하여 내부 상태가 Interrupted가 되어도 플레이어가 맡았다는 기억은 표시에서 갑자기 사라지지 않는다. 이 목록 변화로 숨은 사건을 알 수 없게 했다. 아직 별도의 실제 사망/이탈 인지 후 부탁 정리/대체 담당자 흐름까지 구현한 것은 아니다.

고정 항목/숨김은 ItemExchange.Journal에 저장하며 노트 하위 화면·지난 일 표시·선택 항목도 PlayerUiSnapshot에 연결했다. OptionalObjects 67108864로 과거 파일은 기본 1개 표시 설정을 갖는다. 다음 챕터에는 유지되고 새 회차에는 해당 회차의 부탁·대여와 함께 비운다. 저장 검증은 고정 대상이 실제 부탁/대여 기록에 존재하는지 확인한다. HUD는 0.5초 주기로 가볍게 읽고 전체 위치 관측 목록은 노트 상세에서만 조회한다.

컴파일/테스트/에디터 실행/씬 생성/시각 검수/빌드 없음. 기능 구현 후 일괄 검수한다. 현재 배포 v0.22 및 직렬화 씬은 그대로다. 화면 배치/큰 글자/키보드 목록 이동/저장 복원은 최종 통합 검수 대상이다. 전체 개인 장기 목표·36개 생활 요청·추천 활동 3개·NPC 관계/공동 작업·사건 문법/최종 3D와 아트는 아직 미완료다.


## 2026-09-26 — NPC가 받은 책의 실제 본문 읽기 (소스 구현)
생활 부탁으로 전달한 공용 책을 NPC가 직접 들어 읽는 흐름을 추가했다. 차기 씬 생성의 라온(CH_08)에게 M_BOOK을 읽고 싶다는 생활 선호를 연결했다. 손에 그 책을 가지고 있고, 약속/구조/사건/대화 등 우선 행동이 없을 때 현재 자리에서 읽기를 계획한다. 소유하거나 시간이 지났다는 이유만으로 내용을 아는 처리는 없다. 이미 직접 읽은 같은 본문/개정은 반복 완료시키지 않는다. 실패 후 1800틱은 같은 읽기를 재계획하지 않고 일상으로 돌아갈 수 있다.

실제 동작은 60틱 들어 올리기→본문 분량에 따른 연속 읽기(최소 600틱)→60틱 내리기다. 책과 팔의 공간/경로, 실제 손 소유, 일과/자리/시각의 연속성을 확인한다. 눈높이에 맞춰 책과 고개를 돌리고, 세계 공간 TextMeshPro 본문에 모든 글자가 실제 표시되는지 확인한다. 숨김/투명/누락/잘림/다른 문장, 페이지 뒷면, 멀거나 시선 밖인 글, 글자 앞 장애물은 읽기를 완료시키지 않는다. 규칙판과 같은 글자 검사 함수를 공유한다. 모든 글을 계속 볼 수 있었을 때만 읽은 인물 본인에게 해당 페이지의 Document 원문 기록을 남긴다. 주변 사람에게 책 내용이나 원격 관측을 배포하지 않는다. 내용의 진실성이나 다른 페이지까지 알았다는 의미도 부여하지 않는다.

대화/재판/행동 변경/물건 소유 변경/장애물로 중단되면 미완성 읽기에서 전체 본문 지식을 얻지 않는다. 읽기를 완료한 뒤 책을 내리는 도중 중단되어도 이미 읽은 기억은 유지한다. 물건을 없애거나 주인에게 자동 반환하지 않는다. 저장은 동작·본문 개정·연속 읽은 틱·실제 읽기 원문·세계 사건을 함께 보존하며 중간 세계 체크포인트는 아직 처리하지 않은 읽기 틱을 완성시키지 않는다. 새 OptionalObjects 134217728은 이전 저장을 빈 읽기 상태로 이관한다. 챕터 전환은 동작을 비우고 기억을 유지하며 회차 초기화는 양쪽을 초기화한다.

차기 생성 M_BOOK은 같은 외곽 크기/충돌체를 유지하는 펼친 페이지 기능용 모형으로 변경한다. 기존 한글 글꼴을 사용하는 3D 본문 ‘소리와 쉼’을 추가했다. 이는 생활 읽기를 위한 신규 짧은 글이며 사건 정답이나 기획서 원문이라고 주장하지 않는다. 처음부터 펼쳐진 모형이고, 책을 펼치는 애니메이션/여러 페이지 넘기기/앉는 자세/최종 아트까지 구현한 것은 아니다. 읽을 때 드는 손의 실제 모양, 글자 크기/조명/가독성, 동작 길이, 경로/저장 복원은 최종 통합 검수 대상이다.

카메라 관련 사용자 정정을 유지한다. 세계 내 감시 카메라/녹화 시스템은 사용하지 않고, 기존 비활성화·레거시 차단 경로를 유지한다. 신들의 눈은 어두운 구석의 신 전용 장치이며 플레이어가 보지 않을 때만 활성화된다. NPC 접근/증거/원격 지식 생성 경로가 없다. 현재 신들의 눈 배치 및 최종 외형이 모든 공간에 완료되었다는 뜻은 아니다. Unity Camera는 플레이어 화면을 그리는 엔진 구성요소로만 남는다.

컴파일/테스트/에디터 실행/씬 생성/시각 검수/빌드는 하지 않았다. 사용자 요청대로 기능 완료 후 일괄 검수한다. 배포 v0.22와 직렬화된 현재 씬은 그대로다. 전체 게임 기능·36개 생활 부탁/장기 목표·사건 파이프라인·전체 3D 및 아트는 아직 미완료다.
