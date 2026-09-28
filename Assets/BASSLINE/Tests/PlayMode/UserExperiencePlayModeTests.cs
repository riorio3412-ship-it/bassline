using System;
using System.Collections;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Core;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class UserExperiencePlayModeTests
{
    [Test] public void AT_INPUT_02_LegacySettingsKeepCustomKeysAndGainRunning()
    {
        var legacy=new PlayerControls();legacy.Sensitivity=3.25f;legacy.Bindings=legacy.Bindings.Where(x=>x.Action!="Run").ToArray();
        Assert.That(legacy.Rebind("Interact",KeyCode.Q),Is.EqualTo("Applied"));
        var migrated=PlayerControls.FromJson(JsonUtility.ToJson(legacy),true);
        Assert.That(migrated.Key("Interact"),Is.EqualTo(KeyCode.Q));Assert.That(migrated.Key("Run"),Is.EqualTo(KeyCode.LeftShift));
        Assert.That(migrated.SensitivityY,Is.EqualTo(3.25f));Assert.That(migrated.Key("Focus"),Is.EqualTo(KeyCode.F));
        Assert.That(migrated.Bindings.Select(x=>x.Key).Distinct().Count(),Is.EqualTo(migrated.Bindings.Length));
        Assert.That(migrated.RunFovEffect,Is.False);
    }

    [Test] public void AT_INPUT_03_NewPreferencesRoundTripAndRepairInvalidFields()
    {
        var original=new PlayerControls{Sensitivity=1.25f,SensitivityY=3.75f,InvertX=true,InvertY=true,Fov=80,ToggleRun=true,UiScale=1.2f,FontScale=1.8f,MusicVolume=.25f,SfxVolume=.6f,TextSpeed=15,ResolutionWidth=1280,ResolutionHeight=720,Fullscreen=true,VSync=0};
        var result=PlayerControls.FromJson(JsonUtility.ToJson(original));
        Assert.That(JsonUtility.ToJson(result),Is.EqualTo(JsonUtility.ToJson(original)));
        result.Sensitivity=float.NaN;result.FontScale=100;result.Bindings=new[]{new ControlBinding{Action="Run",Key=KeyCode.Escape},new ControlBinding{Action="Interact",Key=KeyCode.W}};result.Validate();
        Assert.That(result.Sensitivity,Is.EqualTo(2));Assert.That(result.FontScale,Is.EqualTo(2));
        Assert.That(result.Bindings.Any(x=>x.Key==KeyCode.Escape||x.Key==KeyCode.None),Is.False);
        Assert.That(result.Bindings.Select(x=>x.Key).Distinct().Count(),Is.EqualTo(PlayerControls.Defaults().Length));
    }

    [UnityTest] public IEnumerator AT_INPUT_04_TolerantTargetStillRequiresLineOfSight()
    {
        var cameraObject=new GameObject("Interaction test camera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();cameraObject.transform.position=new Vector3(1000,1000,1000);
        var item=new GameObject("Small reachable target",typeof(BoxCollider),typeof(FixtureTarget));item.transform.position=cameraObject.transform.position+new Vector3(.18f,0,1.5f);item.GetComponent<BoxCollider>().size=Vector3.one*.08f;item.GetComponent<FixtureTarget>().StableId="UX_SMALL";
        GameObject wall=null;
        try
        {
            Physics.SyncTransforms();
            Func<string,InteractionView> describe=id=>new InteractionView{TargetId=id,Available=true,Label="작은 물체",PrimaryAction="관찰"};
            var candidate=InteractionPicker.Pick(camera,null,describe);Assert.That(candidate,Is.Not.Null,"Nearby small target must not require a pixel-perfect center hit");Assert.That(candidate.TargetId,Is.EqualTo("UX_SMALL"));
            wall=new GameObject("Opaque blocking wall",typeof(BoxCollider));wall.transform.position=cameraObject.transform.position+Vector3.forward*.7f;wall.GetComponent<BoxCollider>().size=new Vector3(2,2,.1f);Physics.SyncTransforms();
            Assert.That(InteractionPicker.Pick(camera,null,describe),Is.Null,"Candidate tolerance must never reach through the wall");
            wall.SetActive(false);Physics.SyncTransforms();
            Assert.That(InteractionPicker.Pick(camera,null,id=>new InteractionView{TargetId=id,Available=false}),Is.Null,"A visible but unavailable target must not become actionable");
            item.transform.position=cameraObject.transform.position+new Vector3(.18f,0,2.4f);Physics.SyncTransforms();Assert.That(InteractionPicker.Pick(camera,null,describe),Is.Null,"Candidate radius cannot extend interaction reach");
        }
        finally{UnityEngine.Object.Destroy(cameraObject);UnityEngine.Object.Destroy(item);if(wall)UnityEngine.Object.Destroy(wall);}
        yield return null;
    }

    [Test] public void AT_UI_02_ReduceMotionSuppressesRunFovWithoutChangingSensitivity()
    {
        var go=new GameObject("Settings camera",typeof(Camera));
        try
        {
            var camera=go.GetComponent<Camera>();var settings=new PlayerControls{Fov=70,RunFovEffect=true,RunFovAmount=6,ReduceMotion=true};
            PlayerPresentationSettings.UpdateCamera(camera,settings,true,.1f);Assert.That(camera.fieldOfView,Is.EqualTo(70));Assert.That(settings.Sensitivity,Is.EqualTo(2));
            settings.ReduceMotion=false;PlayerPresentationSettings.UpdateCamera(camera,settings,true,.1f);Assert.That(camera.fieldOfView,Is.GreaterThan(70));Assert.That(camera.fieldOfView,Is.LessThanOrEqualTo(76));
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
}
