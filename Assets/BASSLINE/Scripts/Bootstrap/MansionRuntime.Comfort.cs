using System;
using System.IO;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.NPC;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerSmallTalkPort,IPlayerWelcomePort,IPlayerSaveLibraryPort
    {
        public bool WelcomeRead=>Knowledge.For("CH_01").Records().Any(r=>r.Predicate=="WelcomeRead");
        public void ReadWelcome()
        {
            if(WelcomeRead)return;
            Knowledge.Observe("CH_01",new KnownRecord{Kind="Document",ProvenanceKey="WELCOME_"+World.Loop,Source="CH_01",SubjectId="WELCOME_CARD",Predicate="WelcomeRead",Value="Read",PlaceId=PlaceOf(bodies["CH_01"].transform.position),Text="현관의 안내문을 읽었다. 식사와 물은 공용 시설에서 이용할 수 있고, 개인실은 노크 후 허락을 구해야 한다.",FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"안내문에 적힌 생활 안내"},DoesNotEstablish=new[]{"다른 사람의 현재 위치나 행동"}},World.Tick);
        }
        public string SmallTalk(string actor,int topic)
        {
            if(World.Paused||!bodies.ContainsKey(actor)||actor=="CH_01"||topic<0||topic>2||!Reach(actor)||!World.Resident(actor).Alive)return "Unavailable";
            string text=EverydayDialogue.Line(actor,topic);
            string reason="CHAT_L"+World.Loop+"_"+actor+"_"+topic;
            return BeginConversation(actor,text,"Everyday_"+topic,EverydayDialogue.DurationTicks(actor,topic),reason);
        }
        string ManualPath(int slot)
        {
            if(slot<1||slot>10)throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(StorageDirectory,"manual-"+slot.ToString("00")+".dat");
        }
        public SaveSlotView[] ReadSaveSlots()
        {
            return Enumerable.Range(1,10).Select(i=>{
                string path=ManualPath(i);bool exists=File.Exists(path);string description="빈 슬롯";
                if(exists){
                    try{var saved=Store().Load(path);description="회차 "+saved.World.Loop+" · 챕터 "+saved.World.Chapter+"\n"+WorldTimeLabel.Format(saved.World.Tick,saved.World.ClockVersion)+"  /  "+File.GetLastWriteTime(path).ToString("MM.dd HH:mm");}
                    catch{description="읽을 수 없는 저장 · 이전 파일은 보존됨";}
                }
                return new SaveSlotView{Slot=i,Exists=exists,Description=description};
            }).ToArray();
        }
        public string SaveManual(int slot){try{SaveTo(ManualPath(slot));return "슬롯 "+slot+"에 저장했습니다.";}catch(Exception){return "저장하지 못했습니다. 기존 저장은 보존됩니다.";}}
        public string LoadManual(int slot){try{LoadFrom(ManualPath(slot));return "불러오기 완료";}catch(Exception){return "저장을 읽을 수 없습니다. 현재 진행을 유지합니다.";}}
    }
}
