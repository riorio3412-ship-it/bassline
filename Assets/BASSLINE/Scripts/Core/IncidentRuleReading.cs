using System;
namespace BASSLINE.Core
{
    public static class IncidentRuleReading
    {
        public static string Root(IncidentExecutionDefinition d)=>"ACTION_RULE_"+d.Id+"_"+d.Revision;
        public static string DisplayText(IncidentExecutionDefinition d)=>"사용 안내\n"+d.PublicRule+"\n안내 개정: "+d.Revision;
        public static int Duration(string text)=>(int)Math.Min(int.MaxValue,Math.Max(180L,(long)(text??"").Length*5));
        public static string ContentKey(IncidentExecutionDefinition d)=>Part(d.Id)+Part(d.Revision)+Part(d.PublicRule);
        static string Part(string value)=>(value??"").Length+":"+(value??"");
    }
}
