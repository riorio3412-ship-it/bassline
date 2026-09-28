using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BASSLINE.Authoring
{
    public static partial class MansionBuilder
    {
        // Scene authoring changes, but the physical/save geometry revision remains unchanged.
        public const string AppearanceVersion = "M01_ManorIdentity_011";
        static readonly Dictionary<string, string[]> WingPalette = new Dictionary<string, string[]> {
            {"CENTRAL",new[]{"B9B9AF","365960"}}, {"LIV",new[]{"C6B49B","906342"}},
            {"LIB",new[]{"AFB3AD","42676A"}}, {"CUL",new[]{"BBB0BC","705171"}},
            {"NAT",new[]{"ADBBAA","4D7057"}}, {"WRK",new[]{"B5B4AD","8B6B42"}},
            {"MED",new[]{"C0CCCA","447B7B"}}, {"AQU",new[]{"A9BDC5","3E7086"}},
            {"ANX",new[]{"AEA799","716453"}}, {"LIVING",new[]{"C1AD99","956951"}},
            {"BASE",new[]{"9BABA9","466362"}}, {"UPPER",new[]{"B3B8C8","566783"}},
            {"TRIAL",new[]{"393941","80616C"}}
        };

        static void ApplyAppearance()
        {
            string physicalBefore = MansionAppearanceVerification.PhysicalHash(layout);
            var floors = new Dictionary<string, Material>(); var accents = new Dictionary<string, Material>();
            foreach (var pair in WingPalette) {
                floors[pair.Key] = Mat("Readability_Floor_"+pair.Key,pair.Value[0],.16f);
                accents[pair.Key] = Mat("Readability_Accent_"+pair.Key,pair.Value[1],.12f);
            }
            var wall = Mat("Readability_Wall","8B9293",.12f);
            var dado = Mat("Readability_Dado","36343A",.20f);
            var gilt = Mat("Manor_Gilt","AF9870",.35f,.45f);
            var darkStone = Mat("Manor_DarkStone","394148",.32f);
            var tread = Mat("Readability_Tread","7D8988",.14f);
            var edge = Mat("Readability_Nosing","E9D6A4",.12f);
            var rail = Mat("Readability_Rail","394B50",.22f);
            var signBacking = Mat("Readability_Sign","243E47",.10f,0,.10f);
            var ceiling = Mat("Readability_Ceiling","AAA9A0",.08f);
            int nosings=0, bands=0, signs=0;
            foreach(var room in layout.Rooms) {
                var floorMaterial=floors[room.WingId];var accent=accents[room.WingId];
                // The two main spines also have different visual identities within CENTRAL.
                if(room.RoomId=="R_CE"){floorMaterial=floors["LIV"];accent=accents["LIV"];}
                if(room.RoomId=="R_CW"){floorMaterial=floors["LIB"];accent=accents["LIB"];}
                var parent=room.transform.Find("Architecture");
                if(room.RoomId=="R_VEST"||room.RoomId=="R_HALL") {
                    // Decorative inlay sits on existing floors; navigation/collision remains identical.
                    var b=room.Bounds;float cell=1.2f;
                    for(int x=0;x<Mathf.FloorToInt((b.size.x-.4f)/cell);x++)
                        for(int z=0;z<Mathf.FloorToInt((b.size.z-.4f)/cell);z++)
                            if((x+z)%2==0)Box(parent,"Manor_MarbleInlay",new Vector3(b.min.x+.2f+(x+.5f)*cell,room.FloorCenter.y-.004f,b.min.z+.2f+(z+.5f)*cell),new Vector3(cell-.012f,.010f,cell-.012f),darkStone,false);
                }
                foreach(var renderer in parent.GetComponentsInChildren<MeshRenderer>().ToArray()) {
                    string name=renderer.name;
                    if(name=="WalkFloor"||name.StartsWith("Floor",StringComparison.Ordinal)||name.EndsWith("Deck",StringComparison.Ordinal)||
                       name.Contains("Landing")||name.StartsWith("Top",StringComparison.Ordinal)||name.StartsWith("Exit",StringComparison.Ordinal))
                        renderer.sharedMaterial=floorMaterial;
                    if(name=="Ceiling"&&room.RoomId!="R_TRIAL")renderer.sharedMaterial=ceiling;
                    if(name.StartsWith("Rail",StringComparison.Ordinal))renderer.sharedMaterial=rail;
                    if(name.Contains("_Tread_")) {
                        renderer.sharedMaterial=tread;
                        var step=renderer.transform;
                        // Surface paint, no new collision, rise or path. The near edge faces downstairs.
                        var trim=Box(parent,"Readability_Nosing_"+nosings++,step.TransformPoint(new Vector3(0,.5f,-.39f)),
                            new Vector3(step.lossyScale.x,.009f,.045f),edge,false);
                        trim.transform.rotation=step.rotation;
                    }
                }
                if(room.GeometryType=="Corridor") {
                    bool alongX=room.Bounds.size.x>room.Bounds.size.z;
                    float length=alongX?room.Bounds.size.x:room.Bounds.size.z;
                    float width=alongX?room.Bounds.size.z:room.Bounds.size.x;
                    foreach(int side in new[]{-1,1}) {
                        Vector3 offset=alongX?new Vector3(0,.009f,side*(width*.5f-.4f)):new Vector3(side*(width*.5f-.4f),.009f,0);
                        Box(parent,"Readability_EdgeBand_"+bands++,room.FloorCenter+offset,
                            alongX?new Vector3(length-.3f,.012f,.13f):new Vector3(.13f,.012f,length-.3f),accent,false);
                    }
                    for(float at=-length*.5f+3;at<length*.5f-.5f;at+=6) {
                        var offset=alongX?new Vector3(at,.006f,0):new Vector3(0,.006f,at);
                        Box(parent,"Readability_FloorJoint_"+bands++,room.FloorCenter+offset,
                            alongX?new Vector3(.035f,.008f,width-.3f):new Vector3(width-.3f,.008f,.035f),accent,false);
                    }
                }
                foreach(var sign in room.GetComponentsInChildren<Transform>().Where(t=>t.name=="Wayfinding"||t.name=="RoomNumber"||t.name=="WingSign"||t.name.StartsWith("Floor_",StringComparison.Ordinal)).ToArray()) {
                    RestyleSign(sign,signBacking,sign.name.StartsWith("Floor_",StringComparison.Ordinal)?1.45f:.64f);
                    signs++;
                }
                foreach(var light in room.Lights??Array.Empty<Light>()) {
                    if(room.RoomId=="R_TRIAL")continue;
                    light.color=room.WingId=="LIV"||room.WingId=="LIVING"?new Color(1,.92f,.80f):
                        room.WingId=="LIB"||room.WingId=="AQU"?new Color(.83f,.94f,1):new Color(1,.98f,.92f);
                }
            }
            foreach(var renderer in architecture.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("SharedWall_",StringComparison.Ordinal)).ToArray()) {
                if(renderer.sharedMaterial==black)continue;
                renderer.sharedMaterial=wall;
                var b=renderer.bounds;
                float floorY=Mathf.Floor((b.min.y+.02f)/6)*6;
                float lo=Mathf.Max(b.min.y,floorY+.12f),hi=Mathf.Min(b.max.y,floorY+1.05f);
                if(hi-lo<.2f)continue;
                bool alongZ=b.size.x<.3f;
                foreach(int side in new[]{-1,1}) {
                    var p=b.center;p.y=(lo+hi)*.5f;
                    if(alongZ)p.x+=side*(b.size.x*.5f+.004f);else p.z+=side*(b.size.z*.5f+.004f);
                    Box(architecture,"Readability_WallBase",p,alongZ?new Vector3(.006f,hi-lo,b.size.z):new Vector3(b.size.x,hi-lo,.006f),dado,false);
                    var cap=p;cap.y=hi;
                    Box(architecture,"Manor_BrassRail",cap,alongZ?new Vector3(.012f,.023f,b.size.z):new Vector3(b.size.x,.023f,.012f),gilt,false);
                    float width=alongZ?b.size.z:b.size.x;
                    for(float at=-width*.5f+.5f;at<width*.5f-.3f;at+=2.0f){
                        var seam=p;if(alongZ)seam.z+=at;else seam.x+=at;
                        Box(architecture,"Manor_PanelMoulding",seam,alongZ?new Vector3(.014f,hi-lo-.12f,.018f):new Vector3(.018f,hi-lo-.12f,.014f),gilt,false);
                    }
                }
            }
            foreach(var door in layout.Connections.Where(c=>c.Kind=="Door")) {
                foreach(var renderer in door.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Jamb",StringComparison.Ordinal)||r.name=="Header"))renderer.sharedMaterial=accents[layout.Room(door.RoomB).WingId];
                var a=door.transform.Find("DoorSignA");var b=door.transform.Find("DoorSignB");
                // TextMeshPro's visible front is local -Z. Both old door signs faced into the doorway.
                if(a){a.localRotation=Quaternion.identity;RestyleSign(a,signBacking,.5f);signs++;}
                if(b){b.localRotation=Quaternion.Euler(0,180,0);RestyleSign(b,signBacking,.5f);signs++;}
            }
            var grand=layout.Room("R_GRAND").transform.Find("Architecture");
            // At existing stair surfaces; these label physical floors, not unseen room contents.
            Sign(grand,"Readability_UpperFloor",new Vector3(3.4f,5.6f,7.02f),Quaternion.identity,"2F  ·  위층",2.6f,.65f);
            RestyleSign(grand.Find("Readability_UpperFloor"),signBacking,.65f);
            Sign(grand,"Readability_LowerFloor",new Vector3(6.995f,1.8f,4),Quaternion.Euler(0,90,0),"1F  ·  중앙 홀",2.3f,.65f);
            RestyleSign(grand.Find("Readability_LowerFloor"),signBacking,.65f);
            DressEntryHall();
            ConfigureLocalShadows();
            var lighting=layout.Room("R_HALL").transform.Find("Lighting");
            AddDepthLamp(lighting,"Readability_StairKey",new Vector3(1.5f,9.5f,1),new Vector3(4,1.7f,4.5f),new Color(1,.94f,.83f),3.2f);
            AddDepthLamp(lighting,"Readability_EntryKey",new Vector3(-5,8,-5),new Vector3(0,0,-3),new Color(.87f,.94f,1),2.4f);
            // Keep a readable baseline. Lighting currently changes presentation, not perception rules.
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.44f,.48f,.50f);
            RenderSettings.ambientEquatorColor=new Color(.30f,.32f,.33f);
            RenderSettings.ambientGroundColor=new Color(.23f,.24f,.24f);
            string physicalAfter=MansionAppearanceVerification.PhysicalHash(layout);
            if(physicalBefore!=physicalAfter)throw new InvalidOperationException("Appearance changed physical authoring data.");
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/mansion-appearance.json",JsonUtility.ToJson(new AppearanceReceipt {
                Version=AppearanceVersion,PhysicalBefore=physicalBefore,PhysicalAfter=physicalAfter,GeometryRevision=layout.SourceHash+"|"+layout.BuildVersion,
                Nosings=nosings,FloorBands=bands,RestyledSigns=signs,ShadowSpots=2,Result="PASS_PHYSICAL_INVARIANCE_NOT_VISUAL_APPROVAL"
            },true));
        }

        static void RestyleSign(Transform sign,Material backing,float height)
        {
            var panel=sign.Find("SignBacking");var lettering=sign.Find("Lettering");if(!panel||!lettering)return;
            var scale=panel.localScale;scale.y=height;panel.localScale=scale;
            panel.GetComponent<MeshRenderer>().sharedMaterial=backing;
            var text=lettering.GetComponent<TextMeshPro>();text.color=new Color(.97f,.94f,.84f);
            text.rectTransform.sizeDelta=new Vector2(scale.x-.10f,height-.07f);text.fontSizeMax=height>1?8:4.3f;text.fontSizeMin=1.4f;
            text.ForceMeshUpdate();
        }
        static void AddDepthLamp(Transform parent,string name,Vector3 at,Vector3 target,Color color,float intensity)
        {
            var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(parent,false);light.transform.position=at;
            light.transform.rotation=Quaternion.LookRotation(target-at);light.type=LightType.Spot;light.spotAngle=100;light.innerSpotAngle=65;
            light.range=17;light.intensity=intensity;light.color=color;light.shadows=LightShadows.Soft;light.shadowStrength=.6f;light.shadowBias=.03f;light.shadowNormalBias=.25f;
        }
        static void ConfigureLocalShadows()
        {
            const string id="RP_BASSLINE_URP";string path=UrpMigration.PipelinePath;
            string hash=AtomicSaveStore.Hash("Readability_LocalShadows_001");
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if(!pipeline)throw new InvalidOperationException("Missing project URP pipeline.");
            var serialized=new SerializedObject(pipeline);
            var enabled=serialized.FindProperty("m_AdditionalLightShadowsSupported");
            if(enabled==null)throw new InvalidOperationException("URP local shadow setting not available.");
            // Unity can upgrade URP serialization on import. Preserve those upgrades and any
            // project tuning when the one required capability is already enabled.
            if(enabled.boolValue)return;
            if(!ProductionImporter.CanWrite(ProductionImporter.LoadIndex(),id,path,hash))return;
            enabled.boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);AssetDatabase.SaveAssets();
            ProductionImporter.Track(ProductionImporter.LoadIndex(),id,path,hash,"EngineGenerated_URP_LocalShadows");
        }
        [Serializable] sealed class AppearanceReceipt { public string Version,PhysicalBefore,PhysicalAfter,GeometryRevision,Result;public int Nosings,FloorBands,RestyledSigns,ShadowSpots; }
    }
}
