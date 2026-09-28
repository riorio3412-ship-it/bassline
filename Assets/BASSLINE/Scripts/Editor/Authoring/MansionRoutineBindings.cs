using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;

namespace BASSLINE.Authoring
{
    // Source ordinary-life preferences, not fixed future events. Order, dwell and socket allocation
    // are replaceable production proposals; arrival still uses the real walk graph and controller.
    public static class MansionRoutineBindings
    {
        sealed class Stop
        {
            public string Room,Activity,AnchorKind;
            public Stop(string room,string activity,string anchorKind){Room=room;Activity=activity;AnchorKind=anchorKind;}
        }
        static Stop S(string room,string activity,string anchorKind)=>new Stop("R_"+room,activity,anchorKind);
        static readonly Stop[][] preferences={
            new[]{S("LIBRARY","Read","Read"),S("DINING","Talk","Seat")},
            new[]{S("HALL","Review","Seat"),S("DINING","Prepare","EatTalk"),S("LIBRARY","Read","Read")},
            new[]{S("EXHIBIT","Examine","PrimaryActivity"),S("WORK","Restore","Work"),S("DINING","Tea","Seat")},
            new[]{S("DINING","Talk","Seat"),S("HALL","Meeting","Seat"),S("BED_05","Organize","Store"),S("POOL","Rest","Seat")},
            new[]{S("WORK","Inventory","Work"),S("DINING","Inventory","EatTalk")},
            new[]{S("LIBRARY","Read","Read"),S("DINING","Eat","Seat"),S("ARCADE","Game","PrimaryActivity")},
            new[]{S("ARCADE","Rhythm","PrimaryActivity"),S("DINING","Rest","Seat")},
            new[]{S("LOUNGE","Rest","Seat"),S("DINING","Talk","Seat")},
            new[]{S("KITCHEN","Cook","PrimaryActivity"),S("DINING","Serve","EatTalk"),S("GREEN","WaterPlants","PrimaryActivity")},
            new[]{S("WORK","Repair","Work"),S("DINING","Drink","Seat")},
            new[]{S("BED_12","Organize","Store"),S("DINING","Talk","Seat"),S("LOUNGE","Rest","Seat")},
            new[]{S("ARCADE","Practice","PrimaryActivity"),S("DINING","Eat","Seat"),S("BED_13","Review","Read")},
            new[]{S("GARDEN","Walk","PrimaryActivity"),S("DINING","Tea","Seat"),S("WORK","Craft","Work")},
            new[]{S("HALL","Review","Seat"),S("DINING","Coffee","Seat"),S("BED_15","Review","Read")},
            new[]{S("BED_16","Organize","Store"),S("EXHIBIT","Examine","PrimaryActivity"),S("BED_16","Rest","Rest")},
            new[]{S("WORK","Craft","Work"),S("HALL","Invite","Seat"),S("ARCADE","Game","PrimaryActivity")},
            new[]{S("WORK","Carry","Work"),S("DINING","Drink","Seat"),S("GREEN","Walk","PrimaryActivity"),S("ARCADE","Puzzle","PrimaryActivity")}
        };
        [Serializable] public sealed class Binding
        {
            public string ActorId,SourceParagraph,RoomId,Activity,AnchorId,AnchorType,FurnitureId,NodeId;
            public float PassageClearance;
        }
        [Serializable] sealed class Receipt
        {
            public string Scope="Source P0772–P1140 ordinary-life choices; exact order/dwell/socket are PRODUCTION PROPOSAL. Craft visits can reach for a recently seen nearby loose marking tool, grasp and carry it, attempt physical pigment contact, and return it to its original supported place. Other routines remain timed Performing; no completed cooking or repair claim.";
            public Binding[] Bindings;
        }
        public static ResidentRoutine Build(MansionLayout layout,int number,List<Binding> receipt)
        {
            if(number<2||number>18)throw new ArgumentOutOfRangeException(nameof(number));
            string id="CH_"+number.ToString("00");var stops=preferences[number-2].ToList();
            if(!stops.Any(s=>s.Room=="R_BED_"+number.ToString("00")&&s.Activity=="Rest"))stops.Add(S("BED_"+number.ToString("00"),"Rest","Rest"));
            var destinations=new List<string>();var activities=new List<string>();
            foreach(var stop in stops)
            {
                bool marking=stop.Room=="R_WORK"&&stop.Activity=="Craft";
                var candidates=layout.Anchors.Where(a=>a.RoomId==stop.Room&&a.InteractionType==stop.AnchorKind&&a.ApproachNode>=0
                    &&(!marking||a.FurnitureId=="OBJ_R_WORK_Workbench0")
                    &&PassageClearance(layout,layout.NavigationNodes[a.ApproachNode].Position)>=.9f)
                    .OrderBy(a=>a.AnchorId,StringComparer.Ordinal).ToArray();
                if(candidates.Length==0)throw new InvalidOperationException(id+" has no "+stop.AnchorKind+" activity anchor clear of passage routes in "+stop.Room);
                var anchor=candidates[(number-2)%candidates.Length];var node=layout.NavigationNodes[anchor.ApproachNode];
                if(node.RoomId!=stop.Room)throw new InvalidOperationException("Activity approach left its room: "+anchor.AnchorId);
                string activity=marking?"MarkSurface:M_WORK_MARKING_BOARD":stop.Activity;
                destinations.Add(node.Id);activities.Add(activity);
                receipt.Add(new Binding{ActorId=id,SourceParagraph="P"+(772+(number-2)*23).ToString("0000"),RoomId=stop.Room,Activity=activity,AnchorId=anchor.AnchorId,AnchorType=anchor.InteractionType,FurnitureId=anchor.FurnitureId,NodeId=node.Id,PassageClearance=PassageClearance(layout,node.Position)});
            }
            return new ResidentRoutine{ActorId=id,Destinations=destinations.ToArray(),Activities=activities.ToArray(),SurfaceToolId=stops.Any(s=>s.Room=="R_WORK"&&s.Activity=="Craft")?"M_BLUE_MARKING_CHALK":"",ReadingItemId=number==8?"M_BOOK":"",DwellTicks=1800+number*90};
        }
        static float PassageClearance(MansionLayout layout,Vector3 point)
        {
            // A dwell must leave .75 m passage clearance even at the .14 m arrival tolerance.
            // Furniture whose nearest approach is a portal node remains inspectable, but cannot
            // be selected as a stationary routine destination that blocks the entire doorway.
            float closest=float.MaxValue;
            foreach(var connection in layout.Connections)
            {
                var route=connection.Route;if(route==null||route.Length==0)continue;
                closest=Mathf.Min(closest,Vector3.Distance(point,route[0]));
                for(int i=1;i<route.Length;i++)
                {
                    Vector3 segment=route[i]-route[i-1];float t=segment.sqrMagnitude>1e-6f?Mathf.Clamp01(Vector3.Dot(point-route[i-1],segment)/segment.sqrMagnitude):0;
                    closest=Mathf.Min(closest,Vector3.Distance(point,route[i-1]+segment*t));
                }
            }
            return closest;
        }
        public static void WriteReceipt(List<Binding> bindings)
        {
            Directory.CreateDirectory("Verification");File.WriteAllText("Verification/mansion-routine-bindings.json",JsonUtility.ToJson(new Receipt{Bindings=bindings.ToArray()},true));
        }
    }
}
