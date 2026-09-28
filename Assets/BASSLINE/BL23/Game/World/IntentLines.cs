using System.Collections.Generic;
using System.Linq;
using BL23.Sim;

namespace BL23.Game
{
    /// <summary>
    /// Why people move: when someone near you sets off to do something, they say so in a line ("차나 한 잔 해야겠다"), and
    /// looking at someone shows what they are doing and where they are headed. Presentation only — the kernel's activity
    /// labels and targets are read, nothing is decided here. Crime plans are never described (only a vague cover).
    /// </summary>
    public static class IntentLines
    {
        // activity tag → (polite, casual)
        static readonly Dictionary<string, (string polite, string casual)> _lines = new Dictionary<string, (string, string)>
        {
            ["eat"] = ("배가 고파서요. 식당에 가 볼게요.", "배고프다. 밥 먹으러 가야지."),
            ["snack"] = ("출출하네요. 뭐 좀 집어 먹어야겠어요.", "출출한데. 뭐 좀 먹어야지."),
            ["cook"] = ("부엌에 좀 다녀올게요. 뭐라도 만들어 두려고요.", "부엌 좀 다녀올게. 뭐라도 만들어 두게."),
            ["read"] = ("읽던 책을 마저 읽으려고요.", "읽던 책이나 마저 읽어야지."),
            ["organize"] = ("정리할 게 좀 있어서요.", "정리 좀 해야겠다."),
            ["restore"] = ("복원하던 걸 마저 봐야 해서요.", "하던 복원 작업 마저 해야지."),
            ["tea"] = ("차 한잔하고 올게요.", "차나 한잔해야겠다."),
            ["walk"] = ("잠깐 걷고 올게요. 머리 좀 식히려고요.", "좀 걷다 올게. 머리 식히게."),
            ["swim"] = ("수영장에 다녀올게요.", "수영이나 하고 와야지."),
            ["game"] = ("게임실에 가 보려고요. 같이 하실래요?", "게임실 간다. 올 사람?"),
            ["music"] = ("피아노 좀 치고 올게요.", "연주 좀 하고 올게."),
            ["perform"] = ("공연 연습을 해야 해서요.", "연습하러 간다."),
            ["garden"] = ("온실 화초에 물을 줘야 해요.", "온실 화분들 물 줘야 돼."),
            ["repair"] = ("고칠 게 있어서요.", "고칠 거 있어서 가 봐야 돼."),
            ["inspect"] = ("설비를 한번 둘러보려고요.", "설비 좀 둘러보고 올게."),
            ["craft"] = ("만들던 게 있어서요.", "만들던 거 마저 만들어야지."),
            ["film"] = ("찍어 둘 게 있어서요.", "좀 찍어 둘 게 있어."),
            ["party"] = ("모임에 가 봐야겠어요.", "모임 가 봐야지."),
            ["socialize"] = ("누구랑 이야기라도 하고 싶네요.", "누구 얘기할 사람 없나."),
            ["rest"] = ("잠깐 쉬어야겠어요.", "좀 쉬어야겠다."),
            ["sleep"] = ("피곤해서요. 먼저 들어가 볼게요.", "피곤하다. 먼저 잘게."),
            ["exercise"] = ("몸 좀 풀고 올게요.", "운동 좀 하고 올게."),
            ["puzzle"] = ("풀던 퍼즐이 있어서요.", "풀던 퍼즐 마저 풀어야지."),
            ["carry"] = ("짐 좀 옮겨야 해서요.", "짐 좀 옮겨야 돼."),
            ["trade"] = ("바꿀 물건이 있어서요.", "바꿀 거 있어서."),
            ["style"] = ("옷 정리 좀 하려고요.", "옷 정리 좀 해야겠다."),
            ["exhibit"] = ("전시 준비를 해야 해요.", "전시 준비해야 돼."),
            ["investigate"] = ("기록실에서 확인할 게 있어요.", "기록 좀 확인해 봐야겠어."),
            ["observe"] = ("사람들 구경이나 할까요.", "사람 구경이나 해야지."),
            ["speech"] = ("발표 연습을 해야 해서요.", "발표 연습 좀 해야 돼."),
            ["pray"] = ("잠깐 추모하고 올게요.", "잠깐… 추모하고 올게."),
            ["laundry"] = ("빨래를 해야 해서요.", "빨래해야 돼."),
            ["cleanup"] = ("뒷정리 좀 할게요.", "뒷정리 좀 해야지."),
            ["explore"] = ("저택을 좀 둘러보려고요.", "저택 좀 둘러볼래."),
            ["tour"] = ("못 가 본 방이 있어서요.", "안 가 본 방 좀 보고 올게."),
            ["mourn"] = ("…혼자 있고 싶어요.", "…좀 혼자 있을게."),
            ["listen"] = ("음악 들으러 가려고요.", "음악이나 들으러 가야지."),
            ["chess"] = ("체스 한 판 두려고요.", "체스나 한 판 둘까."),
            ["browse"] = ("이것저것 구경하려고요.", "구경 좀 하고 올게."),
            ["bar"] = ("한잔하고 올게요.", "한잔하러 간다."),
            ["company"] = ("혼자 있기 싫어서요.", "혼자 있기 싫어."),
            ["safety"] = ("…사람 많은 데 있을래요.", "사람 많은 데 있을래."),
            ["meeting"] = ("약속이 있어서요.", "약속 있어서."),
        };

