using UnityEngine;
using UnityEngine.SceneManagement;
namespace BASSLINE.Bootstrap
{
 // Deliberate checkpoint navigation. Changing scenes starts a fresh TestOnly session;
 // F5/F9 remains the explicit save/load path. Never installed into production scenes.
 public sealed class TestOnlySceneNavigation:MonoBehaviour
 {
  void Update(){if(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)){
   if(Input.GetKeyDown(KeyCode.F1))SceneManager.LoadScene("FixtureK_Life");
   if(Input.GetKeyDown(KeyCode.F3))SceneManager.LoadScene("FixtureK_Incident");if(Input.GetKeyDown(KeyCode.F2))SceneManager.LoadScene("Mansion_Main");
  }}
 }
}

