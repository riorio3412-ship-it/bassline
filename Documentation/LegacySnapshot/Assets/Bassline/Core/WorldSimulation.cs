using System;
using System.Collections.Generic;
using System.Linq;

namespace Bassline
{
    public static class WorldFactory
    {
        public static WorldState Create(Content content, int seed)
        {
            var w = new WorldState { seed = seed, randomState = (uint)seed == 0 ? 1u : (uint)seed };
            string[] names = { "메인 홀", "식당", "도서실", "작업실", "온실", "별관", "오락실", "기록 보관실", "대회의실", "객실 회랑" };
            for (int i = 0; i < names.Length; i++) w.rooms.Add(new RoomState { id = i, name = names[i] });
            for (int i = 1; i < 10; i++) Connect(w, 0, i);
            Connect(w, 1, 3); Connect(w, 2, 7); Connect(w, 4, 5); Connect(w, 5, 6);
            foreach (var data in content.characters)
            {
                int bedroom = 10 + data.id;
                w.rooms.Add(new RoomState { id = bedroom, name = string.Format("객실 {0:00}", data.id + 1) });
                Connect(w, 9, bedroom);
                w.characters.Add(new CharacterState { id = data.id, room = 0, bedroom = bedroom, pressure = data.id == 0 ? 0 : 22 + data.desire / 5 });
                w.items.Add(new ItemState { id = data.id, owner = data.id, holder = data.id, room = 0, name = data.name + "의 보관 인장" });
                w.items.Add(new ItemState { id = 100 + data.id, owner = data.id, holder = -1, room = bedroom, name = data.name + "의 여행 가방" });
                w.items[data.id * 2].history.Add(w.Record("Custody", "입장 시 인장 인수", data.id, item: data.id).id);
            }
            int[][] pairs = { new[]{0,1,-15},new[]{0,2,25},new[]{0,10,20},new[]{1,11,-20},new[]{3,14,-30},new[]{6,7,30},new[]{9,5,-25},new[]{8,11,25},new[]{10,12,30},new[]{15,12,-10},new[]{13,9,20},new[]{16,15,15},new[]{17,4,-25} };
            foreach (var p in pairs)
            {
                foreach (var ids in new[]{new[]{p[0],p[1]},new[]{p[1],p[0]}})
                {
                    var r = w.Person(ids[0]).Relation(ids[1]); r.trust = p[2]; r.affection = p[2]; r.grievance = Math.Max(0,-p[2]);
                }
            }
            w.Record("Admission", "3명에서 반복. 4명에서는 신규 사건 없이 최종 계약 심리. 5명 오답 시 마지막 세 자리 보존.", visible:true);
            RuleEngine.BeginChapter(w, content);
            return w;
        }
        public static void Connect(WorldState w, int a, int b)
        {
            if (!w.rooms[a].links.Contains(b)) w.rooms[a].links.Add(b);
            if (!w.rooms[b].links.Contains(a)) w.rooms[b].links.Add(a);
        }
        public static bool Reachable(WorldState w, int from, int to)
        {
            var seen = new HashSet<int> { from }; var queue = new Queue<int>(); queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int r = queue.Dequeue(); if (r == to) return true;
                foreach (var n in w.rooms[r].links)
                    if (w.rooms[n].accessible && seen.Add(n)) queue.Enqueue(n);
            }
            return false;
        }
    }
    public static class RuleEngine
    {
        static readonly int[] Weights = { 30,30,25,15 };
        public static bool Compatible(RuleData a, RuleData b)
        {
            if (a.type == b.type || (a.major && b.major)) return false;
            string key = a.id + ":" + b.id, rev = b.id + ":" + a.id;
            return !new[]{"A5:D4","A10:C2"}.Any(x => x == key || x == rev);
        }
        public static void BeginChapter(WorldState w, Content c)
        {
            w.previousRules = new List<string>(w.rules); w.rules.Clear(); w.disenfranchised.Clear(); w.preliminary.Clear(); w.deadlineActor = -1;w.preliminaryConfirmed=false;
            w.chapterStart = w.tick; w.chapterEventStart=w.events.Count; w.incident = null; w.trial = new TrialState();
            foreach(var item in w.items.Where(i=>i.holder>=0 && !w.Person(i.holder).alive))
            {
                item.holder=-1;item.state="공동 회수";
                item.history.Add(w.Record("Custody","퇴장자의 물건을 현장에 반환",item:item.id,to:item.room).id);
            }
            foreach (var p in w.characters) { p.knowledge.Clear(); p.candidates.Clear(); p.hypothesis=-1; p.promisedVote=-1; }
            if (w.Living.Count == 4) { GameFlow.OpenFinalHearing(w,c); return; }
            var candidates = c.rules.Where(r => r.implemented && !r.proposal && r.minAlive <= w.Living.Count && r.minChapter <= w.chapter && !w.previousRules.Contains(r.id) && (r.id!="D9" || w.Living.Any(p=>p.id!=0 && !p.rewardExpired))).ToList();
            var first = Draw(w,candidates); if (first != null) w.rules.Add(first.id);
            if (w.chapter >= 3 && w.Next(100) < 25 && first != null)
            {
                var second = Draw(w,candidates.Where(x => Compatible(first,x)).ToList());
                if (second != null) w.rules.Add(second.id);
            }
            foreach (string id in w.rules)
            {
                w.Record("RuleAnnounced", id + " " + c.Rule(id).name + " — " + c.Rule(id).description, visible:true);
                Apply(w,c,id);
            }
            w.phase = Phase.Announcement;
            w.notice = "새 규칙을 확인한 뒤 일상을 시작하세요. 규칙은 이번 장이 끝날 때까지 적용됩니다.";
        }
        static RuleData Draw(WorldState w,List<RuleData> candidates)
        {
            if(candidates.Count==0) return null;
            var groups = new[]{"A","B","C","D"}.Where(t => candidates.Any(r=>r.type==t)).ToList();
            int roll=w.Next(groups.Sum(t=>Weights[t[0]-'A'])); string picked=groups[0];
            foreach(string g in groups) { roll-=Weights[g[0]-'A']; if(roll<0) { picked=g;break; } }
            var pool=candidates.Where(r=>r.type==picked).ToList(); return pool[w.Next(pool.Count)];
        }
        public static void Apply(WorldState w,Content c,string id)
        {
            var living=w.Living;
            if(id=="C1") w.Record("RecordScope","이번 장에는 사건 시각의 동관 시각 인증 기록을 공동 공개합니다. 기록에 없는 사람의 위치는 보증하지 않습니다.",visible:true);
            if(id=="B5")
            {
                var rooms=living.Select(p=>p.bedroom).ToList(); Shuffle(w,rooms);
                for(int i=0;i<living.Count;i++)
                {
                    var p=living[i];int old=p.bedroom;p.bedroom=rooms[i];
                    w.Record("RoomAssigned", c.Person(p.id).name+"의 객실 재배정",p.id,visible:true,from:old,to:p.bedroom);
                    foreach(var item in w.items.Where(x=>x.owner==p.id && x.holder<0 && x.room==old))
                    {
                        item.room=p.bedroom;item.history.Add(w.Record("ItemMoved","개인 소지품 이전",p.id,from:old,to:p.bedroom,item:item.id).id);
                    }
                }
            }
            if(id=="B8")
            {
                // The hall remains connected. Peripheral shortcuts really alter movement cost.
                for(int a=1;a<=7;a++) foreach(int b in w.rooms[a].links.Where(x=>x>0 && x<=7).ToArray()) { w.rooms[a].links.Remove(b);w.rooms[b].links.Remove(a); }
                var rooms=Enumerable.Range(1,7).ToList();Shuffle(w,rooms);
                for(int i=0;i<6;i+=2) WorldFactory.Connect(w,rooms[i],rooms[i+1]);
                w.Record("TopologyChanged","회랑 연결이 바뀌었습니다. 문 표지와 지도에 새 통로가 반영됩니다.",visible:true);
            }
            if(id=="A7") { var ids=living.Select(p=>p.id).ToList();Shuffle(w,ids);w.disenfranchised=ids.Take(2).ToList();w.Record("VotingRights","투표권 제외: "+string.Join(", ",w.disenfranchised.Select(x=>c.Person(x).name)),visible:true); }
            if(id=="D7") foreach(var p in living)
            {
                var chosen=living.Where(x=>x.id!=p.id).OrderByDescending(x=>p.Relation(x.id).trust).ThenBy(x=>x.id).Take(3).ToList();
                w.Record("TrustList",c.Person(p.id).name+"의 신뢰 명단: "+string.Join(", ",chosen.Select(x=>c.Person(x.id).name)),p.id,visible:true);
                foreach(var other in living.Where(x=>x.id!=p.id && !chosen.Contains(x))) { other.Relation(p.id).grievance+=2; }
            }
            if(id=="D9")
            {
                var pool=living.Where(x=>x.id!=0 && !x.rewardExpired).ToList();
                if(pool.Count>0) { var p=pool[w.Next(pool.Count)]; w.deadlineActor=p.id;p.pressure=WorldState.Clamp(p.pressure+25);w.Record("Deadline",c.Person(p.id).name+"의 계약 보상이 이번 판결 후 만료됩니다.",p.id,visible:true); }
            }
        }
        public static void Shuffle<T>(WorldState w,List<T> list) { for(int i=list.Count-1;i>0;i--) { int j=w.Next(i+1);T v=list[j];list[j]=list[i];list[i]=v; } }
        public static int VictimCap(WorldState w) { return w.Living.Count<=6 ? 1 : w.HasRule("B2") ? 3 : 2; }
    }
    public static class WorldSimulation
    {
        public static bool Move(WorldState w,int actor,int to)
        {
            var p=w.Person(actor); if(!p.alive || !w.rooms[p.room].links.Contains(to) || !w.rooms[to].accessible) return false;
            int old=p.room;p.room=to;p.action="이동 · "+w.rooms[to].name;
            w.Record("Move",p.action,actor,from:old,to:to);
            foreach(var item in w.items.Where(x=>x.holder==actor))
            {item.room=to;item.history.Add(w.Record("ItemMoved","보관자와 함께 이동",actor,from:old,to:to,item:item.id).id);}
            return true;
        }
        public static void Advance(WorldState w,Content c)
        {
            if(w.phase!=Phase.Daily && w.phase!=Phase.Investigation) return;
            w.tick++;
            foreach(var p in w.Living.Where(x=>x.id!=0))
            {
                if(w.Next(100)<65)
                {
                    var links=w.rooms[p.room].links;
                    // Public common areas attract daily activity; guest rooms remain accessible.
                    var options=links.Where(x=>x<10 || x==p.bedroom).ToList();
                    if(options.Count>0) Move(w,p.id,options[w.Next(options.Count)]);
                }
                if(w.phase==Phase.Daily)
                {
                    var available=w.items.FirstOrDefault(i=>i.id<100 && i.holder<0 && i.room==p.room);
                    if(available!=null && !w.items.Any(i=>i.id<100 && i.holder==p.id))
                    {
                        available.holder=p.id;available.history.Add(w.Record("Custody","회수된 인장을 인수",p.id,to:p.room,item:available.id).id);
                    }
                    p.pressure=WorldState.Clamp(p.pressure+1+(c.Person(p.id).desire>80 ? 1:0));
                    p.fatigue=WorldState.Clamp(p.fatigue+1);
                    var peers=w.Living.Where(x=>x.id!=p.id && x.room==p.room).ToList();
                    if(peers.Count>0 && w.Next(100)<65) SocialInteractionSystem.Daily(w,c,p,peers[w.Next(peers.Count)]);
                }
                else
                {
                    var evidence=w.incident.evidence.Where(e=>e.room==p.room && e.kind!=EvidenceKind.Testimony && !p.Knows(e.id)).ToList();
                    if(evidence.Count>0 && w.Next(100)<c.Person(p.id).observation)
                    {
                        var e=evidence[w.Next(evidence.Count)];KnowledgeSystem.Learn(w,p.id,e.id,p.id,true);
                        p.action="자료 조사";ReasoningSystem.Reconsider(w,c,p.id);
                    }
                }
            }
            if(w.phase==Phase.Daily && w.tick-w.chapterStart>= (w.HasRule("D10") ? 18:10)) IncidentGenerator.TryGenerate(w,c);
        }
    }
    public static class SocialInteractionSystem
    {
        public static void Daily(WorldState w,Content c,CharacterState a,CharacterState b)
        {
            var data=c.Person(a.id);var relation=a.Relation(b.id);int roll=w.Next(100);
            bool reconcile=roll<data.cooperation/3;
            if(reconcile)
            {
                relation.trust=WorldState.Clamp(relation.trust+3,-100,100);relation.affection=WorldState.Clamp(relation.affection+3,-100,100);
                relation.grievance=Math.Max(0,relation.grievance-8);a.pressure=Math.Max(0,a.pressure-5);
                w.Record("Reconcile",c.Person(a.id).name+" → "+c.Person(b.id).name+": 도움을 요청하고 일의 분담을 합의했다.",a.id,b.id,a.room==w.Person(0).room);
            }
            else
            {
                relation.grievance=WorldState.Clamp(relation.grievance+9);relation.trust=WorldState.Clamp(relation.trust-4,-100,100);a.pressure=WorldState.Clamp(a.pressure+3);
                b.Relation(a.id).grievance=WorldState.Clamp(b.Relation(a.id).grievance+4);
                w.Record("Conflict",c.Person(a.id).name+" → "+c.Person(b.id).name+": "+(data.desire>80 ? "계약과 개인의 몫을 두고 충돌했다.":"서로의 약속과 정보 공개를 두고 충돌했다."),a.id,b.id,a.room==w.Person(0).room);
            }
            a.action=reconcile ? "협의 중":"논쟁 중";
            var item=w.items.FirstOrDefault(x=>x.holder==a.id && x.id<100);
            if(item!=null && reconcile && w.Next(100)<30)
            {
                item.holder=b.id;item.history.Add(w.Record("Custody","보관 인장을 맡김",a.id,b.id,from:a.room,to:b.room,item:item.id).id);
            }
        }
        public static string Talk(WorldState w,Content c,int target,bool help)
        {
            var p=w.Person(target);if(!p.alive || p.room!=w.Person(0).room) return "지금 같은 공간에 있지 않습니다.";
            if(w.phase==Phase.Daily)
            {
                p.Relation(0).trust=WorldState.Clamp(p.Relation(0).trust+(help?6:-2),-100,100);
                p.pressure=WorldState.Clamp(p.pressure+(help?-12:5));
                w.Record(help?"Help":"Question",help?"민혁이 이야기를 듣고 부담을 나누었다.":"민혁이 계약에 관해 물었다.",0,target,true);
                return c.Person(target).quote;
            }
            if(w.phase==Phase.Investigation)
            {
                var share=p.knowledge.Select(k=>w.incident.evidence.Find(e=>e.id==k.evidenceId)).Where(e=>e!=null && !w.Person(0).Knows(e.id)).ToList();
                var chosen=share.FirstOrDefault(e=>e.kind==EvidenceKind.Testimony) ?? share.FirstOrDefault();
                if(chosen==null) return "제가 확인한 내용은 이미 말씀드렸어요. 다른 방의 기록도 대조해 주세요.";
                if(c.Person(target).deception>85 && p.Relation(0).trust<10 && chosen.kind!=EvidenceKind.Testimony) return "지금은 내 판단을 조금 더 확인하고 싶어. 직접 기록을 찾아봐.";
                KnowledgeSystem.Learn(w,0,chosen.id,target,false);return chosen.text;
            }
            return "지금은 관장의 안내를 기다려야 합니다.";
        }
    }
}
