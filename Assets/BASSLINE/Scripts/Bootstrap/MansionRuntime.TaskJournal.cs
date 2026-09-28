using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerTaskJournalPort
    {
        public TaskJournalView ReadTaskJournal()=>BuildTaskJournal(true);
        public TaskJournalEntry ReadPinnedTask()=>BuildTaskJournal(false).Pinned;
        TaskJournalView BuildTaskJournal(bool includeLocations)
        {
            var rows=new List<TaskJournalEntry>();var own=Knowledge.For("CH_01");var records=includeLocations?own.Records():Array.Empty<KnownRecord>();
            string held=World.Resident("CH_01").HeldObject;
            foreach(var r in Requests){
                var d=everydayRequests.FirstOrDefault(x=>x.Id==r.Id);var offer=own.Find(r.OfferRecordId);if(d==null||offer==null)continue;
                bool done=r.Status=="Completed",accepted=r.Decision=="Accept"&&!done;
                // Interrupted is world bookkeeping. Until personally informed, the player remembers
                // the agreement; a hidden death or departure must not remove/change their HUD task.
                string status=done?"마침":accepted?"맡은 부탁":r.Decision=="Cancel"?"그만둔 부탁":r.Decision=="Defer"?"나중에":r.Decision=="Decline"?"거절함":"답하기 전";
                var entry=new TaskJournalEntry{Id="REQUEST:"+r.Id,Title=d.Title,ActorId=r.Requester,Status=status,CanPin=accepted,Completed=done,StartedTick=r.DecisionTick>=0?r.DecisionTick:r.OfferedTick,
                    SourceRecordId=offer.Id,SourceText=offer.Text,
                    Instruction=done?"부탁받은 물건을 직접 건넸어요.":accepted?(held==r.ItemId?NameOf(r.Requester)+"에게 직접 건네주세요.":KoreanText.AsObject(NameOf(r.ItemId))+" 찾아 손에 들어 주세요."):"다시 이야기하고 싶다면 가까이에서 말을 걸어 주세요."};
                if(includeLocations)AddKnownPlaces(entry,r.ItemId,r.Requester,records);rows.Add(entry);
            }
            foreach(var loan in itemExchange.Loans){
                var offer=own.Find(loan.OfferRecordId);if(offer==null)continue;bool done=loan.Status=="Returned";
                var entry=new TaskJournalEntry{Id="LOAN:"+loan.Id,Title=NameOf(loan.Lender)+"에게 "+KoreanText.AsObject(NameOf(loan.ItemId))+" 돌려주기",ActorId=loan.Lender,Status=done?"돌려줌":"빌린 물건",CanPin=!done,Completed=done,StartedTick=loan.StartedTick,
                    SourceRecordId=offer.Id,SourceText=offer.Text,Instruction=done?"빌려준 사람이 직접 받았어요.":held==loan.ItemId?"다 썼다면 빌려준 사람에게 직접 건네주세요.":"빌린 물건을 다시 손에 들고 돌려주세요."};
                if(includeLocations)AddKnownPlaces(entry,loan.ItemId,loan.Lender,records);rows.Add(entry);
            }
            var settings=itemExchange.Journal??new TaskJournalSettings();
            var entries=rows.OrderByDescending(r=>r.CanPin).ThenBy(r=>r.Completed).ThenBy(r=>r.StartedTick).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
            var pinned=settings.Hidden?null:entries.FirstOrDefault(r=>r.CanPin&&r.Id==settings.PinnedId)??entries.FirstOrDefault(r=>r.CanPin);
            if(pinned!=null)pinned.Pinned=true;
            return new TaskJournalView{Entries=entries,Pinned=pinned,Hidden=settings.Hidden};
        }
        void AddKnownPlaces(TaskJournalEntry entry,string item,string actor,KnownRecord[] records)
        {
            KnownRecord Seen(string id)=>records.Where(r=>r.Direct&&r.IdentityConfirmed&&r.SubjectId==id&&r.Predicate=="AtPlace"&&r.ReceivedTick<=World.Tick).OrderByDescending(r=>r.ToTick).ThenByDescending(r=>r.ReceivedTick).FirstOrDefault();
            var itemSeen=Seen(item);var actorSeen=Seen(actor);
            if(itemSeen!=null){entry.LastItemPlace=itemSeen.PlaceId;entry.ItemSeenTick=itemSeen.ToTick-1;}
            if(actorSeen!=null){entry.LastActorPlace=actorSeen.PlaceId;entry.ActorSeenTick=actorSeen.ToTick-1;}
        }
        public string PinTask(string id)
        {
            if(!ReadTaskJournal().Entries.Any(r=>r.Id==id&&r.CanPin))return "지금 표시할 수 있는 할 일을 골라 주세요.";
            if(itemExchange.Journal==null)itemExchange.Journal=new TaskJournalSettings();
            itemExchange.Journal.PinnedId=id;itemExchange.Journal.Hidden=false;return "이 할 일 하나를 화면에 표시해요.";
        }
        public void HidePinnedTask(){if(itemExchange.Journal==null)itemExchange.Journal=new TaskJournalSettings();itemExchange.Journal.Hidden=true;}
        public void ShowPinnedTask(){if(itemExchange.Journal==null)itemExchange.Journal=new TaskJournalSettings();itemExchange.Journal.Hidden=false;}
        void ValidateTaskJournal(ItemExchangeSnapshot exchange)
        {
            var settings=exchange.Journal;if(settings==null||settings.PinnedId==null)throw new InvalidDataException("할 일 표시 설정이 없습니다.");
            if(settings.PinnedId!=""&&!exchange.Loans.Any(l=>settings.PinnedId=="LOAN:"+l.Id)&&!(exchange.Requests??Array.Empty<EverydayRequestState>()).Any(r=>settings.PinnedId=="REQUEST:"+r.Id))throw new InvalidDataException("기억하지 않은 할 일이 고정되어 있습니다.");
        }
    }
}
