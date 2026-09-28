using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BASSLINE.AuthoringData;
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
        [UnityTest] public IEnumerator ContinuousSleevesKeepClosedTopologyAndCuffAttachmentAcrossPoses()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            var runtime=Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;
            var actor=runtime.Bodies.Single(b=>b.ActorId=="CH_02");
            var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();Assert.That(skins.Length,Is.EqualTo(2));
            foreach(var skin in skins){
                var arm=skin.GetComponentInParent<ActorArmRig>();Assert.That(arm,Is.Not.Null);
                var mesh=skin.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
                var edges=new Dictionary<(int,int),int>();
                void Edge(int a,int b){var key=a<b?(a,b):(b,a);edges[key]=edges.TryGetValue(key,out int n)?n+1:1;}
                for(int i=0;i<triangles.Length;i+=3){Edge(triangles[i],triangles[i+1]);Edge(triangles[i+1],triangles[i+2]);Edge(triangles[i+2],triangles[i]);}
                Assert.That(edges.Values.All(n=>n==2),Is.True,"The sleeve must be one closed surface, without an open elbow seam.");
                Assert.That(mesh.boneWeights.All(w=>Mathf.Abs(w.weight0+w.weight1-1)<.00001f),Is.True);
                Assert.That(skin.bones.All(b=>b.IsChildOf(actor.transform)),Is.True);
                float cuffY=vertices.Min(v=>v.y);var cuff=Enumerable.Range(0,vertices.Length).Where(i=>Mathf.Abs(vertices[i].y-cuffY)<.0001f).ToArray();
                var baked=new Mesh();
                try{
                    foreach(float yaw in new[]{0f,85f}){
                        actor.transform.rotation=Quaternion.Euler(0,yaw,0);
                        foreach(var target in new[]{new Vector3(arm.Side*.255f,1.63f*.47f,.07f),new Vector3(arm.Side*.22f,1.63f*.64f,.28f),new Vector3(arm.Side*.22f,1.63f*.72f,.29f)}){
                            arm.Pose(actor.transform.TransformPoint(target),actor.transform.rotation);skin.BakeMesh(baked);
                            var current=baked.vertices;Assert.That(current.Length,Is.EqualTo(vertices.Length));
                            Assert.That(current.All(p=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude)),Is.True);
                            foreach(int i in cuff){var expected=arm.Forearm.TransformPoint(mesh.bindposes[1].MultiplyPoint3x4(vertices[i]));Assert.That(Vector3.Distance(skin.transform.TransformPoint(current[i]),expected),Is.LessThan(.001f),"Cuff detached from the physical forearm.");}
                            foreach(var e in edges.Keys){float before=Vector3.Distance(vertices[e.Item1],vertices[e.Item2]),after=Vector3.Distance(current[e.Item1],current[e.Item2]);Assert.That(after,Is.InRange(before*.15f,before*2.5f),"An elbow edge collapsed or stretched excessively.");}
                            Assert.That(Vector3.Distance(arm.Upper.position,arm.Forearm.position),Is.EqualTo(arm.UpperLength).Within(.0001f));
                            Assert.That(Vector3.Distance(arm.Forearm.position,arm.Wrist.position),Is.EqualTo(arm.ForearmLength).Within(.0001f));
                        }
                    }
                }finally{Object.Destroy(baked);}
            }
            yield return null;
        }

        [UnityTest] public IEnumerator NeutralProfileRemapsSkinBonesWithoutReadingRemoteArmPose()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            var runtime=Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;
            var actor=runtime.Bodies.Single(b=>b.ActorId=="CH_02");var originalArms=actor.GetComponentsInChildren<ActorArmRig>();
            foreach(var arm in originalArms)arm.Pose(actor.transform.TransformPoint(new Vector3(arm.Side*.22f,1.17f,.29f)),actor.transform.rotation);
            var before=originalArms.Select(a=>a.Wrist.position).ToArray();actor.gameObject.SetActive(false);
            var slot=new GameObject("TestOnly skinned neutral profile",typeof(RectTransform),typeof(UnityEngine.UI.Image));var portrait=slot.AddComponent<DialoguePortrait>();
            try{
                portrait.Show(actor.transform,slot.GetComponent<UnityEngine.UI.Image>(),false);
                var preview=(GameObject)typeof(DialoguePortrait).GetField("preview",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(portrait);
                Assert.That(preview,Is.Not.Null);var skins=preview.GetComponentsInChildren<SkinnedMeshRenderer>();Assert.That(skins.Length,Is.EqualTo(2));
                foreach(var skin in skins)Assert.That(skin.bones.All(b=>b.IsChildOf(preview.transform)&&!b.IsChildOf(actor.transform)),Is.True);
                for(int i=0;i<originalArms.Length;i++)Assert.That(Vector3.Distance(originalArms[i].Wrist.position,before[i]),Is.LessThan(.00001f));
                Assert.That(actor.gameObject.activeSelf,Is.False);Assert.That(preview.GetComponentsInChildren<FixtureActorBody>(),Is.Empty);
            }finally{Object.Destroy(slot);}
            yield return null;
        }
    }
}
