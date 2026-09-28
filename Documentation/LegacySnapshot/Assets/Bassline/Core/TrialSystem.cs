using System;
using System.Collections.Generic;
using System.Linq;

namespace Bassline
{
    public static class DialogueDirector
    {
        public const int MaxTurns=36;
        public static void Say(WorldState w,Content c,int speaker,SpeechAct act,string text,int target=-1,string evidence="",string second="",string reason="")
        {
            w.trial.speeches.Add(new Speech { speaker=speaker,target=target,act=act,text=text,tick=w.tick,evidenceId=evidence,secondId=second,reason=reason });
            w.Record("Speech",text,speaker,target,true);
            if(w.trial.speeches.Count>100) w.trial.speeches.RemoveAt(0);
        }
        public static void Reveal(WorldState w,Content c,int speaker,string id)
        {
            if(!w.Person(speaker).Knows(id)) throw new InvalidOperationException("Unknown evidence cannot be disclosed");
            var e=w.incident.evidence.Single(x=>x.id==id);
            bool verified=e.kind!=EvidenceKind.Testimony && w.Person(speaker).knowledge.Any(k=>k.evidenceId==id && k.verified);
            foreach(var p in w.Living) { KnowledgeSystem.Learn(w,p.id,id,speaker,verified);ReasoningSystem.Reconsider(w,c,p.id); }
            if(!w.trial.disclosed.Contains(id)) w.trial.disclosed.Add(id);
            Say(w,c,speaker,SpeechAct.Reveal,e.title+"을 공개하겠습니다.\n"+e.text,evidence:id,reason:verified?"확인한 원본 공개":"확인하지 않은 전언 또는 개인 진술");
        }
        public static void Next(WorldState w,Content c,int requested=-1)
        {
            if(w.phase!=Phase.Trial || w.trial.turn>=MaxTurns) return;
            var pool=w.Living.Where(x=>x.id!=0).OrderBy(x=>x.id).ToList();if(pool.Count==0) return;
            var p=requested>=0 ? pool.FirstOrDefault(x=>x.id==requested) : pool[w.trial.speakerCursor++%pool.Count];
            if(p==null) return;w.trial.turn++;w.tick++;
            ReasoningSystem.Reconsider(w,c,p.id);var d=c.Person(p.id);
            var last=w.trial.speeches.LastOrDefault();
            var undisclosed=p.knowledge.Select(k=>w.incident.evidence.Find(e=>e.id==k.evidenceId)).Where(e=>e!=null && !w.trial.disclosed.Contains(e.id)).ToList();
            var useful=undisclosed.OrderBy(e=>e.kind==EvidenceKind.Testimony?1:0).FirstOrDefault();
            if(useful!=null && (requested>=0 || d.cooperation>=65 || w.trial.turn>12 || p.Relation(0).trust>=15))
            { Reveal(w,c,p.id,useful.id);return; }
            if(last!=null && last.target==p.id && last.act==SpeechAct.Claim)
            {
                p.fear=WorldState.Clamp(p.fear+8);p.Relation(last.speaker).trust=WorldState.Clamp(p.Relation(last.speaker).trust-4,-100,100);
                Say(w,c,p.id,SpeechAct.Defend,d.composure<50?"잠깐만… 하나씩 물어봐 주세요. 제가 확인한 것부터 말할게요.":"그 의심과 실제 기록은 구분해 줘. 어떤 근거로 나를 지목했지?",last.speaker);return;
            }
            if(last!=null && last.act==SpeechAct.Defend && d.empathy>=80)
            { Say(w,c,p.id,SpeechAct.Mediate,"한 사람에게 몰아붙이지 말자. 먼저 직접 확인한 것과 들은 말을 나눠 보자.",last.speaker);w.Person(last.speaker).fear=Math.Max(0,w.Person(last.speaker).fear-6);return; }
            string key=p.id+":"+p.hypothesis+":"+p.knowledge.Count;
            if(p.hypothesis>=0 && !w.trial.usedClaims.Contains(key))
            {
                w.trial.usedClaims.Add(key);
                string certainty=p.candidates.Count==1?"확인된 자료를 연결하면 남는 사람은 ":"아직 가설이지만, 나는 ";
                Say(w,c,p.id,SpeechAct.Claim,certainty+c.Person(p.hypothesis).name+(p.candidates.Count==1?"입니다. 반례가 있다면 확인하겠습니다.":"의 설명을 더 듣고 싶어. 관계 때문에 판단이 기울었을 수도 있어."),p.hypothesis,reason:p.candidates.Count==1?"확인한 증거로 다른 후보 배제":"불완전한 정보와 관계에 따른 잠정 가설");return;
            }
            if(useful!=null) Say(w,c,p.id,SpeechAct.Withhold,"아직 공개하지 않은 자료가 있어. 지금은 다른 사람의 설명도 듣고 싶어.",reason:"협력 성향이 낮아 정보 공개 시점을 보류");
            else Say(w,c,p.id,SpeechAct.Question,"같은 말을 반복하기보다 출처를 맞춰 보자. 현장 흔적과 기록을 함께 확인했어?",0);
        }
    }
    public static class InterruptSystem
    {
        public static string Act(WorldState w,Content c,SpeechAct action,int target,string first="",string second="",LinkKind relation=LinkKind.Supports)
        {
            if(w.phase!=Phase.Trial) return "재판에서만 개입할 수 있습니다.";
            if(!w.Living.Any(x=>x.id==target)) return "현재 참가자를 선택하세요.";
            if(action==SpeechAct.Question || action==SpeechAct.Request)
            {
                DialogueDirector.Say(w,c,0,action,action==SpeechAct.Request?c.Person(target).name+", 말을 끝까지 들어볼게요.":c.Person(target).name+", 직접 확인한 근거를 설명해 주세요.",target);
                w.Person(target).fear=Math.Max(0,w.Person(target).fear-8);
                w.Person(target).Relation(0).trust=WorldState.Clamp(w.Person(target).Relation(0).trust+2,-100,100);
                DialogueDirector.Next(w,c,target);return "발언권을 연결했습니다.";
            }
            if(action==SpeechAct.Agree)
            {
                var claim=w.trial.speeches.LastOrDefault(x=>x.speaker==target && x.act==SpeechAct.Claim);
                if(claim==null) return "그 인물에게 아직 지지할 주장이 없습니다.";
                DialogueDirector.Say(w,c,0,action,c.Person(target).name+"의 가설을 함께 검토하겠습니다. 동의가 곧 증명은 아닙니다.",claim.target);
                w.Person(target).Relation(0).trust=WorldState.Clamp(w.Person(target).Relation(0).trust+4,-100,100);
                // A public promise is social pressure; it does not create evidence or change truth.
                foreach(var p in w.Living.Where(x=>x.id!=0 && c.Person(x.id).cooperation>65 && x.Relation(0).trust>=12)) p.promisedVote=claim.target;
                return "주장 지지가 관계와 공개 입장에 반영되었습니다.";
            }
            if(!w.Person(0).Knows(first) || !w.Person(0).Knows(second)) return "직접 확보한 단서 두 개를 선택하세요.";
            var link=EvidenceGraph.Connect(w.incident,first,second,relation);
            if(link==null)
            {
                DialogueDirector.Say(w,c,0,action,"두 자료를 연결해 보았지만, 이 관계는 아직 입증할 수 없습니다.",target,first,second);
                return "근거가 부족합니다. 출처와 시각, 연결 유형을 다시 확인하세요.";
            }
            if(action==SpeechAct.Rebuttal && relation!=LinkKind.Contradicts) return "반박은 발언과 자료의 모순을 연결해야 합니다. 새 추론에는 논점 변경을 사용하세요.";
            if(!w.trial.disclosed.Contains(first)) DialogueDirector.Reveal(w,c,0,first);
            if(!w.trial.disclosed.Contains(second)) DialogueDirector.Reveal(w,c,0,second);
            string key=first+":"+second+":"+relation;
            if(!w.trial.playerLinks.Contains(key)) w.trial.playerLinks.Add(key);
            DialogueDirector.Say(w,c,0,action,link.explanation,target,first,second,"확보한 자료의 관계 검증");
            foreach(var p in w.Living) ReasoningSystem.Reconsider(w,c,p.id);
            if(relation==LinkKind.Contradicts)
            {
                var testimony=w.incident.evidence.FirstOrDefault(e=>(e.id==first || e.id==second) && e.kind==EvidenceKind.Testimony);
                if(testimony!=null) foreach(var p in w.Living.Where(x=>x.id!=testimony.speaker)) p.Relation(testimony.speaker).trust=WorldState.Clamp(p.Relation(testimony.speaker).trust-12,-100,100);
            }
            w.trial.topic++;return "새 논점이 공유되었습니다. 각 인물이 근거를 다시 판단합니다.";
        }
    }
    public static class SocialDecisionSystem
    {
        public static Ballot Vote(WorldState w,Content c,int actor,List<int> restricted=null)
        {
            ReasoningSystem.Reconsider(w,c,actor);var p=w.Person(actor);var d=c.Person(actor);
            var eligible=(restricted??w.Living.Select(x=>x.id).ToList()).Where(x=>w.Person(x).alive).ToList();
            int believed=p.hypothesis;
            if(believed<0 || !eligible.Contains(believed)) believed=eligible.OrderByDescending(x=>p.Relation(x).grievance).ThenBy(x=>x).First();
            int choice=believed;string reason=p.candidates.Count==1 ? "확인한 증거를 연결해 다른 후보를 배제했다.":"증거가 불완전해 현재 가설과 출처 신뢰를 기준으로 판단했다.";
            if(eligible.Count>1 && believed!=actor && p.Relation(believed).affection>=40 && d.empathy>=80)
            {
                choice=eligible.Where(x=>x!=believed && x!=actor).DefaultIfEmpty(believed).OrderByDescending(x=>p.Relation(x).grievance).First();
                reason="실제 가설은 "+c.Person(believed).name+"이지만, 가까운 사람을 보호하려 표를 바꾸었다. 증거를 부정한 것은 아니다.";
            }
            else if(p.candidates.Count!=1 && p.promisedVote>=0 && eligible.Contains(p.promisedVote) && d.cooperation>=65)
            { choice=p.promisedVote;reason="결정적 근거가 없는 상태에서 공개 지지 약속을 지켰다."; }
            else if(believed==actor && d.desire>=65 && eligible.Any(x=>x!=actor))
            { choice=eligible.Where(x=>x!=actor).OrderByDescending(x=>p.Relation(x).grievance).ThenBy(x=>x).First();reason="자신에게 불리한 판단을 알지만 계약과 생존을 위해 다른 사람에게 투표했다."; }
            p.voteReason=reason;return new Ballot { voter=actor,target=choice,reason=reason };
        }
    }
    public static class VotingSystem
    {
        public static bool Cast(WorldState w,Content c,int playerTarget)
        {
            if(w.phase!=Phase.Trial || w.trial.settled) return false;
            var targets=w.trial.tieCandidates.Count>0?w.trial.tieCandidates:w.Living.Select(x=>x.id).ToList();
            if(!targets.Contains(playerTarget)) return false;
            w.trial.ballots.Clear();
            foreach(var p in w.Living.Where(x=>!w.disenfranchised.Contains(x.id)))
                w.trial.ballots.Add(p.id==0 ? new Ballot{voter=0,target=playerTarget,reason="플레이어의 최종 판단"}:SocialDecisionSystem.Vote(w,c,p.id,targets));
            var groups=w.trial.ballots.GroupBy(x=>x.target).Select(g=>new{target=g.Key,count=g.Count()}).OrderByDescending(x=>x.count).ToList();
            if(groups.Count==0) throw new InvalidOperationException("No eligible voters");
            var tied=groups.Where(g=>g.count==groups[0].count).Select(g=>g.target).ToList();
            if(tied.Count>1 && w.trial.tieRound==0)
            {
                w.trial.tieRound=1;w.trial.tieCandidates=tied;
                DialogueDirector.Say(w,c,-1,SpeechAct.Question,"동률입니다. 해당 후보들로 한 차례 재투표합니다. 다시 동률이면 그 후보 중 공개 추첨합니다.");return false;
            }
            w.trial.nominated=tied.Count==1?tied[0]:tied[w.Next(tied.Count)];
            if(tied.Count>1) w.Record("TieLottery","재동률 공개 추첨: "+c.Person(w.trial.nominated).name,visible:true);
            Resolve(w,c,w.trial.nominated);return true;
        }
        public static void Resolve(WorldState w,Content c,int nominee)
        {
            if(w.phase!=Phase.Trial || w.trial.settled) throw new InvalidOperationException("Invalid adjudication phase");
            if(nominee>=0 && !w.Person(nominee).alive) throw new InvalidOperationException("Nominee is not alive");
            var incident=w.incident;bool correct=nominee==incident.culprit;
            w.trial.nominated=nominee;w.trial.correct=correct;w.trial.settled=true;
            var removed=new List<string>();
            if(correct) Exit(w,c,incident.culprit,false,removed);
            else
            {
                if(w.HasRule("A1") && nominee>=0) Exit(w,c,nominee,false,removed);
                Exit(w,c,incident.culprit,true,removed);
                if((!w.HasRule("A1") || nominee<0) && w.Living.Count>3)
                { var choices=w.Living;Exit(w,c,choices[w.Next(choices.Count)].id,false,removed); }
            }
            if(w.Living.Count<3) throw new InvalidOperationException("Survivor invariant violated");
            if(w.deadlineActor>=0 && !w.Person(w.deadlineActor).escaped)
            { w.Person(w.deadlineActor).rewardExpired=true;w.Record("RewardExpired",c.Person(w.deadlineActor).name+"의 계약 보상 권리가 만료되었습니다.",w.deadlineActor,visible:true); }
            w.trial.verdict=(correct?"범인 지목 성공":"범인 지목 실패")+"\n실제 범인: "+c.Person(incident.culprit).name+"\n"+string.Join("\n",removed)+"\n남은 참가자 "+w.Living.Count+"명";
            w.Record("Verdict",w.trial.verdict,visible:true);
            w.archive.Add(new LoopRecord { loop=w.loop,chapter=w.chapter,culprit=incident.culprit,nominee=nominee,title=incident.title,verdict=w.trial.verdict,events=w.events.Where(e=>e.tick>=w.chapterStart).ToList() });
            w.phase=Phase.Verdict;w.notice=w.trial.verdict;
        }
        static void Exit(WorldState w,Content c,int id,bool escape,List<string> removed)
        {
            var p=w.Person(id);if(!p.alive) return;p.alive=false;p.escaped=escape;
            removed.Add(c.Person(id).name+(escape?" · 계약 보상과 함께 탈출":" · 퇴장"));
            w.Record(escape?"Escape":"Exit",removed.Last(),id,visible:true);
        }
    }
    public static class GameFlow
    {
        public static void StartDaily(WorldState w) { if(w.phase==Phase.Announcement) {w.phase=Phase.Daily;w.notice="WASD 이동 · 마우스 시점 · E 상호작용 · Tab 조사수첩. 대화와 이동에 따라 시간이 흐릅니다.";} }
        public static void StartTrial(WorldState w,Content c)
        {
            if(w.phase!=Phase.Investigation) return;w.phase=Phase.Trial;
            w.Record("TrialStarted","모든 참가자가 대회의실로 소집되었습니다.",visible:true);
            if(w.HasRule("C6"))
            {
                foreach(var statement in w.events.Where(e=>e.kind=="SealedStatement" && e.id>=w.chapterEventStart).ToArray())
                    w.Record("SealedStatementOpened",statement.text,statement.actor,visible:true);
                foreach(var e in w.incident.evidence.Where(e=>e.kind==EvidenceKind.Testimony))
                    foreach(var p in w.Living) KnowledgeSystem.Learn(w,p.id,e.id,e.speaker,false);
            }
            foreach(var p in w.Living) ReasoningSystem.Reconsider(w,c,p.id);
            DialogueDirector.Say(w,c,-1,SpeechAct.Question,"지금부터 심리를 시작합니다. 직접 본 사실과 전해 들은 말, 추측을 구분해 주십시오.");
        }
        public static void Continue(WorldState w,Content c)
        {
            if(w.phase!=Phase.Verdict) return;
            if(w.Living.Count==3) {w.phase=Phase.Loop;return;}
            if(!w.Person(0).alive) {w.phase=Phase.PlayerOut;return;}
            w.chapter++;RuleEngine.BeginChapter(w,c);
        }
        public static void OpenFinalHearing(WorldState w,Content c)
        {
            w.phase=Phase.FinalHearing;
            // Provisional authored procedure, explicitly labelled in the UI and design notes.
            // No fabricated crime or secret violation: an NPC consents before any exit happens.
            var volunteer=w.Living.Where(p=>p.id!=0).OrderByDescending(p=>c.Person(p.id).empathy+p.pressure-c.Person(p.id).desire).ThenBy(p=>p.id).First();
            w.hearingVolunteer=volunteer.id;
            w.Record("WaiverOffer",c.Person(volunteer.id).name+"이 보상을 포기하고 자신의 퇴장 심리를 요청했다. 요청은 철회할 수 있으며 다른 참가자의 위반을 뜻하지 않는다.",volunteer.id,visible:true);
            w.notice="최종 계약 심리 · 임시 시나리오: 자발적 계약 포기. 신규 사건 없이 신청 당사자의 의사를 확인합니다.";
        }
        public static void ConcludeHearing(WorldState w,Content c)
        {
            if(w.phase!=Phase.FinalHearing || w.Living.Count!=4) return;
            var consent=w.events.LastOrDefault(e=>e.kind=="WaiverOffer" && e.actor==w.hearingVolunteer);
            if(consent==null) throw new InvalidOperationException("No voluntary waiver record");
            w.Person(w.hearingVolunteer).alive=false;
            string verdict=c.Person(w.hearingVolunteer).name+"의 자발적 계약 포기와 퇴장을 기록합니다. 남은 참가자 3명.";
            w.Record("WaiverAccepted",verdict,w.hearingVolunteer,visible:true);
            w.archive.Add(new LoopRecord { loop=w.loop,chapter=w.chapter,culprit=-1,nominee=w.hearingVolunteer,title="최종 계약 심리 (임시)",verdict=verdict,events=w.events.Where(e=>e.tick>=w.chapterStart).ToList() });
            w.phase=Phase.Loop;
        }
        public static WorldState ResetLoop(WorldState old,Content c)
        {
            if(old.phase!=Phase.Loop && old.phase!=Phase.PlayerOut) throw new InvalidOperationException("Not at a loop boundary");
            var next=WorldFactory.Create(c,old.seed+old.loop*7919);next.seed=old.seed;next.loop=old.loop+1;next.archive=new List<LoopRecord>(old.archive);
            // Fresh character states intentionally contain no knowledge or emotional memory.
            return next;
        }
    }
}