        static bool Polite(string id) => Cast.Get(id)?.Speech.PoliteDefault ?? true;

        /// <summary>The line someone says when setting off to do an activity (null when there is nothing worth saying).</summary>
        public static string Line(string actorId, string actId)
        {
            if (actId == null || !actId.StartsWith("life:")) return null;
            var parts = actId.Split(':'); if (parts.Length != 2) return null;   // routines with a target (comfort:P05…) speak for themselves
            var voiced = LineContext.Intent(actorId, parts[1]); if (voiced != null) return voiced;   // per-character "intent_<tag>" from the voice packs (Sim/Content/Voice)
            return _lines.TryGetValue(parts[1], out var l) ? (Polite(actorId) ? l.polite : l.casual) : null;
        }

        static bool Readable(string actId) => actId != null && (actId.StartsWith("life:") || actId.StartsWith("social:") || actId.StartsWith("inv:") || actId.StartsWith("case:"));

        /// <summary>"도서실로 가는 중 · 독서", "식사 중", "시온와(과) 이야기 중" — what someone looks to be doing right now.</summary>
        public static string Doing(GameState S, Actor a)
        {
            if (a == null || !a.Alive) return null;
            if (a.Pose == Pose.Sleep) return "잠들어 있다";
            if (a.TalkingTo != null) return LineBank.FixParticles(Cast.GivenOf(a.TalkingTo) + "와(과) 이야기 중");
            var act = a.Act;
            if (act == null) return a.Pose == Pose.Sit ? "앉아서 쉬는 중" : "서성이는 중";
            if (!Readable(act.Id)) return a.Speed > 0.05f ? "어딘가로 걸어가는 중" : "무언가를 하는 중";   // plans are never spelled out
            string label = act.Label ?? "";
            var st = act.Cur;
            if (st != null && st.Kind == "GoTo" && st.HasTarget)
            {
                int room = S.Layout.RoomAt(st.Target);
                if (room >= 0 && room != a.Room) return LineBank.FixParticles($"{S.RoomName(room)}(으)로 가는 중" + (label.Length > 0 ? " · " + label : ""));
            }
            // labels already read as a state ("충격으로 굳어 있음", "…을 찾는 중") are not given a second "중"; "…하러 감" reads "…하러 가는 중"
            return label.Length > 0 ? LineBank.FixParticles(label.EndsWith("중") || label.EndsWith("음") ? label : label.EndsWith("감") ? label.Substring(0, label.Length - 1) + "가는 중" : label + " 중") : "무언가를 하는 중";
        }
    }
}
