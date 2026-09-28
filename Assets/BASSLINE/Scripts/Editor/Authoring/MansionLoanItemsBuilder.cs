using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BASSLINE.AuthoringData;
using BASSLINE.Core;
namespace BASSLINE.Authoring
{
    // Functional props for the next generated scene; these are not final art or six finished request stories.
    public static class MansionLoanItemsBuilder
    {
        public static IEnumerable<FixtureObjectBody> Build(MansionLayout layout)
        {
            var paper=Material("M_LoanPaper",new Color(.78f,.74f,.62f));
            var ink=Material("M_LoanInk",new Color(.13f,.15f,.21f));
            var red=Material("M_LoanCover",new Color(.29f,.075f,.13f));
            var brass=Material("M_LoanRuler",new Color(.64f,.53f,.32f));
            var book=Create(layout,"M_JINWOO_PUZZLE_BOOK","진우의 작은 수수께끼 책","CH_02",new Vector3(.07f,.015f,.12f));
            Part(book.transform,"Pages",Vector3.zero,new Vector3(.067f,.011f,.116f),paper);
            Part(book.transform,"CoverTop",Vector3.up*.0065f,new Vector3(.07f,.002f,.12f),red);
            Part(book.transform,"CoverBottom",Vector3.down*.0065f,new Vector3(.07f,.002f,.12f),red);
            Part(book.transform,"Spine",Vector3.left*.034f,new Vector3(.002f,.015f,.12f),red);
            Part(book.transform,"Bookmark",new Vector3(.015f,.0076f,0),new Vector3(.006f,.0008f,.11f),paper);
            book.Loan=new ItemLoanTerms{Lender="CH_02",ItemId=book.ObjectId,ShortName="수수께끼 책",RequestLabel="그 수수께끼 책, 빌려도 돼?",
                Offer="읽어 볼래? 좋아.\n접힌 귀퉁이가 원래부터 있었으니까 놀라지는 말고.\n다 보면 나한테 줘.",
                Reminder="책은 아직 네가 빌려 간 걸로 기억하는데.\n다 읽었으면 직접 돌려줘. 어디 뒀는지는 나도 몰라.",
                Unavailable="지금 손에 없네.\n어디 있는지 확인하고 나서 빌려줄게.",
                ReturnInstruction="나한테 건네주면 돼.\n도서실 책 사이에 꽂아 두면 내 책인지 찾기 힘들잖아.",
                Declined="그래. 읽고 싶어지면 다시 말해."};
            yield return book;
            var ruler=Create(layout,"M_SEOYUN_RULER","서윤의 눈금 자","CH_03",new Vector3(.025f,.006f,.15f));
            Part(ruler.transform,"Body",Vector3.zero,new Vector3(.025f,.006f,.15f),brass);
            for(int i=0;i<15;i++)Part(ruler.transform,"Mark"+i,new Vector3(-.007f,.0032f,-.07f+i*.01f),new Vector3(i%5==0?.011f:.006f,.0003f,.0007f),ink);
            ruler.Loan=new ItemLoanTerms{Lender="CH_03",ItemId=ruler.ObjectId,ShortName="자",RequestLabel="그 자, 잠깐 빌릴 수 있어?",
                Offer="응. 끝이 조금 날카로우니까 조심해.\n쓰고 나면 내 손에 돌려줘. 작업대에 두지는 말고.",
                Reminder="아까 빌려준 자부터 돌려받고 싶어.\n마지막으로 놓은 곳을 한번 생각해 봐.",
                Unavailable="지금은 내가 가지고 있지 않아.\n없는 물건을 빌려주겠다고 할 수는 없지.",
                ReturnInstruction="다 썼을 때 나한테 직접 줘.\n작업대 위에 놓는 것과 내가 받는 건 다르니까.",
                Declined="알았어. 지금 필요하지 않으면 내가 갖고 있을게."};
            yield return ruler;
        }
        static FixtureObjectBody Create(MansionLayout layout,string id,string label,string owner,Vector3 size)
        {
            var go=new GameObject(id);go.transform.position=layout.Room("R_BED_"+owner.Substring(3)).WalkPoint+new Vector3(.55f,size.y*.5f+.002f,0);
            var body=go.AddComponent<FixtureObjectBody>();body.ObjectId=id;body.InitialOwner=owner;body.LoanRestPosition=go.transform.position;
            var hit=go.AddComponent<BoxCollider>();hit.size=size;body.Collider=hit;
            var target=go.AddComponent<FixtureTarget>();target.StableId=id;target.PublicName=label;return body;
        }
        static Material Material(string name,Color color)
        {
            string path="Assets/BASSLINE/Art/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.25f);AssetDatabase.CreateAsset(material,path);}return material;
        }
        static void Part(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=size;
            part.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(part.GetComponent<Collider>());
        }
    }
}
