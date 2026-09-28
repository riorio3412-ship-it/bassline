using UnityEngine;
using BASSLINE.Core;
namespace BASSLINE.AuthoringData
{
 public sealed class FixtureIncidentDefinition:ScriptableObject
 {
  public string StableId="SO_K_INCIDENT_01",DecisionStatus="PRODUCTION_PROPOSAL_TestOnly",ArtStatus="FunctionalProxy";
  public IncidentSettings Settings=new IncidentSettings();
 }
}
