using System.Collections.Generic;
using UnityEngine;
using BASSLINE.Core;
namespace BASSLINE.AuthoringData
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class MansionTracePatternVisual:MonoBehaviour
    {
        public SurfacePattern Pattern=new SurfacePattern();
        Mesh mesh;
        public void Build(SurfacePattern pattern,Material material)
        {
            SurfacePattern.Validate(pattern);Pattern=pattern.Copy();
            if(mesh)Destroy(mesh);mesh=new Mesh{name="Visible pigment pattern"};
            var vertices=new List<Vector3>();var triangles=new List<int>();float size=pattern.SizeMm*.001f,cell=size/4;
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)if((pattern.Mask&(1<<(y*4+x)))!=0){
                float left=(x-2)*cell,top=(2-y)*cell;int first=vertices.Count;
                vertices.Add(new Vector3(left,top-cell,0));vertices.Add(new Vector3(left+cell*.88f,top-cell,0));vertices.Add(new Vector3(left+cell*.88f,top-cell*.12f,0));vertices.Add(new Vector3(left,top-cell*.12f,0));
                triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
            }
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var block=new MaterialPropertyBlock();var colour=new[]{new Color(.84f,.82f,.75f),new Color(.16f,.32f,.65f),new Color(.58f,.12f,.18f),new Color(.08f,.07f,.09f)}[pattern.Colour];block.SetColor("_BaseColor",colour);block.SetColor("_Color",colour);renderer.SetPropertyBlock(block);
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
