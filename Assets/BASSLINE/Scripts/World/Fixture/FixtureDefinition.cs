using System;
using System.Collections.Generic;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Fixture
{
 public sealed class RouteEdge
 {
  public string Id,A,B,DoorId;public Point3[] Points;
  public double Length=>Enumerable.Range(1,Points.Length-1).Sum(i=>Points[i-1].Distance(Points[i]));
 }
 public sealed class FixtureAnchor {public string Id,Node,Room;public Point3 Position;}
 public sealed class FixtureActivity {public string Id;public int Ticks;}
 public static class FixtureDefinition
 {
  public const string MapVersion="FixtureK_Life_004",RuleVersion="FixtureK_P1_004";
  public const int TickRate=60;
  public const double WalkSpeed=1.4;
  public static readonly string[] Actors={"CH_01","CH_02","CH_03","CH_04","CH_11","CH_18"};
  public static readonly Dictionary<string,Point3> Nodes=new Dictionary<string,Point3>(StringComparer.Ordinal){
   {"K_H",P(0,0)},{"K_GATE_H",P(0,4)},{"K_N",P(28,4)},{"K_S",P(28,-4)},{"K_W",P(28,0)},{"K_GATE_L",P(-28,4)},{"K_L",P(-28,0)},{"K_GATE_G",P(0,60)},{"K_G",P(0,64)}};
  public static readonly RouteEdge[] Edges={
   E("K_H_EXIT","K_H","K_GATE_H",null,P(0,0),P(0,4)),
   E("K_ROUTE_N","K_GATE_H","K_N",null,P(0,4),P(0,11),P(28,11),P(28,4)),
   E("K_ROUTE_S","K_GATE_H","K_S",null,P(0,4),P(-4,4),P(-4,-10),P(28,-10),P(28,-4)),
   E("K_N_ENTRY","K_N","K_W","K_DOOR_N",P(28,4),P(28,0)),
   E("K_S_ENTRY","K_S","K_W","K_DOOR_S",P(28,-4),P(28,0)),
   E("K_ROUTE_L","K_GATE_H","K_GATE_L",null,P(0,4),P(-28,4)),
   E("K_L_ENTRY","K_GATE_L","K_L",null,P(-28,4),P(-28,0)),
   E("K_ROUTE_G","K_GATE_H","K_GATE_G",null,P(0,4),P(0,60)),
   E("K_G_ENTRY","K_GATE_G","K_G",null,P(0,60),P(0,64))};
  public static readonly FixtureAnchor[] Anchors=new[]{"H","W","L","G"}.SelectMany(room=>new[]{1,2}.Select(n=>new FixtureAnchor{Id="K_SEAT_"+room+"_0"+n,Node="K_"+room,Room="K_"+room,Position=Nodes["K_"+room].Plus(P(n==1?-1.35:1.35,-1.1))}))
   .Concat(new[]{
    A("K_PASS_W_01","K_W",26.5,-1.8),A("K_PASS_W_02","K_W",29.5,-1.8),A("K_PASS_W_03","K_W",26.5,.8),
    A("K_PASS_N_01","K_N",27.35,10.3),A("K_PASS_N_02","K_N",28.65,10.3),A("K_PASS_N_03","K_N",28,8.5)}).ToArray();
  // TestOnly egress markers let six simultaneous passage requests reserve distinct
  // destinations without pretending they are furniture or adding a fifth room.
  static FixtureAnchor A(string id,string node,double x,double z)=>new FixtureAnchor{Id=id,Node=node,Room="K_W",Position=P(x,z)};
  public static readonly FixtureActivity[] Activities=new[]{"IDLE","READ","WORK","REST","DRINK","EAT","OBSERVE","TIDY","WAIT","TALK","STRETCH","WATER_PLANTS"}.Select((x,i)=>new FixtureActivity{Id="ACT_"+x,Ticks=(20+i*5)*60}).Concat(new[]{new FixtureActivity{Id="ACT_PASSAGE",Ticks=1}}).ToArray();
  public static Point3 P(double x,double z)=>new Point3(x,0,z);
  static RouteEdge E(string id,string a,string b,string door,params Point3[] points)=>new RouteEdge{Id=id,A=a,B=b,DoorId=door,Points=points};
  public static List<RouteEdge> Route(string from,string to,IEnumerable<string> blocked)
  {
   var blocks=new HashSet<string>(blocked);var distance=Nodes.Keys.ToDictionary(x=>x,x=>double.PositiveInfinity);var previous=new Dictionary<string,RouteEdge>();var visited=new HashSet<string>();distance[from]=0;
   while(true){var current=distance.Where(x=>!visited.Contains(x.Key)).OrderBy(x=>x.Value).ThenBy(x=>x.Key,StringComparer.Ordinal).FirstOrDefault();if(current.Key==null||double.IsPositiveInfinity(current.Value))return null;if(current.Key==to)break;visited.Add(current.Key);
    foreach(var edge in Edges.Where(e=>(e.A==current.Key||e.B==current.Key)&&!blocks.Contains(e.DoorId??""))){string next=edge.A==current.Key?edge.B:edge.A;double cost=current.Value+edge.Length;if(cost<distance[next]){distance[next]=cost;previous[next]=edge;}}
   }
   var result=new List<RouteEdge>();string node=to;while(node!=from){var edge=previous[node];result.Add(edge);node=edge.A==node?edge.B:edge.A;}result.Reverse();return result;
  }
  public static HashSet<string> WalkCells()
  {
   var cells=new HashSet<string>();foreach(var room in new[]{"K_H","K_W","K_L","K_G"}){var p=Nodes[room];for(int x=-3;x<3;x++)for(int z=-3;z<3;z++)cells.Add(((int)p.X+x)+","+((int)p.Z+z));}
   foreach(var edge in Edges)for(int n=1;n<edge.Points.Length;n++){
    var a=edge.Points[n-1];var b=edge.Points[n];int x0=(int)Math.Min(a.X,b.X),x1=(int)Math.Max(a.X,b.X),z0=(int)Math.Min(a.Z,b.Z),z1=(int)Math.Max(a.Z,b.Z);
    if(x0==x1){x0--;x1++;}else{z0--;z1++;}for(int x=x0;x<x1;x++)for(int z=z0;z<z1;z++)cells.Add(x+","+z);
   }
   // Include the full turn square: the centre-line vertex must not lie on the
   // concave wall corner of two half-open integer rectangles.
   foreach(var p in Edges.SelectMany(e=>e.Points))for(int x=(int)p.X-1;x<=(int)p.X;x++)for(int z=(int)p.Z-1;z<=(int)p.Z;z++)cells.Add(x+","+z);
   return cells;
  }
 }
}

