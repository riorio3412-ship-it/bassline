using UnityEngine;

namespace BASSLINE.UI
{
    // Schematic landmark symbols, never measured room footprints.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KnownMapSymbol:UnityEngine.UI.MaskableGraphic
    {
        public bool Arrow;
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float w=r.width*.5f,h=r.height*.32f;
            if(Arrow){
                Add(vh,new[]{new Vector2(0,r.height*.5f),new Vector2(-w,-r.height*.5f),new Vector2(0,-r.height*.25f),new Vector2(w,-r.height*.5f)},color);return;
            }
            var left=new Vector2(-w,0);var top=new Vector2(0,h);var right=new Vector2(w,0);var bottom=new Vector2(0,-h);var depth=new Vector2(0,-r.height*.25f);
            Add(vh,new[]{left,bottom,bottom+depth,left+depth},color*.64f);
            Add(vh,new[]{bottom,right,right+depth,bottom+depth},color*.8f);
            Add(vh,new[]{left,top,right,bottom},color);
        }
        static void Add(UnityEngine.UI.VertexHelper vh,Vector2[] points,Color shade)
        {
            int start=vh.currentVertCount;
            foreach(var p in points){var v=UnityEngine.UIVertex.simpleVert;v.position=p;v.color=shade;vh.AddVert(v);}
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
    }
}
