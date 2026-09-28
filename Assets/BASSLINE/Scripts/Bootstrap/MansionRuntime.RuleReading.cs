using UnityEngine;
using BASSLINE.AuthoringData;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        MansionIncidentRulePlate[] rulePlates=System.Array.Empty<MansionIncidentRulePlate>();
        readonly System.Collections.Generic.HashSet<string> ruleReadersThisTick=new System.Collections.Generic.HashSet<string>();
        bool CanReadIncidentRule(string actor,string plateId)
        {
            var body=PhysicalBody(actor);var plate=IncidentRulePlate(plateId);
            if(!body||!plate||!World.CanAct(actor)||!CanReach(actor,plateId,2.5))return false;
            if(actor=="CH_01"&&(cameraView.cullingMask&(1<<plate.ReadLayer))==0)return false;
            Vector3 origin=actor=="CH_01"?cameraView.transform.position:body.transform.position+Vector3.up*body.Height*.88f;
            Vector3 forward=actor=="CH_01"?cameraView.transform.forward:body.Head.forward;
            return RulePlateReadableFrom(plate,origin,forward,body.transform,actor=="CH_01"?cameraView:null);
        }
        bool RulePlateReadableFrom(MansionIncidentRulePlate plate,Vector3 origin,Vector3 forward,Transform observer=null,Camera view=null)
        {
            if(!plate||!plate.TryGetReadPoints(out var points)||Vector3.Dot(plate.ReadNormal,(origin-plate.ReadPosition).normalized)<.35f)return false;
            foreach(var point in points){
                if(view){var viewport=view.WorldToViewportPoint(point);if(viewport.z<view.nearClipPlane||viewport.z>view.farClipPlane||viewport.x<0||viewport.x>1||viewport.y<0||viewport.y>1)return false;}
                Vector3 delta=point-origin;
                if(delta.sqrMagnitude<.0001f||delta.magnitude>2.5f||Vector3.Dot(forward,delta.normalized)<.6f)return false;
                int count=Physics.RaycastNonAlloc(origin,delta.normalized,rayHits,Mathf.Max(0,delta.magnitude-.002f),~0,QueryTriggerInteraction.Ignore);
                if(count==rayHits.Length)return false;
                for(int i=0;i<count;i++)if(!observer||!rayHits[i].transform.IsChildOf(observer))return false;
            }
            return true;
        }
    }
}
