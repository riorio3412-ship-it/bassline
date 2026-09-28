using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering.Universal;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
using BASSLINE.UI;
using BASSLINE.Save;

namespace BASSLINE.Authoring
{
    public static class ProductionGameSetup
    {
        public const string ScenePath="Assets/BASSLINE/Scenes/Mansion_Playable.unity";
        const string Version="M01_FamilyConversation_031A";
        [MenuItem("BASSLINE/Production/9 Build playable mansion")]
        public static void Build()
        {
            ProductionImporter.Import();Directory.CreateDirectory("Verification");
            string hash=AtomicSaveStore.Hash(Version+ProductionImporter.CatalogHash()+MansionBuilder.BuilderVersion+MansionBuilder.AppearanceVersion);
            if(!ProductionImporter.CanWrite(ProductionImporter.LoadIndex(),"SCN_MANSION_PLAYABLE",ScenePath,hash))return;
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(!Application.isBatchMode&&active.isDirty)throw new InvalidOperationException("Save current scene before building.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try{
                var layout=MansionBuilder.BuildIntoActiveScene();MansionWashStationBuilder.Build(layout);
                var runtime=new GameObject("MansionSession").AddComponent<MansionRuntime>();runtime.Layout=layout;
                var definitions=PackageData.Load(ProductionImporter.SourceRoot).Characters;
                var residents=new List<FixtureActorBody>();var routines=new List<ResidentRoutine>();
                var bindings=new List<MansionRoutineBindings.Binding>();
                foreach(var row in definitions){string id=row["CharacterID"];int number=int.Parse(id.Substring(3));
                    var room=layout.Room(number==1?"R_VEST":"R_BED_"+number.ToString("00"));if(room==null||room.WalkNode<0)throw new InvalidOperationException("No spawn route: "+id);
                    Vector3 spawn=layout.NavigationNodes[room.WalkNode].Position;
                    var root=new GameObject(id+"_"+row["Name"]);root.transform.position=spawn;
                    var body=root.AddComponent<FixtureActorBody>();body.ActorId=id;body.Height=(float)row.Number("HeightCm")/100;
                    var controller=root.AddComponent<UnityEngine.CharacterController>();controller.height=body.Height;controller.radius=.28f;controller.center=Vector3.up*body.Height*.5f;controller.skinWidth=.025f;controller.stepOffset=.32f;controller.minMoveDistance=0;body.Capsule=controller;
                    var target=root.AddComponent<FixtureTarget>();target.StableId=id;target.PublicName=row["Name"];
                    body.Head=Child(root.transform,"Head",Vector3.up*body.Height*.88f);body.RightHand=Child(root.transform,"RightHand",new Vector3(.26f,body.Height*.56f,.16f));body.LeftHand=Child(root.transform,"LeftHand",new Vector3(-.26f,body.Height*.56f,.16f));
                    if(number!=1)CreateCharacter(body,number);else CharacterPresentationBuilder.PlayerArms(body);
                    residents.Add(body);
                    if(number==8){var request=root.AddComponent<MansionEverydayRequest>();request.Definition=new BASSLINE.Core.EverydayRequestDefinition{
                        Id="LIFE_BRING_COMMON_BOOK",Revision="1",Source="BL22 생활 요청 규칙 기반 신규 콘텐츠 초안",Requester=id,ItemId="M_BOOK",Title="라온에게 공용 책 가져다주기",
                        Offer="잠깐 읽을 만한 게 있으면 좋겠어.\n다니다가 공용 책을 찾으면 가져다줄래?\n누가 쓰고 있으면 기다려도 되고. 급한 건 아니야.",
                        Accepted="고마워. 어디 있는지는 나도 몰라.\n찾으면 내 손에 건네줘. 다른 사람이 보고 있으면 빼앗지는 말고.",
                        Deferred="응. 네 일부터 해.\n나중에 생각나면 다시 물어봐 줘.",Declined="괜찮아. 부탁은 거절할 수도 있는 거지.\n다른 이야기를 하자.",
                        Cancelled="알았어. 맡아 줬던 것만으로도 고마워.\n그 일 때문에 무리하지는 마.",Received="책 잘 받았어. 직접 가져다줘서 고마워.\n잠깐 앉아서 읽어 볼게."};}
                    if(number>1)routines.Add(MansionRoutineBindings.Build(layout,number,bindings));
                }
                runtime.Bodies=residents.ToArray();runtime.Routines=routines.ToArray();
                MansionRoutineBindings.WriteReceipt(bindings);
                runtime.Presenter=CreatePresenter(layout);
                foreach(var d in layout.Connections.Where(d=>!string.IsNullOrEmpty(d.DoorId))){var target=d.gameObject.AddComponent<FixtureTarget>();target.StableId=d.DoorId;target.PublicName=(layout.Room(d.RoomB)?.DisplayName??d.RoomB)+" 문";}
                foreach(var a in layout.Anchors){if(a.ApproachNode<0)continue;var target=a.gameObject.AddComponent<FixtureTarget>();target.StableId=a.AnchorId;target.PublicName=(layout.Room(a.RoomId)?.DisplayName??a.RoomId)+" · "+a.InteractionType;var hit=a.gameObject.AddComponent<SphereCollider>();hit.radius=.35f;hit.center=Vector3.up*.8f;hit.isTrigger=true;}
                runtime.ObjectBodies=MakeObjects(layout);
                CommonReturnStationBuilder.Build(layout);
                // Observation eyes are authored in dark corners; no surveillance station.
                var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.SetParent(residents[0].transform,false);camera.transform.localPosition=Vector3.up*residents[0].Height*.91f;camera.fieldOfView=65;camera.nearClipPlane=.05f;camera.farClipPlane=250;camera.gameObject.AddComponent<AudioListener>();camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                ProductionUiBuilder.Attach(runtime,camera);
                UnityEngine.Object.FindObjectsByType<FixtureHud>().Single(h=>h.Source==runtime).StartWithMenu=true;
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                ProductionImporter.Track(ProductionImporter.LoadIndex(),"SCN_MANSION_PLAYABLE",ScenePath,hash,"FunctionalProxy_LifeIntegration");
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
                File.WriteAllText("Verification/mansion-integration.json",JsonUtility.ToJson(new SetupReceipt{Editor=Application.unityVersion,Scene=ScenePath,Residents=runtime.Bodies.Length,Rooms=layout.Rooms.Length,Connections=layout.Connections.Length,Anchors=layout.Anchors.Length,Notes="Proxy characters/materials; gameplay validation is separate from generation."},true));
            }finally{if(!Application.isBatchMode){EditorSceneManager.CloseScene(scene,true);if(active.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);}}
        }
        static FixtureObjectBody[] MakeObjects(MansionLayout layout)
        {
            var list=new List<FixtureObjectBody>();foreach(var spec in new[]{new[]{"M_BOOK","공용 책","R_LIBRARY"},new[]{"M_CUP","공용 컵","R_DINING"},new[]{"M_NOTEBOOK","공용 수첩","R_HALL"}}){
                var room=layout.Room(spec[2]);var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=spec[0];go.transform.localScale=new Vector3(.24f,.1f,.3f);
                // Starting loose props belong on existing furniture, never on a mandatory navigation waypoint.
                // In particular the library's walk point is the stair/door landing; a .2m book there trapped shorter capsules.
                if(spec[0]=="M_NOTEBOOK")go.transform.position=layout.Anchors.Where(a=>a.RoomId==room.RoomId&&a.InteractionType=="Seat").OrderBy(a=>a.AnchorId,StringComparer.Ordinal).First().transform.position+Vector3.up*.055f;
                else{
                    string furniture=spec[0]=="M_BOOK"?"OBJ_R_LIBRARY_ReadingTable0":"OBJ_R_DINING_DiningTable0";
                    var support=room.GetComponentsInChildren<MansionProp>().Single(p=>p.ObjectId==furniture).transform.Find("Top");
                    go.transform.position=support.position+Vector3.up*.095f;
                }
                var b=go.AddComponent<FixtureObjectBody>();b.ObjectId=spec[0];b.Collider=go.GetComponent<Collider>();var target=go.AddComponent<FixtureTarget>();target.StableId=spec[0];target.PublicName=spec[1];if(spec[0]=="M_BOOK")MansionReadableBookBuilder.AddPage(b);list.Add(b);
            }
            var pen=new GameObject("M_TAEGYEOM_PEN");pen.transform.position=layout.Room("R_BED_06").WalkPoint+Vector3.up;
            var penHit=pen.AddComponent<BoxCollider>();penHit.size=new Vector3(.018f,.018f,.17f);
            var penBody=pen.AddComponent<FixtureObjectBody>();penBody.ObjectId=pen.name;penBody.InitialOwner="CH_06";penBody.Collider=penHit;
            var penTarget=pen.AddComponent<FixtureTarget>();penTarget.StableId=pen.name;penTarget.PublicName="흠집 난 푸른 펜";
            string penMaterialPath="Assets/BASSLINE/Art/Materials/M_PenBlue.mat";Directory.CreateDirectory(Path.GetDirectoryName(penMaterialPath));
            var penMaterial=AssetDatabase.LoadAssetAtPath<Material>(penMaterialPath);
            if(!penMaterial){penMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));penMaterial.SetColor("_BaseColor",new Color(.12f,.28f,.44f));penMaterial.SetFloat("_Smoothness",.55f);AssetDatabase.CreateAsset(penMaterial,penMaterialPath);}
            Part(pen.transform,"Barrel",Vector3.zero,new Vector3(.012f,.085f,.012f),penMaterial,PrimitiveType.Cylinder);pen.transform.Find("Barrel").localRotation=Quaternion.Euler(90,0,0);
            Part(pen.transform,"Cap",new Vector3(0,0,-.059f),new Vector3(.016f,.025f,.016f),penMaterial,PrimitiveType.Cylinder);pen.transform.Find("Cap").localRotation=Quaternion.Euler(90,0,0);
            Part(pen.transform,"Clip",new Vector3(.009f,0,-.056f),new Vector3(.003f,.004f,.04f),penMaterial,PrimitiveType.Cube);
            string silverPath="Assets/BASSLINE/Art/Materials/M_PenSilver.mat";var silver=AssetDatabase.LoadAssetAtPath<Material>(silverPath);
            if(!silver){silver=new Material(Shader.Find("Universal Render Pipeline/Lit"));silver.SetColor("_BaseColor",new Color(.75f,.78f,.8f));silver.SetFloat("_Metallic",.7f);silver.SetFloat("_Smoothness",.65f);AssetDatabase.CreateAsset(silver,silverPath);}
            pen.transform.Find("Clip").GetComponent<Renderer>().sharedMaterial=silver;
            Part(pen.transform,"Cap wear mark",new Vector3(-.004f,.0075f,-.051f),new Vector3(.007f,.001f,.002f),silver,PrimitiveType.Cube);pen.transform.Find("Cap wear mark").localRotation=Quaternion.Euler(0,25,0);
            Part(pen.transform,"Tip",new Vector3(0,0,.083f),new Vector3(.006f,.009f,.006f),silver,PrimitiveType.Cylinder);pen.transform.Find("Tip").localRotation=Quaternion.Euler(90,0,0);
            penBody.Loan=BASSLINE.Core.ItemLoanTerms.Pen();list.Add(penBody);list.AddRange(MansionLoanItemsBuilder.Build(layout));list.Add(AppointmentDeskBuilder.Build(layout));list.Add(MansionTraceToolsBuilder.Build(layout));list.AddRange(MansionWeaponBuilder.Build(layout));return list.ToArray();
        }
        static Transform Child(Transform parent,string name,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        static FixtureActorBody CreatePresenter(MansionLayout layout)
        {
            var root=new GameObject("PRES_YUSTI_FunctionalProxy");root.transform.position=layout.NavigationNodes.Where(n=>n.RoomId=="R_HALL"&&n.Position.y<.2f).OrderBy(n=>(n.Position-new Vector3(-3,0,-2)).sqrMagnitude).First().Position;
            root.transform.rotation=Quaternion.Euler(0,180,0);
            var body=root.AddComponent<FixtureActorBody>();body.ActorId="PRES_YUSTI";body.Height=1.8f;body.Capsule=root.AddComponent<UnityEngine.CharacterController>();body.Capsule.height=1.8f;body.Capsule.center=Vector3.up*.9f;body.Capsule.radius=.28f;body.Capsule.stepOffset=.32f;body.Capsule.skinWidth=.025f;
            var target=root.AddComponent<FixtureTarget>();target.StableId=body.ActorId;target.PublicName="유스티";
            body.Head=Child(root.transform,"Head",Vector3.up*1.6f);body.RightHand=Child(root.transform,"RightHand",new Vector3(.3f,1,0));body.LeftHand=Child(root.transform,"LeftHand",new Vector3(-.3f,1,0));
            CharacterPresentationBuilder.Butler(body);
            return body;
        }
        static void CreateCharacter(FixtureActorBody body,int number)
        {
            CharacterPresentationBuilder.Build(body,number);
        }
        static void Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material,PrimitiveType type){var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}
        [Serializable] sealed class SetupReceipt{public string Editor,Scene,Notes;public int Residents,Rooms,Connections,Anchors;}
        [MenuItem("BASSLINE/Production/10 Build Windows mansion")]
        public static void BuildWindows()
        {
            BuildWindowsTo("Builds/MansionWindows");
        }
        public static void BuildWindowsV10(){BuildWindowsTo("Builds/BASSLINE_v0.10");}
        public static void BuildWindowsV11(){BuildWindowsTo("Builds/BASSLINE_v0.11");}
        public static void BuildWindowsV12(){BuildWindowsTo("Builds/BASSLINE_v0.12");}
        public static void BuildWindowsV13(){BuildWindowsTo("Builds/BASSLINE_v0.13");}
        public static void BuildWindowsV14(){BuildWindowsTo("Builds/BASSLINE_v0.14");}
        public static void BuildWindowsV15(){BuildWindowsTo("Builds/BASSLINE_v0.15");}
        public static void BuildWindowsV16(){BuildWindowsTo("Builds/BASSLINE_v0.16");}
        public static void BuildWindowsV17(){BuildWindowsTo("Builds/BASSLINE_v0.17");}
        public static void BuildWindowsV18(){BuildWindowsTo("Builds/BASSLINE_v0.18");}
        public static void BuildWindowsV19(){BuildWindowsTo("Builds/BASSLINE_v0.19");}
        public static void BuildWindowsV31(){PlayerSettings.bundleVersion="0.31";BuildWindowsTo("Builds/BASSLINE_v0.31");}
        public static void BuildWindowsV30(){PlayerSettings.bundleVersion="0.30";BuildWindowsTo("Builds/BASSLINE_v0.30");}
        public static void BuildWindowsV29(){PlayerSettings.bundleVersion="0.29";BuildWindowsTo("Builds/BASSLINE_v0.29");}
        public static void BuildWindowsV28(){PlayerSettings.bundleVersion="0.28";BuildWindowsTo("Builds/BASSLINE_v0.28");}
        public static void BuildWindowsV27(){PlayerSettings.bundleVersion="0.27";BuildWindowsTo("Builds/BASSLINE_v0.27");}
        public static void BuildWindowsV26(){PlayerSettings.bundleVersion="0.26";BuildWindowsTo("Builds/BASSLINE_v0.26");}
        public static void BuildWindowsV25(){PlayerSettings.bundleVersion="0.25";BuildWindowsTo("Builds/BASSLINE_v0.25");}
        public static void BuildWindowsV24(){BuildWindowsTo("Builds/BASSLINE_v0.24");}
        public static void BuildWindowsV23(){BuildWindowsTo("Builds/BASSLINE_v0.23");}
        public static void BuildWindowsV22(){BuildWindowsTo("Builds/BASSLINE_v0.22");}
        public static void BuildWindowsV21(){BuildWindowsTo("Builds/BASSLINE_v0.21");}
        public static void BuildWindowsV20(){BuildWindowsTo("Builds/BASSLINE_v0.20");}
        public static void BuildWindowsReview()
        {
            BuildWindowsTo("Builds/MansionWindows_v9");
        }
        static void BuildWindowsTo(string output)
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))throw new NotSupportedException("Windows build support unavailable");
            Build();Directory.CreateDirectory(output);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath,"Assets/BASSLINE/Scenes/Tests/FixtureK_Life.unity","Assets/BASSLINE/Scenes/Tests/FixtureK_Incident.unity","Assets/BASSLINE/Scenes/Mansion_Main.unity"},locationPathName=output+"/BASSLINE.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText("Verification/mansion-build.txt",$"{Application.unityVersion}\n{report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nSeconds={report.summary.totalTime.TotalSeconds}");
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Mansion Windows build failed");
        }
    }
}
