using UnityEngine;
using UnityEditor;
using TMPro;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static class MansionReadableBookBuilder
    {
        // Functional open-page prop for the next scene generation, not final book art.
        public static void AddPage(FixtureObjectBody body)
        {
            var root=body.transform;var size=root.localScale;root.localScale=Vector3.one;
            Object.DestroyImmediate(body.GetComponent<MeshRenderer>());Object.DestroyImmediate(body.GetComponent<MeshFilter>());
            ((BoxCollider)body.Collider).size=size;
            var pages=GameObject.CreatePrimitive(PrimitiveType.Cube);pages.name="OpenPages";pages.transform.SetParent(root,false);pages.transform.localScale=size;
            Object.DestroyImmediate(pages.GetComponent<Collider>());
            const string materialPath="Assets/BASSLINE/Art/Materials/M_CommonBookPaper.mat";
            var paper=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!paper){paper=new Material(Shader.Find("Universal Render Pipeline/Lit"));paper.SetColor("_BaseColor",new Color(.84f,.8f,.68f));paper.SetFloat("_Smoothness",.1f);AssetDatabase.CreateAsset(paper,materialPath);}
            pages.GetComponent<Renderer>().sharedMaterial=paper;
            var book=body.gameObject.AddComponent<MansionReadableBook>();book.Revision="COMMON_BOOK_PAGE_1";
            book.Content="소리와 쉼\n\n빠른 박자 사이에도\n쉼표는 자리를 갖는다.\n같은 구절을 천천히 읽으면\n놓친 리듬이 들린다.";
            var label=new GameObject("ReadablePage");label.transform.SetParent(root,false);label.transform.localPosition=Vector3.up*(size.y*.5f+.0015f);label.transform.localRotation=Quaternion.Euler(90,0,0);
            var text=label.AddComponent<TextMeshPro>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BASSLINE/UI/Fonts/FONT_Korean_System.asset");
            if(!text.font)throw new System.InvalidOperationException("책 본문에 사용할 기존 한글 글꼴이 없습니다.");
            text.text=book.Content;text.richText=false;text.enableAutoSizing=false;text.fontSize=1.5f;text.color=new Color(.08f,.07f,.065f);
            text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Truncate;
            text.rectTransform.sizeDelta=new Vector2(size.x-.016f,size.z-.022f);book.Page=text;
        }
    }
}
