using System.Collections;
using System.Linq;
using BASSLINE.Bootstrap;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BASSLINE.Tests
{
    public sealed partial class AuthoredStandingTests
    {
        [UnityTest] public IEnumerator JinwooVisualKeepsSpecifiedScaleAndPhysicalArmLengths()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            var runtime=Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;
            var actor=runtime.Bodies.Single(b=>b.ActorId=="CH_02");var visual=actor.transform.Find("CharacterProxy");
            Assert.That(actor.Height,Is.EqualTo(1.63f).Within(.001f));
            Assert.That(visual.GetComponentsInChildren<Collider>(),Is.Empty);
            Assert.That(visual.GetComponentsInChildren<Rigidbody>(),Is.Empty);
            Assert.That(visual.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh.name=="J19_Trouser_-1"),Is.True);
            var points=visual.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>actor.transform.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
            Assert.That(points.Min(p=>p.y),Is.InRange(-.001f,.01f));
            Assert.That(points.Max(p=>p.y),Is.EqualTo(1.63f).Within(.01f));
            var arms=actor.GetComponentsInChildren<BASSLINE.AuthoringData.ActorArmRig>();Assert.That(arms.Length,Is.EqualTo(2));
            foreach(var arm in arms)foreach(var local in new[]{new Vector3(arm.Side*.255f,actor.Height*.47f,.07f),new Vector3(arm.Side*.21f,actor.Height*.64f,.28f)}){
                arm.Pose(actor.transform.TransformPoint(local),actor.transform.rotation);
                Assert.That(Vector3.Distance(arm.Upper.position,arm.Forearm.position),Is.EqualTo(arm.UpperLength).Within(.0001f));
                Assert.That(Vector3.Distance(arm.Forearm.position,arm.Wrist.position),Is.EqualTo(arm.ForearmLength).Within(.0001f));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ProfileUsesNeutralGeometryWithoutChangingOrRevealingRemotePose()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            var runtime=Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;
            var actor=runtime.Bodies.Single(b=>b.ActorId=="CH_04");var visual=actor.transform.Find("CharacterProxy");
            visual.localRotation=Quaternion.Euler(80,0,0);actor.gameObject.SetActive(false);
            var slot=new GameObject("TestOnly neutral profile",typeof(RectTransform),typeof(UnityEngine.UI.Image));var portrait=slot.AddComponent<DialoguePortrait>();
            try{
                portrait.Show(actor.transform,slot.GetComponent<UnityEngine.UI.Image>(),false);
                var preview=(GameObject)typeof(DialoguePortrait).GetField("preview",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(portrait);
                Assert.That(preview,Is.Not.Null);var model=preview.transform.Find("CharacterProxy(Clone)");Assert.That(model,Is.Not.Null);
                Assert.That(Quaternion.Angle(model.rotation,Quaternion.identity),Is.LessThan(.01f));Assert.That(model.gameObject.activeInHierarchy,Is.True);
                Assert.That(actor.gameObject.activeSelf,Is.False);Assert.That(Quaternion.Angle(visual.localRotation,Quaternion.Euler(80,0,0)),Is.LessThan(.01f));
                Assert.That(preview.GetComponentsInChildren<BASSLINE.AuthoringData.FixtureActorBody>(),Is.Empty);yield return null;
            }finally{Object.Destroy(slot);}
        }
        [UnityTest] public IEnumerator SwitchingAuthoredResidentsKeepsBothSharedTexturesAlive()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");
            var runtime=Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;
            var first=Resources.Load<Texture2D>("BASSLINE/Standing/CH_02_v019");var second=Resources.Load<Texture2D>("BASSLINE/Standing/CH_04");
            Assert.That(first,Is.Not.Null);Assert.That(second,Is.Not.Null);
            var slot=new GameObject("TestOnly portrait slot",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            var portrait=slot.AddComponent<DialoguePortrait>();
            try{
                portrait.Show(runtime.Bodies.Single(b=>b.ActorId=="CH_02").transform,slot.GetComponent<UnityEngine.UI.Image>());
                Assert.That(slot.GetComponentInChildren<UnityEngine.UI.RawImage>().texture,Is.SameAs(first));
                portrait.Show(runtime.Bodies.Single(b=>b.ActorId=="CH_04").transform,slot.GetComponent<UnityEngine.UI.Image>());
                Assert.That(slot.GetComponentInChildren<UnityEngine.UI.RawImage>().texture,Is.SameAs(second));
                Object.Destroy(portrait);yield return null;
                Assert.That(first!=null&&second!=null,Is.True,"Closing a portrait must not destroy shared Resources assets");
            }finally{Object.Destroy(slot);}
        }
    }
}
