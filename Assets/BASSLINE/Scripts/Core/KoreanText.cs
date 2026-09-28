namespace BASSLINE.Core
{
    public static class KoreanText
    {
        public static string AsSubject(string name)
        {
            if(string.IsNullOrEmpty(name))return "인물이";
            char last=name[name.Length-1];bool final=last>='가'&&last<='힣'&&(last-'가')%28!=0;
            return name+(final?"이":"가");
        }
        public static string AsObject(string name)
        {
            if(string.IsNullOrEmpty(name))return "대상을";
            char last=name[name.Length-1];bool final=last>='가'&&last<='힣'&&(last-'가')%28!=0;
            return name+(final?"을":"를");
        }
    }
}
