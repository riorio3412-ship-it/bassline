using System;
using System.Linq;
using System.Globalization;
namespace BASSLINE.Core
{
    [Serializable] public sealed class SurfacePattern
    {
        public int Mask,Colour,SizeMm=60;
        public SurfacePattern Copy()=>(SurfacePattern)MemberwiseClone();
        public static int Mirror(int mask){int result=0;for(int y=0;y<4;y++)for(int x=0;x<4;x++)if((mask&(1<<(y*4+x)))!=0)result|=1<<(y*4+3-x);return result;}
        public static int Rotate(int mask){int result=0;for(int y=0;y<4;y++)for(int x=0;x<4;x++)if((mask&(1<<(y*4+x)))!=0)result|=1<<(x*4+3-y);return result;}
        public string Key=>Mask.ToString("X4",CultureInfo.InvariantCulture)+":"+Colour+":"+SizeMm;
        public static void Validate(SurfacePattern p){if(p==null||p.Mask<1||p.Mask>65535||p.Colour<0||p.Colour>3||p.SizeMm<20||p.SizeMm>200)throw new ArgumentException("Invalid visible surface pattern");}
        public string Describe(){Validate(this);string colour=new[]{"흰색","푸른색","붉은색","검은색"}[Colour];return colour+" 자국 · 폭 "+SizeMm+" mm\n"+string.Join("\n",Enumerable.Range(0,4).Select(y=>string.Join(" ",Enumerable.Range(0,4).Select(x=>(Mask&(1<<(y*4+x)))!=0?"■":"·"))));}
    }
    public static class SurfacePatternComparison
    {
        public static bool CanCompare(KnownRecord r)=>r!=null&&r.Direct&&r.Kind=="Visual"&&(r.Predicate=="SurfacePattern"||r.Predicate=="ContactPattern")&&!string.IsNullOrEmpty(r.Value);
        static bool Equivalent(string a,string b)
        {
            var left=a.Split(':');var right=b.Split(':');
            if(left.Length!=3||right.Length!=3||left[1]!=right[1]||left[2]!=right[2]||!int.TryParse(left[0],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out int x)||!int.TryParse(right[0],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out int y))return false;
            for(int i=0;i<4;i++){if(x==y||SurfacePattern.Mirror(x)==y)return true;x=SurfacePattern.Rotate(x);}return false;
        }
        public static string Describe(KnownRecord left,KnownRecord right)
        {
            if(!CanCompare(left)||!CanCompare(right)||left.LoopId!=right.LoopId)return "직접 살펴본 같은 회차의 무늬 두 개가 필요해요.";
            if(left.ProvenanceKey==right.ProvenanceKey)return "같은 표면을 다시 살펴본 기록이에요. 별개의 흔적은 아니에요.";
            return Equivalent(left.Value,right.Value)?"살펴본 무늬의 색·폭·모양이 같아요. 같은 모양의 다른 물건도 이런 자국을 남길 수 있어요. 누가, 언제 사용했는지는 아직 몰라요.":"살펴본 무늬의 색·폭·모양 중 다른 부분이 있어요. 마모나 덧묻은 자국이 있을 수 있으니 이것만으로 접촉 여부를 단정할 수는 없어요.";
        }
    }
}
