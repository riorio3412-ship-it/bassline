using UnityEngine;
namespace BASSLINE.AuthoringData
{
    // Two rigid bone segments. Solving never scales bones or changes the actor capsule.
    public sealed class ActorArmRig : MonoBehaviour
    {
        public Transform Upper, Forearm, Wrist, Grip;
        // Optional clothing guide. It follows the physical elbow but never
        // participates in reach, collisions, gripping or the IK solve itself.
        public Transform ElbowClothingGuide;
        public float UpperLength, ForearmLength;
        public int Side=1;
        public Vector3 GripOffset=new Vector3(0,-.040f,.027f);
        public Transform[] FingerRoots=System.Array.Empty<Transform>(),FingerTips=System.Array.Empty<Transform>();
        public Transform Thumb;
        public Vector3 Shoulder=>Upper.position;
        public void Curl(float amount)
        {
            amount=Mathf.Clamp01(amount);
            foreach(var finger in FingerRoots)finger.localRotation=Quaternion.Euler(Mathf.Lerp(-6,-78,amount),0,0);
            foreach(var finger in FingerTips)finger.localRotation=Quaternion.Euler(Mathf.Lerp(-4,-55,amount),0,0);
            if(Thumb)Thumb.localRotation=Quaternion.Euler(Mathf.Lerp(12,-35,amount),0,Side*Mathf.Lerp(-30,-55,amount));
        }
        public bool Pose(Vector3 gripPosition,Quaternion handRotation)
        {
            Vector3 wrist=gripPosition-handRotation*GripOffset;
            Vector3 delta=wrist-Shoulder;float distance=delta.magnitude;
            bool reachable=distance<=UpperLength+ForearmLength-.001f&&distance>Mathf.Abs(UpperLength-ForearmLength)+.001f;
            float d=Mathf.Clamp(distance,Mathf.Abs(UpperLength-ForearmLength)+.001f,UpperLength+ForearmLength-.001f);
            Vector3 direction=distance>.0001f?delta/distance:Vector3.down;
            Vector3 pole=transform.TransformDirection(new Vector3(Side*.7f,-.4f,-1));
            Vector3 bend=Vector3.ProjectOnPlane(pole,direction).normalized;
            if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(direction,Vector3.right).normalized;
            float along=(UpperLength*UpperLength-ForearmLength*ForearmLength+d*d)/(2*d);
            Vector3 elbow=Shoulder+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,UpperLength*UpperLength-along*along));
            Vector3 end=Shoulder+direction*d;
            Upper.rotation=Quaternion.FromToRotation(Vector3.down,elbow-Shoulder);
            Forearm.rotation=Quaternion.FromToRotation(Vector3.down,end-elbow);
            Wrist.rotation=handRotation;
            if(ElbowClothingGuide){ElbowClothingGuide.position=Forearm.position;ElbowClothingGuide.rotation=Quaternion.Slerp(Upper.rotation,Forearm.rotation,.5f);}
            return reachable;
        }
    }
}
