using System;
using System.Linq;
using System.Collections.Generic;

namespace BASSLINE.World.Mansion
{
    public sealed class ChapterRuleDefinition
    {
        public string Id,Name,Type,Summary;public int MinimumPeople,MinimumChapter=2;public bool Major,Carry;
    }
    [Serializable] public sealed class ChapterRuleInstance
    {
        public string Id,InstanceId;public int SourceChapter,CarryCount;
        public string[] ReceivedBy=Array.Empty<string>(),ConsequenceRefs=Array.Empty<string>();
        public ChapterRuleInstance Copy()=>new ChapterRuleInstance{Id=Id,InstanceId=InstanceId,SourceChapter=SourceChapter,CarryCount=CarryCount,ReceivedBy=(string[])ReceivedBy.Clone(),ConsequenceRefs=(string[])ConsequenceRefs.Clone()};
    }
    [Serializable] public sealed class ChapterRuleHistory
    {
        public string Id,InstanceId;public int Loop,Chapter;
        public ChapterRuleHistory Copy()=>(ChapterRuleHistory)MemberwiseClone();
    }
    [Serializable] public sealed class ChapterRulePlan
    {
        public int Version=1,Loop=1,Chapter=1,StartN=18;public uint RandomState=32771;
        public long AnnouncedTick=-1;public string[] Ended=Array.Empty<string>();
        public ChapterRuleInstance[] Active=Array.Empty<ChapterRuleInstance>();
        public ChapterRuleHistory[] History=Array.Empty<ChapterRuleHistory>();
        public ChapterRulePlan Copy()=>new ChapterRulePlan{Version=Version,Loop=Loop,Chapter=Chapter,StartN=StartN,RandomState=RandomState,AnnouncedTick=AnnouncedTick,Ended=(string[])Ended.Clone(),Active=Active.Select(a=>a.Copy()).ToArray(),History=History.Select(h=>h.Copy()).ToArray()};
    }
    public static class ChapterRules
    {
        static ChapterRuleDefinition D(int id,string name,string type,int n,bool major,bool carry,string summary,int chapter=2)=>new ChapterRuleDefinition{Id="CH"+id.ToString("00"),Name=name,Type=type,MinimumPeople=n,Major=major,Carry=carry,Summary=summary,MinimumChapter=chapter};
        static readonly ChapterRuleDefinition[] catalog={
            D(1,"공개 표결","판결",5,false,true,"모두 투표를 확정한 뒤, 누가 누구에게 투표했는지 공개합니다."),
            D(2,"양익 생활","환경",8,true,true,"두 생활익을 나누어 사용하며 수사 공표 후 통로를 개방합니다."),
            D(3,"정기 소등","환경",7,false,false,"공표된 두 시각에 60초 동안 소등합니다. 비상등은 유지합니다."),
            D(4,"최초 진술 봉인","정보",5,false,true,"처음 한 진술을 보존합니다. 정정은 원문을 지우지 않고 덧붙입니다."),
            D(5,"두 계약 공개","정보",4,false,false,"두 계약자의 보상 문장만 한 번 공개합니다."),
            D(6,"과거의 봉투","관계",6,true,false,"당사자에게 실제 전달된 봉투를 읽어야 내용을 알 수 있습니다."),
            D(7,"제한 대여","관계",6,false,true,"공용 장비의 대여 기한은 20분입니다. 반납은 직접 해야 합니다."),
            D(8,"공동 점검","환경",5,false,true,"시설 점검을 두 사람이 함께하도록 권장합니다. 불참에 처벌은 없습니다."),
            D(9,"정산의 기한","계약",8,true,false,"해당 계약자에게 이번 정산의 보상 기한을 개별 통지합니다.",3),
            D(10,"공개 질의","절차",5,false,true,"수사 말미에 질의 모임을 엽니다. 실제 참석자만 발언을 듣습니다."),
            D(11,"공용품 교환회","관계",6,false,false,"동의한 물건만 실제로 건네어 대여하거나 교환합니다."),
            D(12,"이동식 전시","환경",6,true,false,"기존 전시품을 공표한 작업창 안에서 직접 운반합니다."),
            D(13,"교대 열쇠","관계",6,false,true,"합의한 담당자가 공용실 열쇠를 만나서 인계합니다."),
            D(14,"분산 열람","정보",7,true,true,"공용 자료의 열람 창구를 두 곳으로 나누어 운영합니다."),
            D(15,"공동 식탁","관계",6,false,true,"공동 식사의 권장 자리를 제안합니다. 다른 자리도 선택할 수 있습니다."),
            D(16,"공개 정비 시간","환경",6,false,true,"비필수 시설의 정비 시간과 대체 시설을 미리 안내합니다."),
            D(17,"자료 보관 담당","정보",6,false,true,"공용 문서 담당자가 실제 분류와 인계를 수행합니다."),
            D(18,"반론 우선권","절차",5,false,true,"새 비난을 받은 당사자의 관련 반론 요청을 한 번 우선 검토합니다."),
            D(19,"출처 대조 심리","절차",5,false,true,"전해 들은 말은 출처와 확인 범위부터 검토합니다. 모르는 것은 남겨 둡니다."),
            D(20,"시간차 공지","정보",7,true,false,"선택형 행사 공지 한 건을 같은 원문으로 두 곳에 시차 전달합니다.")
        };
        public static ChapterRuleDefinition[] Catalog()=>catalog.Select(c=>D(int.Parse(c.Id.Substring(2)),c.Name,c.Type,c.MinimumPeople,c.Major,c.Carry,c.Summary,c.MinimumChapter)).ToArray();
        public static ChapterRuleDefinition Find(string id){var c=catalog.FirstOrDefault(d=>d.Id==id);return c==null?null:D(int.Parse(c.Id.Substring(2)),c.Name,c.Type,c.MinimumPeople,c.Major,c.Carry,c.Summary,c.MinimumChapter);}
        public static bool Compatible(string a,string b)
        {
            var x=Find(a);var y=Find(b);if(x==null||y==null||a==b||x.Type==y.Type||x.Major&&y.Major)return false;
            string pair=string.CompareOrdinal(a,b)<0?a+"/"+b:b+"/"+a;
            return pair!="CH02/CH03"&&pair!="CH05/CH06"&&pair!="CH06/CH09";
        }
        static uint Draw(ref uint seed){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed;}
        // The caller supplies ONLY rules whose real effects/resources and safety alternatives are available.
        // Hidden plans, victims and player correctness are deliberately absent from this API.
        public static ChapterRulePlan Next(ChapterRulePlan previous,int loop,int chapter,int startN,int contractors,string[] available,uint seed)
        {
            if(loop<1||chapter<1||startN<4||startN>18||contractors<0)throw new ArgumentException("Invalid chapter boundary");
            if(previous!=null)Validate(previous);
            bool newLoop=previous==null||previous.Loop!=loop;
            if(previous!=null&&newLoop&&loop!=previous.Loop+1)throw new ArgumentException("Loops must advance sequentially");
            if(previous!=null&&!newLoop&&chapter!=previous.Chapter+1)throw new ArgumentException("Rules change only at a chapter boundary");
            if(newLoop&&chapter!=1)throw new ArgumentException("A new loop starts at chapter one");
            var state=new ChapterRulePlan{Loop=loop,Chapter=chapter,StartN=startN,RandomState=seed==0?32771:seed,
                History=previous?.History.Select(h=>h.Copy()).ToArray()??Array.Empty<ChapterRuleHistory>()};
            if(chapter==1)return state;
            var usable=new HashSet<string>(available??Array.Empty<string>());
            bool Eligible(ChapterRuleDefinition d)=>usable.Contains(d.Id)&&startN>=d.MinimumPeople&&chapter>=d.MinimumChapter&&(d.Id!="CH05"||contractors>=2);
            var chosen=new List<ChapterRuleInstance>();
            if(chapter>=3&&!newLoop){
                var carries=previous.Active.Where(a=>Find(a.Id).Carry&&a.CarryCount==0&&Eligible(Find(a.Id))).OrderBy(a=>a.Id,StringComparer.Ordinal).ToArray();
                if(carries.Length>0){var item=carries[Draw(ref state.RandomState)%(uint)carries.Length].Copy();item.CarryCount++;item.ReceivedBy=Array.Empty<string>();chosen.Add(item);}
            }
            int slots=chapter==2?1:2;
            var candidates=catalog.Where(Eligible).Where(d=>!chosen.Any(a=>a.Id==d.Id))
                .Where(d=>!state.History.Any(h=>h.Loop==loop&&h.Id==d.Id&&h.Chapter>=chapter-1)).OrderBy(d=>d.Id,StringComparer.Ordinal).ToList();
            while(chosen.Count<slots){
                var possible=candidates.Where(d=>chosen.All(a=>Compatible(a.Id,d.Id))).ToArray();if(possible.Length==0)break;
                var selected=possible[Draw(ref state.RandomState)%(uint)possible.Length];
                chosen.Add(new ChapterRuleInstance{Id=selected.Id,InstanceId="L"+loop+"_C"+chapter+"_"+selected.Id,SourceChapter=chapter});candidates.Remove(selected);
            }
            state.Active=chosen.ToArray();state.Ended=previous.Active.Where(a=>!chosen.Any(c=>c.InstanceId==a.InstanceId)).Select(a=>a.Id).ToArray();
            state.History=state.History.Concat(chosen.Select(a=>new ChapterRuleHistory{Id=a.Id,InstanceId=a.InstanceId,Loop=loop,Chapter=chapter})).ToArray();Validate(state);return state;
        }
        public static void Validate(ChapterRulePlan p)
        {
            if(p==null||p.Version!=1||p.Loop<1||p.Chapter<1||p.StartN<4||p.StartN>18||p.RandomState==0||p.Active==null||p.History==null||p.Ended==null||p.AnnouncedTick< -1)throw new ArgumentException("Invalid saved chapter rules");
            if(p.Active.Any(a=>a==null)||p.Active.Length>(p.Chapter==1?0:p.Chapter==2?1:2)||p.Active.Select(a=>a.Id).Distinct().Count()!=p.Active.Length)throw new ArgumentException("Invalid rule count");
            foreach(var a in p.Active){var d=Find(a.Id);if(d==null||a.ReceivedBy==null||a.ConsequenceRefs==null||string.IsNullOrEmpty(a.InstanceId)||p.StartN<d.MinimumPeople||p.Chapter<d.MinimumChapter||a.CarryCount<0||a.CarryCount>1||a.SourceChapter+a.CarryCount!=p.Chapter||a.CarryCount>0&&!d.Carry||a.ReceivedBy.Distinct().Count()!=a.ReceivedBy.Length)throw new ArgumentException("Invalid rule instance");}
            if(p.Active.Length==2&&!Compatible(p.Active[0].Id,p.Active[1].Id))throw new ArgumentException("Incompatible rule pair");
            if(p.Ended.Any(id=>Find(id)==null)||p.History.Any(h=>h==null||Find(h.Id)==null||h.Chapter<2||h.Loop<1||h.Loop>p.Loop||h.Loop==p.Loop&&h.Chapter>p.Chapter))throw new ArgumentException("Invalid rule history");
        }
        public static string Summary(ChapterRulePlan p)=>p.Active.Length==0?"추가 규칙 없음 · 기본 생활 규칙을 적용합니다.":string.Join("\n\n",p.Active.Select(a=>(a.CarryCount>0?"유지  ":"추가  ")+Find(a.Id).Name+"\n"+Find(a.Id).Summary))+(p.Ended.Length==0?"":"\n\n종료  "+string.Join(" · ",p.Ended.Select(id=>Find(id).Name))+"\n이미 남은 기록과 물건의 상태는 유지됩니다.");
    }
}
