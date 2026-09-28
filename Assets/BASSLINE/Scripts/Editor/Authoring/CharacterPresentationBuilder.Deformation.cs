using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BASSLINE.AuthoringData;

namespace BASSLINE.Authoring
{
    public static partial class CharacterPresentationBuilder
    {
        // One closed tube, with smooth weights through the elbow. The authoritative
        // hand solver still owns both bones; clothing never changes reach or custody.
        static void JinwooDeformingSleeve(ActorArmRig arm,Material material)
        {
            Object.DestroyImmediate(arm.Upper.Find("Upper sleeve").gameObject);
            Object.DestroyImmediate(arm.Forearm.Find("Lower sleeve").gameObject);
            arm.Upper.localRotation=arm.Forearm.localRotation=arm.Wrist.localRotation=Quaternion.identity;
            var surface=Node(arm.transform,"J20 Deforming sleeve",Vector3.zero);
            float u=arm.UpperLength,l=arm.ForearmLength,total=u+l-.026f;
            arm.ElbowClothingGuide=Node(arm.transform,"Elbow clothing guide",arm.Upper.localPosition+Vector3.down*u);
            const int rows=41,columns=28;
            var vertices=new List<Vector3>();var triangles=new List<int>();var weights=new List<BoneWeight>();
            var profile=new[]{(0f,.035f),(.025f,.052f),(u*.28f,.060f),(u*.70f,.054f),(u,.052f),(u+l*.25f,.055f),(u+l*.62f,.047f),(u+l*.78f,.051f),(total,.040f)};
            float Width(float distance){for(int i=1;i<profile.Length;i++)if(distance<=profile[i].Item1)return Mathf.Lerp(profile[i-1].Item2,profile[i].Item2,Mathf.InverseLerp(profile[i-1].Item1,profile[i].Item1,distance));return profile[profile.Length-1].Item2;}
            BoneWeight Weight(float distance){
                const float span=.105f;
                if(distance<u){float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(u-span,u,distance));return new BoneWeight{boneIndex0=0,weight0=1-blend,boneIndex1=2,weight1=blend};}
                float lowerBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(u,u+span,distance));return new BoneWeight{boneIndex0=2,weight0=1-lowerBlend,boneIndex1=1,weight1=lowerBlend};
            }
            for(int row=0;row<rows;row++){
                float distance=total*row/(rows-1f),width=Width(distance);
                for(int j=0;j<columns;j++){
                    float a=j*Mathf.PI*2/columns;
                    vertices.Add(arm.Upper.localPosition+new Vector3(Mathf.Sin(a)*width,-distance,Mathf.Cos(a)*width*1.04f));
                    weights.Add(Weight(distance));
                }
            }
            for(int row=0;row<rows-1;row++)for(int j=0;j<columns;j++){
                int a=row*columns+j,b=row*columns+(j+1)%columns,c=a+columns,d=b+columns;
                triangles.AddRange(new[]{a,c,b,b,c,d});
            }
            for(int end=0;end<2;end++){
                int center=vertices.Count,ring=end==0?0:rows-1;float distance=end==0?0:total;
                vertices.Add(arm.Upper.localPosition+Vector3.down*distance);weights.Add(Weight(distance));
                for(int j=0;j<columns;j++){int a=ring*columns+j,b=ring*columns+(j+1)%columns;triangles.AddRange(end==0?new[]{center,a,b}:new[]{center,b,a});}
            }
            var mesh=MeshAsset("J20_SkinnedSleeve_"+arm.Side,vertices,triangles);
            mesh.boneWeights=weights.ToArray();var bones=new[]{arm.Upper,arm.Forearm,arm.ElbowClothingGuide};
            mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*surface.localToWorldMatrix).ToArray();EditorUtility.SetDirty(mesh);
            var renderer=surface.gameObject.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterial=material;renderer.bones=bones;renderer.rootBone=arm.Upper;
            renderer.quality=SkinQuality.Bone2;renderer.updateWhenOffscreen=true;
        }

        // Project facial ink onto the actual triangulated head, not an independently
        // estimated ellipse. Small subdivisions keep the patches on curved cheeks.
        static void ConformJinwooFeatures(Transform root,Mesh face)
        {
            ConformJinwooDetails(root,face,new[]{"J19_White_","J19_Iris_","J19_Pupil_","J19_Glint_","J19_UpperLid_","J19_LowerLid_","J19_Brow_","J19_Mouth","J19_LowerLip"});
            ConformJinwooDetails(root,root.Find("J19_Cardigan").GetComponent<MeshFilter>().sharedMesh,new[]{"J19_WaistFold_"});
            foreach(string name in new[]{"LegLeft","LegRight"}){
                var leg=root.Find(name);var trouser=leg.GetComponentsInChildren<MeshFilter>().Single(f=>f.name.StartsWith("J19_Trouser_"));
                ConformJinwooDetails(leg,trouser.sharedMesh,new[]{"J19_PantsCrease_","J19_PantsFold_"});
            }
        }
        static void ConformJinwooDetails(Transform root,Mesh face,string[] prefixes)
        {
            var faceVertices=face.vertices;var faceTriangles=face.triangles;
            float Depth(float x,float y){
                float depth=float.NegativeInfinity;
                for(int i=0;i<faceTriangles.Length;i+=3){
                    var a=faceVertices[faceTriangles[i]];var b=faceVertices[faceTriangles[i+1]];var c=faceVertices[faceTriangles[i+2]];
                    float det=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(det)<1e-10f)continue;
                    float u=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/det,v=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/det,w=1-u-v;
                    if(u>=-.00001f&&v>=-.00001f&&w>=-.00001f)depth=Mathf.Max(depth,u*a.z+v*b.z+w*c.z);
                }
                if(float.IsNegativeInfinity(depth))throw new System.InvalidOperationException("Facial feature is outside the authored head: "+x+","+y);
                return depth;
            }
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>().Where(f=>prefixes.Any(prefix=>f.name.StartsWith(prefix))).ToArray()){
                var source=filter.sharedMesh;var old=source.vertices;var indices=source.triangles;
                var v=new List<Vector3>();var t=new List<int>();var unique=new Dictionary<Vector3,int>();
                float offset=filter.name.Contains("Lid")?.0019f:filter.name.Contains("Glint")?.0016f:filter.name.Contains("Pupil")?.0014f:filter.name.Contains("Iris")?.0012f:.0007f;
                int Vertex(Vector3 p){p.z=Depth(p.x,p.y)+offset;if(unique.TryGetValue(p,out int index))return index;index=v.Count;unique.Add(p,index);v.Add(p);return index;}
                void Triangle(Vector3 a,Vector3 b,Vector3 c,int level){
                    if(level>0){var ab=(a+b)*.5f;var bc=(b+c)*.5f;var ca=(c+a)*.5f;Triangle(a,ab,ca,level-1);Triangle(ab,b,bc,level-1);Triangle(ca,bc,c,level-1);Triangle(ab,bc,ca,level-1);return;}
                    int ia=Vertex(a),ib=Vertex(b),ic=Vertex(c);if(ia==ib||ib==ic||ic==ia)return;
                    if(Cross2(a,b,c)<0)t.AddRange(new[]{ia,ic,ib});else t.AddRange(new[]{ia,ib,ic});
                }
                // The original panels contain front and back copies. Keep front-facing
                // triangles only so coincident ink surfaces do not flicker.
                for(int i=0;i<indices.Length;i+=3){var a=old[indices[i]];var b=old[indices[i+1]];var c=old[indices[i+2]];if(Cross2(a,b,c)>1e-10f)Triangle(a,b,c,2);}
                filter.sharedMesh=MeshAsset("J20_Conformed_"+filter.name,v,t);
            }
        }
    }
}
