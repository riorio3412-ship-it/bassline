using System.IO;
using UnityEngine;
using UnityEditor;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static class MansionWashStationBuilder
    {
        public static void Build(MansionLayout layout)
        {
            var room=layout.Room("R_KITCHEN");if(!room)return;
            var root=new GameObject("M_KITCHEN_TAP");root.transform.SetParent(room.transform,true);root.transform.position=room.FloorCenter+new Vector3(-3.26f,1.20f,0);
            var target=root.AddComponent<FixtureTarget>();target.StableId="M_KITCHEN_TAP";target.PublicName="주방 수도";
            var station=root.AddComponent<MansionWashStation>();
            var steel=AssetDatabase.LoadAssetAtPath<Material>("Assets/BASSLINE/Art/Materials/M_WeaponSteel.mat");
            Part(root.transform,"수도 몸체",new Vector3(-.20f,-.04f,0),new Vector3(.04f,.40f,.04f),steel,true);
            Part(root.transform,"수도 꼭지",new Vector3(-.10f,.14f,0),new Vector3(.24f,.035f,.035f),steel,true);
            var point=new GameObject("물건을 씻는 자리");point.transform.SetParent(root.transform,false);point.transform.localPosition=new Vector3(0,-.025f,0);station.WaterPoint=point.transform;
            const string path="Assets/BASSLINE/Art/Materials/M_RinseWater.mat";
            Directory.CreateDirectory(Path.GetDirectoryName(path));var water=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!water){water=new Material(Shader.Find("Universal Render Pipeline/Lit"));water.SetColor("_BaseColor",new Color(.45f,.73f,.84f));water.SetFloat("_Smoothness",.9f);AssetDatabase.CreateAsset(water,path);}
            station.Water=Part(root.transform,"흐르는 물",new Vector3(0,-.04f,0),new Vector3(.012f,.32f,.012f),water,false);station.Water.SetActive(false);
        }
        static GameObject Part(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool collider)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
    }
}
