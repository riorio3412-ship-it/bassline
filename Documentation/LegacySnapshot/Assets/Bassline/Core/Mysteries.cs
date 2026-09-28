using System;
using System.Collections.Generic;
using System.Linq;

namespace Bassline
{
    public static class IncidentGenerator
    {
        public static bool TryGenerate(WorldState w, Content c)
        {
            if(w.phase!=Phase.Daily || w.Living.Count<=4) return false;
            var options=w.Living.Where(p=>p.id!=0 && p.pressure>=62 && w.items.Any(i=>i.id<100 && i.holder==p.id))
                .OrderByDescending(p=>p.pressure+c.Person(p.id).desire-c.Person(p.id).cooperation/3).ToList();
            foreach(var actor in options)
            {
                var victims=w.Living.Where(x=>x.id!=actor.id && x.room==actor.room).ToList();
                if(victims.Count<1 || victims.Count>RuleEngine.VictimCap(w)) continue;
                if(victims.Any(v=>actor.Relation(v.id).grievance<12)) continue;
                var conflicts=victims.Select(v=>w.events.LastOrDefault(e=>e.kind=="Conflict" && e.actor==actor.id && e.target==v.id && e.tick>=w.chapterStart)).ToList();
                if(conflicts.Any(x=>x==null)) continue;
                // Cooperation can resolve an opportunity; pressure does not guarantee an incident.
                if(w.Next(100)<c.Person(actor.id).cooperation/3)
                {
                    actor.pressure=Math.Max(0,actor.pressure-18);
                    foreach(var v in victims) SocialInteractionSystem.Daily(w,c,actor,v);
                    w.Record("Confession",c.Person(actor.id).name+"이 계약 압박을 고백하고 도움을 구했다.",actor.id,visible:actor.room==w.Person(0).room);
                    continue;
                }
                var item=w.items.First(i=>i.id<100 && i.holder==actor.id);
                var incident=new Incident { culprit=actor.id,room=actor.room,tick=w.tick,item=item.id,motiveEvent=conflicts[0].id,
                    template=item.owner==actor.id?"sealed-meeting":"entrusted-seal",title=item.owner==actor.id?"닫힌 약속":"맡겨진 인장",
                    victims=victims.Select(v=>v.id).ToList(),suspects=w.Living.Where(p=>!victims.Contains(p)).Select(p=>p.id).ToList(),positions=w.characters.Select(p=>p.room).ToList(),
                    motives=conflicts.Select(x=>x.text).ToList() };
                // Transactional construction: rejected candidates leave no events, deaths or evidence.
                int checkpoint=w.events.Count;
                BuildEvidence(w,c,incident,item);
                var errors=MysteryValidator.Validate(w,incident);
                if(errors.Count>0) { w.events.RemoveRange(checkpoint,w.events.Count-checkpoint); continue; }
                w.incident=incident;item.state="현장 봉인에 사용됨";
                item.history.Add(w.Record("ItemUsed","사건 현장 봉인",actor.id,from:actor.room,to:actor.room,item:item.id).id);
                foreach(var v in victims) { v.alive=false;w.Record("Victim",c.Person(v.id).name+"의 빈자리가 남았다.",v.id,visible:true); }
                w.Record("Incident",w.rooms[incident.room].name+"에서 사건이 발견되었습니다.",visible:true,to:incident.room);
                w.phase=w.Person(0).alive ? Phase.Investigation:Phase.PlayerOut;
                w.notice=w.rooms[incident.room].name+" · 피해자 "+string.Join(", ",victims.Select(v=>c.Person(v.id).name))+". 기록은 조사수첩에 남습니다.";
                foreach(var e in incident.evidence.Where(e=>e.kind==EvidenceKind.Testimony)) KnowledgeSystem.Learn(w,e.speaker,e.id,e.speaker,false);
                // Everyone knows their own actions; this is firsthand knowledge, not access to sealed truth.
                foreach(var p in w.Living)
                {
                    var local=incident.evidence.FirstOrDefault(e=>e.kind==EvidenceKind.Official && e.excludes.Contains(p.id));
                    if(local!=null) KnowledgeSystem.Learn(w,p.id,local.id,p.id,true);
                    if(w.HasRule("C6")) w.Record("SealedStatement",c.Person(p.id).name+"의 최초 진술 (진위 미확인): "+incident.evidence.Single(e=>e.kind==EvidenceKind.Testimony && e.speaker==p.id).text,p.id);
                }
                if(w.HasRule("C1")) foreach(var p in w.Living) KnowledgeSystem.Learn(w,p.id,incident.evidence[0].id,-1,true);
                if(w.HasRule("A8")) foreach(var p in w.Living)
                {
                    var target=w.Living.Where(x=>x.id!=p.id).OrderByDescending(x=>p.Relation(x.id).grievance).ThenBy(x=>x.id).First();
                    w.preliminary.Add(new Ballot { voter=p.id,target=target.id,reason="조사 전의 관계와 의심에 따른 사전 표. 최종 집계 제외." });
                }
                return true;
            }
            return false;
        }
        static void BuildEvidence(WorldState w,Content c,Incident incident,ItemState item)
        {
            string prefix="L"+w.loop+"C"+w.chapter+"-";
            var innocent=incident.suspects.Where(x=>x!=incident.culprit).ToList();
            var groups=new[]{innocent.Where((x,i)=>i%2==0).ToList(),innocent.Where((x,i)=>i%2==1).ToList()};
            for(int group=0;group<2;group++)
            {
                var e=new Evidence { id=prefix+"A"+group,title=group==0?"동관 시각 인증 기록":"서관 시각 인증 기록",route="A",sourceFamily="facility-clock",kind=EvidenceKind.Official,room=group==0?7:2,tick=w.tick,
                    text="사건 시각 "+w.Time+". 시설 시계가 아래 참가자의 현장 밖 위치를 인증했습니다. 기록에 없는 사람의 위치는 알 수 없습니다.\n",excludes=groups[group] };
                foreach(int id in groups[group])
                {
                    var stamp=w.Record("Presence",c.Person(id).name+" · "+w.rooms[w.Person(id).room].name,id,to:w.Person(id).room);
                    e.eventIds.Add(stamp.id);e.text+=c.Person(id).name+" — "+w.rooms[w.Person(id).room].name+"\n";
                }
                incident.evidence.Add(e);
            }
            var traceEvent=w.Record("SealTrace","사건 발생 순간 닫힌 현장 봉인에 인장 #"+item.id+"의 고유 흔적. 현장에는 피해자와 봉인을 닫은 한 사람만 있었다.",incident.culprit,to:incident.room,item:item.id);
            var trace=new Evidence { id=prefix+"B0",title="닫힌 현장의 봉인 흔적",route="B",sourceFamily="physical-seal",kind=EvidenceKind.Physical,room=incident.room,tick=w.tick,
                text="현장 출입판은 사건 순간 닫혔습니다. 봉인은 내부에서만 닫히며, 당시 피해자 외 인원은 한 명입니다. 사용된 인장: #"+item.id+". 인장의 원래 주인과 당시 보관자가 같은지는 별도 확인이 필요합니다.",eventIds=new List<int>{traceEvent.id} };
            var custody=w.Record("CustodyAudit","인장 #"+item.id+"의 당시 보관자: "+c.Person(item.holder).name,item.holder,to:item.room,item:item.id);
            var chain=new Evidence { id=prefix+"B1",title="인장 인수·반환 대장",route="B",sourceFamily="custody-register",kind=EvidenceKind.Official,room=3,tick=w.tick,
                text="#"+item.id+"의 최초 소유자: "+c.Person(item.owner).name+". 사건 시각의 마지막 인수자: "+c.Person(item.holder).name+". 그 뒤 인계는 없습니다. 보관했다는 사실만으로 범행을 증명하지 못합니다.",
                eventIds=item.history.Concat(new[]{custody.id}).ToList(),excludes=innocent,requires=new List<string>{trace.id} };
            incident.evidence.Add(trace);incident.evidence.Add(chain);
            incident.links.Add(new EvidenceLink { from=trace.id,to=chain.id,kind=LinkKind.Supports,explanation="현장에서 사용된 인장과 당시 보관자를 대조하면 단순 소유와 실제 사용을 구별할 수 있습니다." });
            incident.links.Add(new EvidenceLink { from=incident.evidence[0].id,to=incident.evidence[1].id,kind=LinkKind.Excludes,explanation="두 시각 기록을 합쳐 사건 현장에 있을 수 없었던 사람들을 배제합니다." });
            incident.links.Add(new EvidenceLink { from=chain.id,to=trace.id,kind=LinkKind.Source,explanation="인수 대장은 봉인에 남은 인장 번호의 보관 이력을 확인하는 출처입니다." });
            foreach(var p in w.Living.Where(p=>incident.suspects.Contains(p.id)))
            {
                bool lied=p.id==incident.culprit && c.Person(p.id).deception>=60;
                string statement=lied?"사건 시각에 인장을 보관하고 있지 않았어.":"사건 무렵 "+w.rooms[p.room].name+"에 있었어요. 다른 사람의 행동까지 확인한 건 아니에요.";
                var own=w.Record("PersonalObservation",statement,p.id,to:p.room,item:lied?item.id:-1);
                var e=new Evidence { id=prefix+"T"+p.id,title=c.Person(p.id).name+"의 진술",text=statement,route="",sourceFamily="witness-"+p.id,kind=EvidenceKind.Testimony,room=p.room,speaker=p.id,tick=w.tick,eventIds=new List<int>{own.id} };
                incident.evidence.Add(e);
                if(lied) incident.links.Add(new EvidenceLink { from=e.id,to=chain.id,kind=LinkKind.Contradicts,explanation="보관하지 않았다는 말과 실제 인수 기록이 모순됩니다. 거짓말만으로 범행이 입증되지는 않습니다." });
            }
        }
    }
    public static class EvidenceGraph
    {
        public static List<int> Candidates(Incident incident,IEnumerable<string> known)
        {
            var ids=new HashSet<string>(known);var suspects=new HashSet<int>(incident.suspects);
            foreach(var e in incident.evidence.Where(e=>ids.Contains(e.id) && e.kind!=EvidenceKind.Testimony))
                if(e.requires.All(ids.Contains)) suspects.ExceptWith(e.excludes);
            return suspects.OrderBy(x=>x).ToList();
        }
        public static EvidenceLink Connect(Incident incident,string from,string to,LinkKind kind)
        {
            return incident.links.FirstOrDefault(x=>x.kind==kind && ((x.from==from && x.to==to)||(kind!=LinkKind.Source && x.from==to && x.to==from)));
        }
    }
    public static class MysteryValidator
    {
        public static List<string> Validate(WorldState w,Incident incident)
        {
            var errors=new List<string>();
            int initialCount=incident.suspects.Count+incident.victims.Count;
            int cap=initialCount<=6?1:w.HasRule("B2")?3:2;
            if(incident.victims.Count<1 || incident.victims.Count>cap) errors.Add("피해자 상한 위반");
            if(incident.positions.Count!=w.characters.Count) {errors.Add("현장 스냅샷 누락");return errors;}
            if(incident.victims.Contains(incident.culprit) || !incident.suspects.Contains(incident.culprit)) errors.Add("인물 역할 충돌");
            if(incident.positions.Count!=w.characters.Count || incident.positions[incident.culprit]!=incident.room || incident.victims.Any(v=>incident.positions[v]!=incident.room)) errors.Add("현장 동선 모순");
            var onScene=incident.suspects.Where(p=>incident.positions[p]==incident.room).ToList();
            if(onScene.Count!=1 || onScene[0]!=incident.culprit) errors.Add("폐쇄 현장 템플릿 전제 위반");
            var item=w.items.Find(x=>x.id==incident.item);
            if(item==null || item.holder!=incident.culprit || item.room!=incident.room) errors.Add("소지품 시간표 모순");
            if(incident.motives.Count!=incident.victims.Count || incident.motiveEvent<0 || !w.events.Any(e=>e.id==incident.motiveEvent && e.kind=="Conflict")) errors.Add("동기 사건 없음");
            if(incident.evidence.Select(e=>e.id).Distinct().Count()!=incident.evidence.Count) errors.Add("단서 ID 중복");
            foreach(var e in incident.evidence)
            {
                if(!WorldFactory.Reachable(w,0,e.room)) errors.Add("단서 접근 불가: "+e.id);
                if(e.eventIds.Count==0 || e.eventIds.Any(id=>id<0 || id>=w.events.Count || w.events[id].tick>incident.tick)) errors.Add("사실 출처 없음: "+e.id);
                if(e.excludes.Contains(incident.culprit)) errors.Add("공식 자료가 진실과 충돌");
                if(e.kind==EvidenceKind.Testimony && e.eventIds.Any(id=>w.events[id].actor!=e.speaker)) errors.Add("목격하지 않은 사실 증언");
                if(e.excludes.Any(id=>id<0 || id>=incident.positions.Count)) errors.Add("잘못된 인물 참조");
                if(e.route=="A" && e.excludes.Any(id=>incident.positions[id]==incident.room)) errors.Add("현장 인물을 알리바이로 배제");
            }
            foreach(string route in new[]{"A","B"})
            {
                var answer=EvidenceGraph.Candidates(incident,incident.evidence.Where(e=>e.route==route).Select(e=>e.id));
                if(answer.Count!=1 || answer[0]!=incident.culprit) errors.Add("독립 조사 경로 실패: "+route);
            }
            var familiesA=incident.evidence.Where(e=>e.route=="A").Select(e=>e.sourceFamily);
            if(incident.evidence.Where(e=>e.route=="B").Any(e=>familiesA.Contains(e.sourceFamily))) errors.Add("조사 경로 출처 공유");
            int after=initialCount-incident.victims.Count;
            if(after-1<3 || (after-2<3 && initialCount!=5)) errors.Add("판결 후 최소 인원 위반");
            foreach(var move in w.events.Where(e=>e.kind=="Move" && e.id>=w.chapterEventStart))
                if(!w.rooms[move.from].links.Contains(move.to)) { errors.Add("이동 불가능한 통로");break; }
            return errors;
        }
    }
    public static class KnowledgeSystem
    {
        public static void Learn(WorldState w,int actor,string evidenceId,int source,bool verified)
        {
            if(w.incident==null || !w.incident.evidence.Any(e=>e.id==evidenceId)) throw new InvalidOperationException("Unknown evidence");
            var person=w.Person(actor);var existing=person.knowledge.Find(k=>k.evidenceId==evidenceId);
            if(existing!=null) { existing.verified|=verified;return; }
            person.knowledge.Add(new Knowledge { evidenceId=evidenceId,source=source,verified=verified,learnedAt=w.tick });
            w.Record("Learned",evidenceId,actor,source);
        }
        public static List<string> Verified(CharacterState p) { return p.knowledge.Where(k=>k.verified).Select(k=>k.evidenceId).ToList(); }
    }
    public static class ReasoningSystem
    {
        // This method deliberately has no branch that reads Incident.culprit.
        public static void Reconsider(WorldState w,Content c,int actor)
        {
            var p=w.Person(actor);
            p.candidates=EvidenceGraph.Candidates(w.incident,KnowledgeSystem.Verified(p));
            if(p.candidates.Count==1) p.hypothesis=p.candidates[0];
            else
            {
                var candidates=p.candidates.Where(x=>x!=actor).ToList();
                p.hypothesis=candidates.Count==0 ? -1 : candidates.OrderByDescending(x=>p.Relation(x).grievance-p.Relation(x).trust/2).ThenBy(x=>x).First();
            }
        }
    }
}
