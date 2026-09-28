using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BASSLINE.Bootstrap;
using BASSLINE.Presentation;
using UnityEngine.SceneManagement;
public sealed class BootstrapPlayModeTests
{
 [UnityTest] public IEnumerator AT_SPACE_02_03_HallSubsetPhysicalWalkAndVoid()
 {
  yield return SceneManager.LoadSceneAsync("Mansion_Main");
  var walker=Object.FindFirstObjectByType<ReviewWalkController>();walker.ReadInput=false;
  walker.ApplyMouseDelta(4,2);Assert.That(walker.transform.eulerAngles.y,Is.GreaterThan(0));var look=walker.transform.rotation;walker.SetReviewPause(true);walker.ApplyMouseDelta(20,20);Assert.That(walker.transform.rotation,Is.EqualTo(look));walker.SetReviewPause(false);walker.transform.rotation=Quaternion.identity;
  var capsule=walker.GetComponent<UnityEngine.CharacterController>();capsule.enabled=false;walker.transform.position=new Vector3(-.15f,.04f,2.2f);capsule.enabled=true;
  Physics.SyncTransforms();
  Assert.That(Physics.Raycast(new Vector3(-5,6.5f,0),Vector3.down,.8f),Is.False,"Central void has a balcony floor");
  Assert.That(Physics.Raycast(new Vector3(0,1,-10),Vector3.back,3),Is.False,"Entrance opening blocked");
  Assert.That(Physics.Raycast(new Vector3(12,1,0),Vector3.right,3),Is.False,"East hub opening blocked");
  Assert.That(Physics.Raycast(new Vector3(-9.5f,6.5f,0),Vector3.left,2),Is.True,"Balcony west rail missing");
  var route=new[]{new Vector2(6.4f,2.2f),new Vector2(6.4f,5.8f),new Vector2(.1f,5.8f),new Vector2(.1f,8),new Vector2(9.9f,8),new Vector2(9.9f,6.4f),new Vector2(11.6f,6.4f)};
  foreach(var target in route){
   int steps=0;
   while(Vector2.Distance(new Vector2(walker.transform.position.x,walker.transform.position.z),target)>.08f&&steps++<500){
    var delta=target-new Vector2(walker.transform.position.x,walker.transform.position.z);walker.MoveForReview(new Vector3(delta.x,0,delta.y).normalized,1f/60);if(steps%30==0)yield return null;
   }
   Assert.That(steps,Is.LessThan(500),"Blocked at "+walker.transform.position+" toward "+target);
  }
  Assert.That(walker.transform.position.y,Is.InRange(5.95f,6.15f),"Did not walk to balcony level");
  TestContext.Progress.WriteLine("ArtReview TestOnly capsule: entrance/east openings, central void ray, west rail ray, physical F1→F2 route PASS. All M01 doors/nav/rail continuity NOT COVERED.");
 }
 [UnityTest] public IEnumerator AT_TIME_02_AT_SAVE_02_BootstrapSubset()
 {
  var go=new GameObject("TestOnly_Bootstrap");var session=go.AddComponent<BootstrapSession>();session.CatalogHash="test";
  string path=Path.Combine(Application.temporaryCachePath,"bassline_playmode.dat");
  try{
   yield return new WaitForSecondsRealtime(.05f);Assert.That(session.WorldTick,Is.GreaterThan(0));
   session.AcquireReadPause("NOTE_01");session.AcquireReadPause("SETTINGS_01");long frozen=session.WorldTick;
   yield return new WaitForSecondsRealtime(.08f);Assert.That(session.WorldTick,Is.EqualTo(frozen));session.SaveBootstrap(path);
   session.ReleaseReadPause("NOTE_01");yield return null;Assert.That(session.WorldPaused,Is.True);
   session.ReleaseReadPause("SETTINGS_01");yield return new WaitForSecondsRealtime(.05f);Assert.That(session.WorldTick,Is.GreaterThan(frozen));
   session.LoadBootstrap(path);Assert.That(session.WorldTick,Is.EqualTo(frozen));Assert.That(session.WorldPaused,Is.True);
   yield return null;Assert.That(session.WorldTick,Is.EqualTo(frozen));
  }finally{Object.Destroy(go);foreach(string suffix in new[]{"",".previous",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
 }
}
