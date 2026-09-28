using System;
namespace BASSLINE.UI
{
    // Presentation only. Never alter command outcomes or look up authority data to explain a failure.
    public static class PlayerUiText
    {
        public static string Status(string value)
        {
            if(string.IsNullOrEmpty(value))return "";
            switch(value)
            {
                case "Pending":return "잠깐만요. 확인 중이에요.";
                case "Accepted":return "좋아요. 그렇게 하기로 했어요.";
                case "Committed":case "Applied":return "반영했어요.";
                case "Unavailable":return "지금은 할 수 없어요.";
                case "WorldPaused":return "노트를 닫고 이어서 해요.";
                case "Dialogue":return "";
                case "Queued":return "발언할 차례를 기다리고 있어요.";
                case "Delivered":case "Received":return "내용을 전달했어요.";
                case "Recorded":return "선택을 기록했어요.";
                case "Observed":return "노트에 남겼어요.";
                case "Focused":return "여기서 잠깐. 근거를 살펴봐요.";
                case "Retired":return "이 주장은 여기까지 정리했어요.";
                case "Requested":return "증언을 요청했어요. 답을 들어 봐요.";
                case "Opened":return "이제 투표할 수 있어요.";
                case "VotesLocked":return "투표가 끝났어요. 결과를 확인해요.";
                case "VoteAlreadyLocked":return "이미 확정한 표예요.";
                case "RevoteOpened":return "동점이에요. 남은 후보에게 다시 투표해요.";
                case "VerdictCommitted":case "VerdictTargetLocked":return "판정이 확정됐어요.";
                case "Truth":case "Advanced":return "사건의 전말을 이어서 살펴봐요.";
                case "Evaluated":return "이번 사건을 정리했어요.";
                case "RewardApplied":return "성장 기록을 저장했어요.";
                case "SettlementApplied":return "정산을 마쳤어요.";
                case "NextChapterReady":return "다음 사건으로 넘어가요.";
                case "LoopReady":return "다음 회차를 시작해요.";
                case "InputStale":return "그동안 기록이 바뀌었어요. 다시 골라 주세요.";
                case "IncompleteBallots":return "아직 투표가 끝나지 않았어요.";
                case "GatheringIncomplete":return "아직 도착하지 않은 사람이 있어요.";
                case "Cancelled":return "여기서 멈췄어요. 확인한 내용은 남아 있어요.";
                case "Declined":return "이번에는 어렵다고 해요.";
                case "Duplicate":case "AlreadyApplied":return "이미 반영된 내용이에요.";
            }
            if(value.StartsWith("K_HYP_",StringComparison.Ordinal)||value.StartsWith("M_HYP_",StringComparison.Ordinal))return "가설을 적어 뒀어요.";
            if(value.StartsWith("K_REC_",StringComparison.Ordinal)||value.StartsWith("M_REC_",StringComparison.Ordinal))return "기록을 전달했어요.";
            // Existing authored Korean feedback may include useful numbers, dates and punctuation.
            // Pure technical IDs/reason codes belong in the diagnostic log, never on a player button.
            foreach(char c in value)if(c>='가'&&c<='힣')return value;
            return "처리하지 못했어요. 현재 화면에서 다시 확인해 주세요.";
        }
        public static string Appointment(string state)
        {
            switch(state)
            {
                case "Agreed":return "서로 약속했어요.";
                case "Met":return "만나서 확인했어요.";
                case "WindowEnded":return "약속 시간이 지났어요.";
                case "Proposed":return "아직 약속이 확정되지 않았어요.";
                case "Declined":return "이번 제안은 약속으로 정하지 않았어요.";
                default:return "일정을 확인 중이에요.";
            }
        }
        public static string Review(string state)
        {
            switch(state)
            {
                case "Asserted":return "아직 검토하지 않은 주장";
                case "Contradicted":return "자료와 맞지 않음";
                case "SupportedWithinScope":return "확인한 범위에서 뒷받침됨";
                case "UnsupportedScope":return "이 범위까지는 말할 수 없음";
                case "NeedPremise":return "더 확인할 근거가 필요함";
                default:return "검토 상태 확인 중";
            }
        }
        public static string Transition(string state)=>state=="Loop"?"다음 회차로 이어집니다.":state=="NextChapter"?"남은 사람들과 다음 사건으로 이어집니다.":"";
    }
}
