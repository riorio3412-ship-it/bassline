using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        const string SpaceFlag="-bassline-space-review";
        IEnumerator ReviewMansionSpace()
        {
            receipt.Scope="TestOnly static viewpoints in the real standalone scene. Space readability review only; NOT natural route traversal, full progression or a performance benchmark. User preferences are never written.";
            receipt.MovementMethod="TestOnly placements on clear existing navigation nodes; no travelled-distance claim.";
            receipt.DialogueMethod="No dialogue exercise in space review.";
            driveWorld=false;driveMovement=false;
            Click("새로 시작");yield return null;Click("문을 열고 들어가기");yield return null;Require(CurrentScreen==1,"Welcome did not enter exploration.");receipt.TitleButtonEnteredPlay=true;
            var views=new[]{
                new SpaceView("entrance_sign","R_VEST",new Vector3(0,0,-15),new Vector3(0,2.5f,-11)),
                new SpaceView("hall_depth","R_HALL",new Vector3(-4,0,-5),new Vector3(4,3,5.8f)),
                new SpaceView("grand_stair","R_HALL",new Vector3(0,0,0),new Vector3(5,3,3.5f)),
                new SpaceView("upper_balcony","R_BALCONY",new Vector3(11.7f,6,0),new Vector3(2,3,3.5f)),
                new SpaceView("east_corridor","R_CE",new Vector3(17,0,0),new Vector3(30,1.7f,0)),
                new SpaceView("west_corridor","R_CW",new Vector3(-17,0,0),new Vector3(-30,1.7f,0)),
                new SpaceView("living_corridor","R_LIV_COR",new Vector3(30,0,12),new Vector3(30,1.7f,23)),
                new SpaceView("service_stair_2f","R_STAIR",new Vector3(-9.4f,6,16),new Vector3(-10.37f,7.6f,16))
            };
            var station=FindAnyObjectByType<BASSLINE.AuthoringData.CommonReturnStation>();
            if(station)views=views.Concat(new[]{new SpaceView("common_return_tray","R_WORK",station.TrayPoint.position-new Vector3(.2f,.973f,.6f),station.TrayPoint.position),new SpaceView("public_drawer","R_WORK",station.DrawerPoint.position-new Vector3(.2f,1.04f,.6f),station.DrawerPoint.position)}).ToArray();
            foreach(var view in views) {
                var candidates=runtime.Layout.NavigationNodes.Select((n,i)=>new{Node=n,Index=i})
                    .Where(n=>n.Node.RoomId==view.Room).OrderBy(n=>Vector3.Distance(n.Node.Position,view.Position));
                var chosen=candidates.FirstOrDefault(n=>PlayerCapsuleClear(n.Node.Position));
                Require(chosen!=null,"No physically clear viewpoint in "+view.Room);
                placements.Add(new PlacementReceipt{Purpose="TestOnly static space review: "+view.Name,From=player.transform.position,To=chosen.Node.Position});
                SetTestOnlyPlayerPosition(chosen.Node.Position,chosen.Index);
                if(runtime.World.HasPause("K_WINDOW_FOCUS"))Call(hud,"SyncPauseView");
                var direction=view.Target-(chosen.Node.Position+Vector3.up*player.Height*.91f);
                var rotation=Quaternion.LookRotation(direction).eulerAngles;
                runtime.SetLook(rotation.y,Mathf.DeltaAngle(0,rotation.x));
                yield return null;
                yield return CaptureAtBothResolutions("space_"+view.Name+"_testonly",1);
            }
            receipt.Status="PASS_SMOKE_ONLY";
        }
        sealed class SpaceView
        {
            public string Name,Room;public Vector3 Position,Target;
            public SpaceView(string name,string room,Vector3 position,Vector3 target){Name=name;Room=room;Position=position;Target=target;}
        }
    }
}
