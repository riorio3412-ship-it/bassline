using System;
namespace BASSLINE.Core
{
    // Authored willingness and dialogue, never evidence of present custody or permission.
    [Serializable] public sealed class ItemLoanTerms
    {
        public string Lender="",ItemId="",ShortName="",RequestLabel="";
        public string Offer="",Reminder="",Unavailable="",ReturnInstruction="",Declined="";
        public int PermissionTicks=18000;
        public bool Complete=>!string.IsNullOrWhiteSpace(Lender)&&!string.IsNullOrWhiteSpace(ItemId)&&
            !string.IsNullOrWhiteSpace(ShortName)&&!string.IsNullOrWhiteSpace(RequestLabel)&&
            !string.IsNullOrWhiteSpace(Offer)&&!string.IsNullOrWhiteSpace(Reminder)&&
            !string.IsNullOrWhiteSpace(Unavailable)&&!string.IsNullOrWhiteSpace(ReturnInstruction)&&
            !string.IsNullOrWhiteSpace(Declined)&&PermissionTicks>0;
        public static ItemLoanTerms Pen()=>new ItemLoanTerms{
            Lender="CH_06",ItemId="M_TAEGYEOM_PEN",ShortName="펜",RequestLabel="펜 잠깐 빌려도 될까?",
            Offer="잠깐 쓰셔도 됩니다.\n뚜껑 옆에 흠집이 있어요. 다 쓰면 돌려주세요.",
            Reminder="아까 빌려드린 펜을 먼저 찾아 주시겠어요? 마지막으로 어디에 두셨는지부터 생각해 보면 좋겠습니다.",
            Unavailable="지금은 제 손에 펜이 없네요. 갖고 있는 걸 확인한 다음에 빌려드릴게요.",
            ReturnInstruction="제게 직접 건네주세요.\n탁자에 놓으면 다른 물건과 섞일 수 있어요.\n저를 못 찾으시면 일단 가지고 계셔도 됩니다.",
            Declined="알겠습니다. 필요하시면 그때 다시 물어봐 주세요."};
    }
}
