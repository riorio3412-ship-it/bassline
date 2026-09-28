using System;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using UnityEditor;
using UnityEngine;
namespace BASSLINE.Authoring
{
    public static class AppointmentDeskBuilder
    {
        public static FixtureObjectBody Build(MansionLayout layout)
        {
            var support=layout.Room("R_LIBRARY").GetComponentsInChildren<MansionProp>().Single(p=>p.ObjectId=="OBJ_R_LIBRARY_ReadingTable0").transform.Find("Top");
            var station=new GameObject("Appointment writing place").AddComponent<AppointmentDesk>();station.transform.position=support.position;
            var rest=new GameObject("Paper rest position").transform;rest.SetParent(station.transform,false);rest.position=new Vector3(support.position.x+.28f,support.GetComponent<Renderer>().bounds.max.y+.004f,support.position.z);station.PaperRestPoint=rest;
            var card=GameObject.CreatePrimitive(PrimitiveType.Cube);card.name=AppointmentDesk.CardId;card.transform.position=rest.position;card.transform.localScale=new Vector3(.28f,.008f,.20f);
            var body=card.AddComponent<FixtureObjectBody>();body.ObjectId=AppointmentDesk.CardId;body.Collider=card.GetComponent<Collider>();
            var target=card.AddComponent<FixtureTarget>();target.StableId=body.ObjectId;target.PublicName="약속 카드";
            string folder="Assets/BASSLINE/Art/Materials";Directory.CreateDirectory(folder);
            Material Material(string name,Color color){string path=folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(m,path);}return m;}
            card.GetComponent<Renderer>().sharedMaterial=Material("M_AppointmentPaper",new Color(.82f,.78f,.67f));
            var ink=Material("M_AppointmentInk",new Color(.15f,.22f,.24f));
            // Printed guide lines; the actual versioned writing is read through the inspection action.
            foreach(int i in Enumerable.Range(0,4)){
                var line=GameObject.CreatePrimitive(PrimitiveType.Cube);line.name="Printed guide "+i;UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());line.transform.SetParent(card.transform,false);line.transform.localPosition=new Vector3(0,.55f,.26f-i*.16f);line.transform.localScale=new Vector3(.78f,.1f,.006f);line.GetComponent<Renderer>().sharedMaterial=ink;
            }
            return body;
        }
    }
}
