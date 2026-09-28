using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
namespace BASSLINE.AuthoringData
{
    public static class ReadableGlyphs
    {
        public static bool TryGetPoints(TextMeshPro label,string expected,out Vector3[] points)
        {
            points=Array.Empty<Vector3>();
            if(!label||!label.isActiveAndEnabled||!label.font||label.richText||string.IsNullOrWhiteSpace(expected)||label.text!=expected||label.color.a<.9f)return false;
            var renderer=label.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled||!renderer.sharedMaterial)return false;
            if(label.havePropertiesChanged||label.textInfo.characterCount==0)label.ForceMeshUpdate();
            if(label.isTextOverflowing||label.firstVisibleCharacter!=0)return false;
            int expectedCount=0;for(int i=0;i<expected.Length;i++){if(!char.IsWhiteSpace(expected,i))expectedCount++;if(char.IsHighSurrogate(expected[i])&&i+1<expected.Length&&char.IsLowSurrogate(expected[i+1]))i++;}
            var result=new List<Vector3>();var seen=new HashSet<int>();var info=label.textInfo;var materials=label.fontSharedMaterials;
            for(int i=0;i<info.characterCount;i++){
                var c=info.characterInfo[i];if(c.index<0||c.index>=expected.Length)return false;if(char.IsWhiteSpace(expected,c.index))continue;
                if(!seen.Add(c.index)||!c.isVisible||c.textElement==null||c.textElement.unicode!=(uint)char.ConvertToUtf32(expected,c.index)||c.vertex_BL.color.a<230||c.vertex_TR.color.a<230||c.vertex_TL.color.a<230||c.vertex_BR.color.a<230)return false;
                if(c.materialReferenceIndex<0||c.materialReferenceIndex>=materials.Length)return false;var material=materials[c.materialReferenceIndex];
                if(!material||material.HasProperty("_FaceColor")&&material.GetColor("_FaceColor").a<.9f)return false;
                var bottom=label.transform.TransformPoint(c.bottomLeft);var top=label.transform.TransformPoint(c.topLeft);
                if(char.IsLetterOrDigit(expected,c.index)&&Vector3.Distance(bottom,top)<.008f)return false;
                result.Add(label.transform.TransformPoint((c.bottomLeft+c.topRight)*.5f));
            }
            if(result.Count==0||result.Count!=expectedCount)return false;points=result.ToArray();return true;
        }
    }
}
