using System;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using UnityEngine;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        int personIndex;
        void RenderPeople(ProductionScreenView view,NotebookView data,Action<string,Action> button,ref string title,ref string body,ref string context)
        {
            var known=data.Locations.Where(l=>l.ActorId!="CH_01").Select(l=>l.ActorId)
                .Concat(data.Records.Where(r=>r.Predicate=="SaidStatement"&&(r.Source??"").StartsWith("CH_",StringComparison.Ordinal)&&r.Source!="CH_01").Select(r=>r.Source)).Distinct().OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            if(known.Length==0){body="아직 얼굴을 익힌 사람이 없습니다.\n\n가까운 사람에게 말을 걸어 보세요. 이 페이지에는 직접 만나거나 이야기를 나눈 사람이 남습니다.";context="직접 만난 인물  0\n\n마지막 확인 장소는 현재 위치와 다를 수 있습니다.";return;}
            personIndex=(personIndex+known.Length)%known.Length;string id=known[personIndex],name=ActorLabel(id);
            var location=data.Locations.FirstOrDefault(l=>l.ActorId==id);
            var statement=data.Records.Reverse().Where(r=>r.Predicate=="SaidStatement"&&r.Source==id).OrderByDescending(r=>r.ReceivedTick).FirstOrDefault();
            title=name;body="<color=#80C9CD>인물 기록   "+(personIndex+1).ToString("00")+" / "+known.Length.ToString("00")+"</color>\n\n"+(statement==null?"아직 나눈 대화가 없습니다.":"“"+statement.Text+"”");
            context="마지막으로 본 곳\n"+(location==null?"직접 확인한 위치가 없습니다.":Place(location.PlaceId)+"\n"+TimeLabel(location.Tick))+"\n\n우리 사이에 남은 일\n";
            var experiences=data.RelationshipExperiences.Where(e=>e.StartsWith(name+" ·",StringComparison.Ordinal)).TakeLast(6).ToArray();
            context+=experiences.Length==0?"아직 함께한 기록이 없습니다.":string.Join("\n",experiences);
            context+="\n\n지금도 같은 곳에 있다는 뜻은 아닙니다.";
            view.EnsureProfilePortrait();var actor=FindObjectsByType<FixtureTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.StableId==id);
            if(actor){if(!portrait)portrait=gameObject.AddComponent<DialoguePortrait>();portrait.Show(actor.transform,view.Standing,false);}
            button("이전 인물",()=>personIndex=(personIndex+known.Length-1)%known.Length);button("다음 인물",()=>personIndex=(personIndex+1)%known.Length);
            button("대화 찾아보기",()=>{recordCategory=2;selectedRecord=Array.FindIndex(data.Records.Reverse().OrderByDescending(r=>r.ReceivedTick).ToArray(),r=>r.Id==statement?.Id);OpenNotePage(10);});
        }
    }
}
