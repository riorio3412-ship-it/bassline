using UnityEngine;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static class MansionDivineEyeBuilder
    {
        // Local +Z faces the room; the anchor must be outside the wall collider.
        // Materials are authored project assets, not generated or shared-mutated at runtime.
        public static MansionDivineEye BuildAt(Transform darkCorner,Material lid,Material sclera,Material iris)
        {
            if(!darkCorner||!lid||!sclera||!iris)throw new System.ArgumentException("An authored dark corner and eye materials are required.");
            var root=new GameObject("Divine eye");root.transform.SetParent(darkCorner,false);
            var eye=root.AddComponent<MansionDivineEye>();eye.AuthoredDarkCorner=true;eye.ApertureRadius=.13f;
            var closed=Surface(root.transform,"Closed eyelid",new Vector3(.26f,.018f,.025f),Vector3.zero,lid);
            var white=Surface(root.transform,"Open eye",new Vector3(.25f,.13f,.06f),Vector3.zero,sclera);
            var pupil=Surface(root.transform,"Iris",new Vector3(.058f,.096f,.012f),new Vector3(0,0,.031f),iris);
            eye.ClosedVisuals=new[]{closed};eye.OpenVisuals=new[]{white,pupil};white.enabled=false;pupil.enabled=false;
            return eye;
        }
        static Renderer Surface(Transform parent,string name,Vector3 scale,Vector3 position,Material material)
        {
            var shape=GameObject.CreatePrimitive(PrimitiveType.Sphere);shape.name=name;shape.transform.SetParent(parent,false);shape.transform.localPosition=position;shape.transform.localScale=scale;
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            var renderer=shape.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }
    }
}
