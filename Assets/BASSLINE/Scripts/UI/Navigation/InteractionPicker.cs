using System;
using System.Linq;
using UnityEngine;
using BASSLINE.AuthoringData;
using BASSLINE.Core;

namespace BASSLINE.UI
{
    // Candidate tolerance never bypasses reach, knowledge availability, or occlusion.
    public static class InteractionPicker
    {
        public static InteractionView Pick(Camera camera,Transform player,Func<string,InteractionView> describe,float reach=2.1f,float radius=.24f)
        {
            if(!camera||describe==null)return null;
            var center=camera.ViewportPointToRay(new Vector3(.5f,.5f));
            var ray=new Ray(camera.transform.position,center.direction);
            foreach(var hit in Physics.RaycastAll(ray,reach,~0,QueryTriggerInteraction.Collide).OrderBy(x=>x.distance))
            {
                if(IsPlayer(hit.collider,player))continue;
                var target=hit.collider.GetComponentInParent<FixtureTarget>();
                if(target){var view=describe(target.StableId);if(view!=null&&view.Available)return view;}
                if(!hit.collider.isTrigger)break;
            }
            InteractionView best=null;float bestScore=float.MaxValue;
            foreach(var hit in Physics.SphereCastAll(ray,radius,reach,~0,QueryTriggerInteraction.Collide))
            {
                if(IsPlayer(hit.collider,player))continue;
                var target=hit.collider.GetComponentInParent<FixtureTarget>();if(!target)continue;
                var point=hit.collider.ClosestPoint(ray.origin+ray.direction*Mathf.Max(.01f,hit.distance));
                var delta=point-ray.origin;float distance=delta.magnitude;
                if(distance>reach||Vector3.Dot(delta,ray.direction)<0||!Visible(ray.origin,point,target,player))continue;
                var view=describe(target.StableId);if(view==null||!view.Available)continue;
                float score=(1-Vector3.Dot(delta.normalized,ray.direction))*8+distance*.1f;
                if(score<bestScore){best=view;bestScore=score;}
            }
            return best;
        }
        static bool Visible(Vector3 origin,Vector3 point,FixtureTarget target,Transform player)
        {
            Vector3 delta=point-origin;float distance=delta.magnitude;if(distance<.001f)return true;
            foreach(var hit in Physics.RaycastAll(origin,delta/distance,distance+.02f,~0,QueryTriggerInteraction.Collide).OrderBy(x=>x.distance))
            {
                if(IsPlayer(hit.collider,player))continue;
                if(hit.collider.GetComponentInParent<FixtureTarget>()==target)return true;
                if(!hit.collider.isTrigger)return false;
            }
            return true;
        }
        static bool IsPlayer(Collider collider,Transform player)=>player&&(collider.transform==player||collider.transform.IsChildOf(player));
    }
}
