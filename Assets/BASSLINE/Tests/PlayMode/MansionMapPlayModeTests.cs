using System;
using System.Collections;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
using BASSLINE.Core;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace BASSLINE.Tests
{
    public sealed class MansionMapPlayModeTests
    {
        MansionRuntime runtime;FixtureHud hud;
        [UnitySetUp] public IEnumerator Setup()
        {
            SceneManager.LoadScene("Mansion_Playable");yield return null;runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();hud.enabled=false;
            foreach(string owner in runtime.World.Capture().PauseOwners.ToArray())runtime.Pause(owner,false);
        }
        [UnityTest] public IEnumerator M01_MapRoomVolumeAloneCannotNameRoomsAndPauseCannotObserve()
        {
            Assert.That(runtime.ReadMap().Places,Is.Empty);
            runtime.SetLook(0,75);for(int i=0;i<30;i++)runtime.AdvanceOne();
            var map=runtime.ReadMap();Assert.That(map.Places.Count,Is.GreaterThan(0),"A visible fixed floor surface can establish a local anonymous space");
            Assert.That(map.Places.Count,Is.LessThan(runtime.Layout.Rooms.Length));Assert.That(map.Places.Any(p=>p.Label==runtime.Layout.Room("R_CLOSED").DisplayName),Is.False);
            Assert.That(map.Places.All(p=>p.Label.StartsWith("확인한 공간",StringComparison.Ordinal)),Is.True,"Looking down at the floor cannot read remote room signs");
            var before=runtime.ReadMap().Revision;runtime.Pause("K_NOTE",true);runtime.SetLook(0,0);for(int i=0;i<60;i++)runtime.AdvanceOne();Assert.That(runtime.ReadMap().Revision,Is.EqualTo(before));
            hud.Open(11);yield return null;var view=hud.GetComponentsInChildren<KnownMapView>(true).Single();Assert.That(view.MarkerCount,Is.GreaterThan(0));Assert.That(view.Information,Does.Contain("공표 도면 미수신"));
            Assert.That(view.IsPerspective,Is.True);Assert.That(view.SelfVisible,Is.True);
            float yaw=(float)runtime.ReadPlayer().Yaw;view.Zoom(1.3f);view.SelectNext(1);view.TogglePerspective();view.CenterOnSelf();Assert.That(runtime.ReadPlayer().Yaw,Is.EqualTo(yaw));
            Assert.That(runtime.ReadMap().Revision,Is.EqualTo(before),"Map controls cannot observe or turn the player.");
        }
        [UnityTest] public IEnumerator MapShowsOnlyReceivedCrossFloorConnectionsAndHidesSelfOnOtherFloors()
        {
            hud.Open(11);yield return null;var view=hud.GetComponentsInChildren<KnownMapView>(true).Single();
            // Presentation-only receipt fixture; this does not inject knowledge into the runtime.
            var places=new[]{new KnownMapPlace("LOW","아래 계단","F1","M01",0,0,true,"R1",0),new KnownMapPlace("HIGH","위 계단","F2","M01",0,2,true,"R2",1)};
            var map=new KnownMapSnapshot("CH_01","M01",1,places,new[]{new KnownMapConnection("LOW","HIGH","M01","R3",2)},Array.Empty<KnownMapDoor>(),Array.Empty<KnownMapPerson>(),"LOOP_01");
            string before=JsonUtility.ToJson(runtime.Knowledge.Capture());
            view.Bind(map,hud.KoreanFont,1);view.BindPose(new KnownMapPose{Available=true,Floor="F1",X=0,Z=0,Yaw=45});
            Assert.That(view.FloorConnectionCount,Is.EqualTo(1));Assert.That(view.SelfVisible,Is.True);
            view.CycleFloor(1);Assert.That(view.SelectedLayer,Is.EqualTo("M01/F2"));Assert.That(view.SelfVisible,Is.False);Assert.That(view.FloorConnectionCount,Is.EqualTo(1));
            view.CenterOnSelf();Assert.That(view.SelectedLayer,Is.EqualTo("M01/F1"));Assert.That(view.SelfVisible,Is.True);
            var withoutLink=new KnownMapSnapshot("CH_01","M01",2,places,Array.Empty<KnownMapConnection>(),Array.Empty<KnownMapDoor>(),Array.Empty<KnownMapPerson>(),"LOOP_01");
            view.Bind(withoutLink,hud.KoreanFont,1);Assert.That(view.FloorConnectionCount,Is.Zero,"Knowing two rooms is not knowing their connection.");
            Assert.That(JsonUtility.ToJson(runtime.Knowledge.Capture()),Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator DoorNamesAreReadFromTheirActualApproachSides()
        {
            var player=runtime.Bodies.Single(b=>b.ActorId=="CH_01");
            var door=runtime.Layout.Connections.Single(c=>c.Kind=="Door"&&c.RoomA=="R_VEST"&&c.RoomB=="R_HALL");
            for(int side=0;side<2;side++) {
                var face=door.transform.Find(side==0?"DoorSignA":"DoorSignB");
                Vector3 position=door.transform.position+door.NormalAToB*(side==0?-3.5f:3.5f);
                player.Capsule.enabled=false;player.transform.position=position;player.Capsule.enabled=true;
                runtime.World.Resident("CH_01").Position=MansionRuntime.P(position);Physics.SyncTransforms();
                var aim=Quaternion.LookRotation(face.position-(position+Vector3.up*player.Height*.91f)).eulerAngles;
                runtime.SetLook(aim.y,Mathf.DeltaAngle(0,aim.x));
                for(int tick=0;tick<30;tick++)runtime.AdvanceOne();
                string destination=side==0?"R_HALL":"R_VEST";
                Assert.That(runtime.Knowledge.For("CH_01").Records().Any(r=>r.Predicate=="MapNameRead"&&r.SubjectId==destination),Is.True,
                    "The visible destination sign must be readable from this side of the closed physical doorway: "+destination);
                if(side==0)Assert.That(runtime.Knowledge.For("CH_01").Records().Any(r=>r.Predicate=="MapNameRead"&&r.SubjectId=="R_VEST"),Is.False,
                    "The other face must not be read through the door.");
            }
            yield return null;
        }
    }
}
