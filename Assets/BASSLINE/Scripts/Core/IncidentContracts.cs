using System;
namespace BASSLINE.Core
{
 [Serializable] public sealed class IncidentSettings
 {
  public string Id="K_INCIDENT_SETUP_01",Version="FixtureK_Contact_001",Source="SRC11_P1636_P1638",Template="X31",Actor="CH_04",Target="CH_02",Object="K_O31",Approach="K_SEAT_W_01";
  public long StartTick=32400,ApproachTick=39600,EarliestCauseTick=43500,EarliestDepartureTick=45000,OpportunityEndTick=57600;
  public int ContactTicks=30,DelayTicks=300,FatalityCap=1;
  public IncidentSettings Copy()=>(IncidentSettings)MemberwiseClone();
 }
 public interface IIncidentPhysics
 {
  bool HasContact(string actor,string target,string item,out Point3 point);
  string[] ContactObservers(string actor,string target,string item);
  bool SeesSubject(string observer,string subject);
 }
 public sealed class CasePlayerView {public bool Discovered;public string Summary;public string[] RecordIds=Array.Empty<string>();}
}


