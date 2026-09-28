using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
namespace BASSLINE.NPC
{
    [Serializable] public sealed class InvitationPolicy
    {
        // PRODUCTION PROPOSAL: reversible negotiation values, not canonical personality statistics or persuasion bonuses.
        public int BaseWillingness=1,PreferredActivityWeight=2,PreferredPlaceWeight=1,TrustedExperienceWeight=1,BoundaryWeight=2;
        public int AcceptThreshold=2,DeclineThreshold=-2,MaximumExperienceInfluence=3;
        public long SuggestedDelayTicks=5*60*60;
    }
    [Serializable] public sealed class InvitationDecision
    {
        public string ActorId,Outcome,Reason,SuggestedActivity="",SuggestedPlace="",SourceParagraph,PolicyStatus="PRODUCTION_PROPOSAL";
        public bool Accepted;public long SuggestedDelayTicks;public string[] BasisExperienceIds=Array.Empty<string>();
    }
    public sealed class ResidentVoiceProfile
    {
        public string ToneRevision="BL22_KO_20260925";
        public string ActorId,DisplayName,SourceParagraph,WritingSourceParagraph,Greeting,BusyGreeting,ReadingGreeting,RestGreeting,Accepted,Negotiating,Declined;
        public string[] PreferredActivities,PreferredPlaces;
        public ResidentVoiceProfile Copy(){var p=(ResidentVoiceProfile)MemberwiseClone();p.PreferredActivities=(string[])PreferredActivities.Clone();p.PreferredPlaces=(string[])PreferredPlaces.Clone();return p;}
    }
    /// <summary>Source-grounded voice, newly written everyday lines. Never imports biography/contract secrets into runtime dialogue.
    /// Disclosure of a specific observation remains an explicit knowledge-sharing action, not a greeting side effect.</summary>
    public sealed class ResidentConversation
    {
        static ResidentVoiceProfile Voice(string actor,string name,int source,string greeting,string busy,string reading,string resting,string accept,string negotiate,string decline,string[] activities,string[] places)
            =>new ResidentVoiceProfile{ActorId=actor,DisplayName=name,SourceParagraph="SRC11_P"+source.ToString("0000"),WritingSourceParagraph="SRC11_P"+(source+2).ToString("0000"),Greeting=greeting,BusyGreeting=busy,ReadingGreeting=reading,RestGreeting=resting,Accepted=accept,Negotiating=negotiate,Declined=decline,PreferredActivities=activities,PreferredPlaces=places};
        static readonly ResidentVoiceProfile[] Voices={
            Voice("CH_02","김진우",771,
                "민혁아, 무슨 일이야? 일단 네 생각부터 들어 볼까?",
                "잠깐. 이것만 끝내고. 그렇게 급한 얘기야?",
                "책 얘기? 아니면 책 읽는 내 반응이 궁금한 거야?",
                "쉬는 중인데. 네 얘기 정도는 들어 줄게.",
                "좋아. 근데 왜 그걸 골랐어? 그게 더 궁금한데.",
                "지금 꼭 해야 돼? 조금 뒤면 나도 괜찮은데.",
                "이번엔 패스. 거절도 선택지긴 하잖아?",
                new[]{"Read","Talk","Game"},new[]{"R_LIBRARY","R_DINING"}),
            Voice("CH_03","한서윤",794,
                "무슨 일이에요? 잠깐 잡담하는 건 괜찮아요.",
                "이것만 마저 할게요. 급한 일이면 먼저 말해 줘요.",
                "읽던 데만 표시할게요. 무슨 얘기예요?",
                "지금은 좀 쉬려고요. 일 얘기 말고 다른 얘기도 좋고요.",
                "좋아요. 대신 혼자 다 하지 말고 나눠서 해요.",
                "지금 하던 일이랑 겹쳐요. 조금 뒤로 옮길 수 있어요?",
                "이번엔 어려워요. 저도 제 몫부터 끝내야죠.",
                new[]{"Organize","Read","Review","Help"},new[]{"R_LIBRARY","R_HALL","R_DINING"}),
            Voice("CH_04","차도윤",817,
                "민혁 씨, 무슨 일인가요? 천천히 말씀하세요.",
                "이것만 정리하겠습니다. 손을 놓기엔 조금 애매해서요.",
                "민혁 씨, 이 부분만 마저 읽어도 될까요?",
                "잠깐 쉬던 참입니다. 조용히 이야기 나누는 건 좋겠네요.",
                "좋습니다. 다룰 물건이 있다면 허락부터 구하지요.",
                "조금 뒤는 어떠신가요? 서두르면 아쉬움이 남아서요.",
                "이번에는 사양하겠습니다. 제 몫까지 정해 두지는 마세요.",
                new[]{"Restore","Repair","Tea","Examine"},new[]{"R_EXHIBIT","R_WORK","R_DINING"}),
            Voice("CH_05","백이현",840,
                "무슨 이야기인가요? 좋은 제안이면 저도 빠질 수 없죠.",
                "하던 일만 정리하죠. 무슨 얘기인지는 먼저 듣겠습니다.",
                "좋습니다. 책은 잠깐 덮죠. 어떤 이야기인가요?",
                "쉬는 시간까지 회의로 채우진 맙시다. 가벼운 얘기라면 좋고요.",
                "좋습니다. 서로 부담 없는 선에서 해 보죠.",
                "취지는 좋아요. 시간만 다시 맞추면 어떨까요?",
                "지금 조건으로는 어렵겠네요. 제 몫까지 늘리지는 말아 주세요.",
                new[]{"Talk","Meeting","Rest","Swim"},new[]{"R_HALL","R_DINING","R_POOL"}),
            Voice("CH_06","권태겸",863,
                "용건이 뭡니까? 듣고 있습니다.",
                "하던 것부터 끝내겠습니다. 조금만 기다려 주세요.",
                "읽던 항목만 보겠습니다. 무슨 일입니까?",
                "쉬는 중입니다. 급한 용건이면 말씀하세요.",
                "가능합니다. 시간은 지켜 주세요.",
                "지금은 시간이 겹칩니다. 조금 뒤로 잡죠.",
                "이번에는 어렵습니다. 무리해서 약속하진 않겠습니다.",
                new[]{"Organize","Inventory","Review","Trade"},new[]{"R_DINING","R_WORK"}),
            Voice("CH_07","유시온",886,
                "오, 민혁! 무슨 썰인데? 일단 말해 봐.",
                "야, 잠깐만! 이것만 끝내고. 나도 듣고는 있다.",
                "잠깐, 여기서 끊으면 좀 아까운데. 이 부분만 읽고!",
                "쉬는 김에 얘기하자. 혼자 멍때리는 것도 슬슬 질리네.",
                "좋지! 같이 가자. 시간만 안 꼬이면 돼.",
                "지금은 겹치네. 조금 있다 가면 안 되냐?",
                "이번엔 빠질게. 매번 풀참은 나도 무리다.",
                new[]{"Read","Talk","Game","Meal"},new[]{"R_LIBRARY","R_DINING","R_ARCADE"}),
            Voice("CH_08","서라온",909,
                "왜, 민혁. 또 다 같이 구호 외치자는 건 아니지?",
                "이것만 끝내고. 급하면 급하다고 해.",
                "읽던 데만 표시할게. 그래서 무슨 얘긴데?",
                "아무것도 안 하는 것도 일정이거든. 얘기는 해 봐.",
                "그래, 끼워 줘. 단체 구호만 빼고.",
                "지금 말고 좀 뒤에. 하던 건 끝내야지.",
                "이번엔 패스. 거절했다고 서사 만들진 말고.",
                new[]{"Game","RhythmGame","Rest","Walk"},new[]{"R_ARCADE","R_DINING","R_CE"}),
            Voice("CH_09","문재하",932,
                "민혁아, 무슨 얘기야? 이번엔 진짜 듣고 있어.",
                "민혁아, 잠깐만. 이것까지 미루면 나도 좀 양심 없잖아.",
                "민혁아, 읽던 데만 표시하고. 그래서 무슨 일인데?",
                "민혁아, 오 분만 더 쉬자. 쉬는 게 제일 잘 맞는 거 같아.",
                "좋아, 민혁아. 내가 할 건 딱 정해 줘.",
                "조금 뒤에 하면 안 될까? 시간만 다시 맞추자.",
                "이번엔 어려워. 된다고 해 놓고 미루는 건 좀 그렇잖아.",
                new[]{"Talk","Rest","Read","Rehearse"},new[]{"R_HALL","R_DINING"}),
            Voice("CH_10","강준서",955,
                "민혁 씨, 무슨 일이에요? 천천히 말해요.",
                "이것만 안전하게 마무리할게요. 잠깐이면 돼요.",
                "읽던 건 조금 뒤에 봐도 되겠네요. 무슨 얘기예요?",
                "쉬던 참이에요. 같이 좀 쉬어요. 얘기는 들을게요.",
                "좋아요. 무리되면 중간에 말해 줘요.",
                "하던 일을 마쳐야 해서요. 조금 뒤에 같이 갈까요?",
                "지금은 어렵겠어요. 괜히 약속했다가 못 가면 더 미안하니까요.",
                new[]{"Meal","Cook","WaterPlants","Help","Rest"},new[]{"R_DINING","R_GREEN"}),
            Voice("CH_11","윤해린",978,
                "뭔데? 일단 말해 봐. 모르면 같이 보면 되지.",
                "잠깐. 지금 손 떼면 일 두 번 한다. 이것만 끝내고.",
                "읽는 중이야. 궁금한 데 있으면 짚어 봐.",
                "쉬는 중. 질문은 받아. 만능 해결사는 아니고.",
                "해 보자. 대신 안 되는 건 안 된다고 할 거야.",
                "지금은 안 돼. 하던 거 끝나고면 가능.",
                "그 조건으론 못 해. 일단 된다고 지르는 게 더 문제지.",
                new[]{"Repair","Examine","Review","Help"},new[]{"R_WORK"}),
            Voice("CH_12","오수아",1001,
                "민혁 씨, 무슨 얘기예요? 그런 표정이면 괜히 궁금하잖아요.",
                "잠깐만요. 이것만 정리하고 얘기해요.",
                "읽던 데만 표시할게요. 무슨 이야기예요?",
                "오늘은 좀 조용히 쉬고 싶어요. 얘기는 작게 해도 되죠?",
                "좋아요. 불편한 건 그때그때 말해도 되죠?",
                "조금 뒤는 어때요? 지금 당장은 어려워서요.",
                "이번엔 쉴게요. 충전도 해야죠.",
                new[]{"Talk","Rest","Organize"},new[]{"R_DINING","R_HALL"}),
            Voice("CH_13","정세나",1024,
                "왔냐. 한 판 할래? 아니면 할 말 있어?",
                "잠깐. 하던 건 끝내야지. 중간 탈주는 좀.",
                "비교하는 중이야. 이상한 데 있으면 말해.",
                "쉬는 중. 쉬는 것도 실력이라며.",
                "좋아. 같이 해 보자. 안 되면 다시 맞추면 되고.",
                "지금 건 끝내야 돼. 그다음으로 잡자.",
                "이번엔 안 해. 억지로 끌어들이진 말고.",
                new[]{"Game","Practice","Review"},new[]{"R_ARCADE"}),
            Voice("CH_14","차은결",1047,
                "말씀하세요. 급한 얘기가 아니어도 괜찮습니다.",
                "하던 것만 정리하겠습니다. 잠시 기다려 주세요.",
                "읽던 곳을 표시할게요. 천천히 말씀하세요.",
                "조용히 쉬고 있었습니다. 함께 이야기하셔도 좋고요.",
                "함께하겠습니다. 돌아올 시간도 정해 두지요.",
                "약속은 접지 말고, 시간만 조금 옮길까요?",
                "이번에는 사양하겠습니다. 마음은 잘 받았습니다.",
                new[]{"Walk","Tea","Craft","Rest"},new[]{"R_GREEN","R_DINING","R_HALL"}),
            Voice("CH_15","남가온",1070,
                "무슨 얘기예요? 직접 보신 일인지부터 알려 주세요.",
                "하던 확인만 마칠게요. 급한 일이에요?",
                "읽던 내용만 정리할게요. 어떤 얘기인가요?",
                "쉬고 있어요. 오늘은 그냥 얘기부터 듣죠.",
                "같이 확인하죠. 추측은 추측이라고 적어 두고요.",
                "시간이 겹치네요. 조금 뒤로 바꿀 수 있어요?",
                "지금 제안에는 동의하기 어려워요. 조건을 바꾸면 다시 듣죠.",
                new[]{"Review","Examine","Map","Read"},new[]{"R_HALL","R_LIBRARY"}),
            Voice("CH_16","신채령",1093,
                "왜. 할 말 있으면 해. 눈치만 보지 말고.",
                "이것부터 끝낼게. 내 일정까지 대신 짜진 마.",
                "읽는 중이야. 취향 평가만 아니면 들어 줄게.",
                "혼자 좀 쉬려고. 할 말 있으면 짧게 해.",
                "좋아. 대신 일은 나눠. 내 몫은 내가 정하고.",
                "지금은 아니야. 시간부터 다시 맞춰.",
                "안 갈래. 이유까지 발표해야 되는 건 아니지?",
                new[]{"Organize","Examine","Rest","Craft"},new[]{"R_EXHIBIT"}),
            Voice("CH_17","송예담",1116,
                "오늘의 질문! 심심할 때 뭐 하냐? 답변 받습니다.",
                "잠깐! 진행 중인 것부터 끝내자. 갑자기 일정 바꾸기 없기.",
                "읽던 데 표시 끝! 이제 네 얘기 들을 차례.",
                "쉬는 시간은 못 참지. 얘기는 해 봐!",
                "참가할게! 시작 시간은 같이 정하는 거다?",
                "일정 변경 오케이. 대신 바뀐 시간은 꼭 알려 줘.",
                "이번엔 안 할래. …다음 일정은 미리 알려 주고.",
                new[]{"Craft","Game","Quiz","Talk"},new[]{"R_WORK","R_HALL","R_ARCADE"}),
            Voice("CH_18","임민서",1139,
                "네. 듣고 있습니다.",
                "하던 것부터 끝내겠습니다. 다음 건 나눠서 합시다.",
                "읽고 있습니다. 용건은 듣겠습니다.",
                "좀 쉬고 있습니다. 급한 일입니까?",
                "가겠습니다. 일은 같이 나눕시다.",
                "지금은 겹칩니다. 끝나고 합시다.",
                "이번에는 어렵습니다. 제 몫은 여기까지 하겠습니다.",
                new[]{"Walk","Puzzle","Help","Inventory","Rest"},new[]{"R_GREEN","R_DINING"})
        };
        static readonly Dictionary<string,ResidentVoiceProfile> ByActor=Voices.ToDictionary(v=>v.ActorId,StringComparer.Ordinal);
        static readonly HashSet<string> WorkActivities=new HashSet<string>{"Cook","Repair","Restore","WaterPlants","Organize","Inventory","Help","Carry","Clean","ACT_COOK","ACT_REPAIR","ACT_ORGANIZE"};
        static readonly HashSet<string> ReadingActivities=new HashSet<string>{"Read","Review","Map","Examine","ACT_READ"};
        static readonly HashSet<string> RestActivities=new HashSet<string>{"Rest","Tea","ACT_REST"};
        public ResidentVoiceProfile[] Profiles()=>Voices.Select(v=>v.Copy()).ToArray();
        static ResidentVoiceProfile Get(string actor)=>ByActor.TryGetValue(actor??"",out var p)?p:throw new ArgumentException("Unknown NPC voice");
        public string Greeting(string actorId,string currentActivity,IActorKnowledgeQuery ownQuery)
        {
            var voice=Get(actorId);if(ownQuery==null||ownQuery.OwnerId!=actorId)throw new ArgumentException("Greeting requires the speaker's owner-bound knowledge");
            // No record text or count is consulted: knowing a private fact is not consent to disclose it.
            if(WorkActivities.Contains(currentActivity??"")||(currentActivity??"").StartsWith("MarkSurface:",StringComparison.Ordinal))return voice.BusyGreeting;
            if(ReadingActivities.Contains(currentActivity??""))return voice.ReadingGreeting;
            if(RestActivities.Contains(currentActivity??""))return voice.RestGreeting;
            return voice.Greeting;
        }
        public InvitationDecision EvaluateInvitation(string actorId,string activity,string invitationPlace,SocialExperience[] ownRelevantExperience,bool scheduleConflict,InvitationPolicy policy=null,string inviterId="CH_01")
        {
            var voice=Get(actorId);var p=policy??new InvitationPolicy();Validate(p);
            var own=(ownRelevantExperience??Array.Empty<SocialExperience>()).Where(e=>e!=null&&e.Owner==actorId&&e.Other==inviterId&&!string.IsNullOrWhiteSpace(e.Id)).GroupBy(e=>e.Kind+"|"+e.Reason).Select(g=>g.First()).ToArray();
            var decision=new InvitationDecision{ActorId=actorId,SourceParagraph=voice.SourceParagraph,SuggestedActivity=activity??"",SuggestedPlace=invitationPlace??""};
            if(string.IsNullOrWhiteSpace(activity)||string.IsNullOrWhiteSpace(invitationPlace)||string.IsNullOrWhiteSpace(inviterId)||inviterId==actorId){decision.Outcome="Declined";decision.Reason="누가 어디서 무엇을 할지 먼저 정해 주세요.";return decision;}
            if(scheduleConflict){decision.Outcome="CounterOffer";decision.Reason=voice.Negotiating;decision.SuggestedDelayTicks=p.SuggestedDelayTicks;return decision;}
            var positive=own.Where(e=>new[]{"PromiseKept","FairShare","HelpReceived"}.Contains(e.Kind)).ToArray();
            var negative=own.Where(e=>new[]{"PromiseBroken","BrokenPromise","BoundaryViolation","UnfairShare"}.Contains(e.Kind)).ToArray();
            int trust=Math.Min(p.MaximumExperienceInfluence,positive.Length)*p.TrustedExperienceWeight-Math.Min(p.MaximumExperienceInfluence,negative.Length)*p.BoundaryWeight;
            int willingness=p.BaseWillingness+(voice.PreferredActivities.Contains(activity)?p.PreferredActivityWeight:0)+(voice.PreferredPlaces.Contains(invitationPlace)?p.PreferredPlaceWeight:0)+trust;
            decision.BasisExperienceIds=positive.Concat(negative).Select(e=>e.Id).ToArray();
            if(willingness>=p.AcceptThreshold){decision.Accepted=true;decision.Outcome="Accepted";decision.Reason=voice.Accepted;return decision;}
            if(willingness<=p.DeclineThreshold){decision.Outcome="Declined";decision.Reason=voice.Declined;return decision;}
            decision.Outcome="CounterOffer";decision.Reason=voice.Negotiating;decision.SuggestedActivity=voice.PreferredActivities[0];decision.SuggestedDelayTicks=p.SuggestedDelayTicks;return decision;
        }
        static void Validate(InvitationPolicy p)
        {
            if(p.PreferredActivityWeight<0||p.PreferredPlaceWeight<0||p.TrustedExperienceWeight<0||p.BoundaryWeight<0||p.MaximumExperienceInfluence<0||p.AcceptThreshold<=p.DeclineThreshold||p.SuggestedDelayTicks<60)throw new ArgumentException("Invalid invitation proposal policy");
        }
    }
}

