using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    // =====================================================================================================================
    // RULE SHIELDS — the culprit reads 유스티's rules as armour (y_rules 둘/여섯/일곱 and the chapter rules):
    //   y6 second-killer   "사건이 여럿이면 심판이 가릴 범인은 가장 먼저 일부러 목숨을 앗은 한 분" — a second killing is never judged
    //   y6 not-deliberate  only a DELIBERATE taking of life is judged — dress it as an accident, retreat to "it was an accident"
    //   y6 order-swap      make one's own killing look like the LATER one (a shifted time of death)
    //   y7 dead-scapegoat  "돌아가신 분을 지목하셔도 됩니다" — blame the dead, who cannot answer
    //   y2 first-in        "세 분 이상이 시신을 보셔야" — be one of the three; every trace on you is "from finding him"
    //   y2 delay           a room nobody passes: no bell until three have seen it, the hour of death blurs
    //   CH03/CH23 house-dark, CH22 noise, CH02 wing, CH16 closed-room, CH04 sealed-statement, CH10 inquiry, house-night night-lock
    // Chosen at design time (they shape the plan) and remembered for the 심판 (CaseApi.TrialPack.Shields).
    // =====================================================================================================================
    public static partial class Initiative
    {
        static void ChooseShields(Simulation sim, Scheme sc, Actor a, Actor v)
        {
            var S = sim.S; var c = a.Def;
            bool firstNotMine = S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Murder && i.Culprit != a.Id);
            if (firstNotMine) sc.Shield("y6", "second-killer");
            if (sc.Approach.StartsWith("method:") && Methods.Staged(sc.Approach.Substring(7))) sc.Shield("y6", "not-deliberate");
            if (sc.Moment == "house-dark") sc.Shield("CH03", "house-dark");
            if (sc.Moment == "long-dark") sc.Shield("CH23", "house-dark");
            if (sc.Moment == "noise") sc.Shield("CH22", "noise");
            if (sc.Moment == "night-lock") sc.Shield("house-night", "night-lock");
            if (S.RuleActive("CH04")) sc.Shield("CH04", "sealed-statement");
            if (S.RuleActive("CH10")) sc.Shield("CH10", "inquiry");
            if (S.RuleActive("CH02") && sc.Moment == "investigation") sc.Shield("CH02", "wing");
            var kr = S.Layout.Room(sc.KillRoom);
            if (S.RuleActive("CH16") && kr != null && (kr.Type == RoomType.MachineRoom || kr.Type == RoomType.WaterRoom)) sc.Shield("CH16", "closed-room");
            // y2: the body will lie unseen for a while — be one of the three who "find" it
            if ((sc.Approach == "errand" || sc.Approach == "slip-out" || sc.Approach == "ambush" || sc.Approach == "rendezvous") && c.Composure >= 65 && U(S, sc.Id + ":fi" + sc.Redesigns) < 0.5)
            {
                sc.Shield("y2", "first-in");
                sc.Prep.Add(new PrepTask { Kind = "firstin", NotBefore = 18 + Math.Floor(U(S, sc.Id + ":fiw") * 4) * 8, Due = 24 * 60 });
            }
            if (kr != null && sc.Approach != "dark-strike" && (kr.Floor == -1 || RoomInfo.IsMystery(kr.Type) || kr.Type == RoomType.Closet || kr.Type == RoomType.WineCellar || kr.Type == RoomType.ColdStorage || kr.Type == RoomType.Archive))
                sc.Shield("y2", "delay");
            if (DeadScapegoat(S, a.Id, v.Id) != null) sc.Shield("y7", "dead-scapegoat");
            if (sc.Shields.Count > 0) sc.Var("rule-shield");
        }

        /// <summary>A dead resident (this loop) who had a visible reason to hate the victim — the one the dead cannot answer for.</summary>
        internal static string DeadScapegoat(GameState S, string culprit, string victim)
        {
            foreach (var x in S.Actors.Values.Where(x => x.Status == ActorStatus.Dead || x.Status == ActorStatus.Executed).OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (x.Id == victim || x.Id == culprit || x.IsButler || !S.HasRel(x.Id, victim)) continue;
                var r = S.R(x.Id, victim);
                if (r.Grudge > 0.25f || r.Jealous > 0.3f || r.Tags.Contains("feud") || r.Tags.Contains("grudge") || r.Tags.Contains("enemy")) return x.Id;
            }
            return null;
        }

        internal static string MomentText(Scheme sc)
        {
            switch (sc.Moment)
            {
                case "hosted": return K($"직접 연 {sc.EventLabel}");
                case "joined": return K($"초대받은 {sc.EventLabel}");
                case "house-dark": return "저택의 정기 소등";
                case "long-dark": return "긴 암흑 속";
                case "noise": return "저택의 잔향이 소리를 덮는 틈";
                case "meal": return sc.MomentText ?? "식사 자리";
                case "habit": return K($"{Name(sc.Victim)}이(가) 늘 머무는 시간");
                case "night-lock": return "밤 10시 잠금 직전";
                case "rendezvous": return "단둘이 만나자는 약속";
                case "night-visit": return "모두 잠든 새벽";
                case "investigation": return "수사가 한창인 틈";
            }
            return sc.MomentText ?? "기회";
        }

        internal static string ApproachText(Scheme sc) => ApproachText(sc, null);
        internal static string ApproachText(Scheme sc, GameState S)
        {
            string v = Name(sc.Victim); string kr = S != null ? S.RoomName(sc.KillRoom) : "외진 방";
            switch (sc.Approach)
            {
                case "errand": return K($"{v}을(를) \"{sc.Pretext}\" 심부름으로 {kr}에 보내 놓고 뒤따라가");
                case "slip-out": return "잠시 자리를 비운 사이";
                case "dark-strike": return "불이 꺼진 사이 어둠 속에서";
                case "serve": return K($"{v}의 잔을 손수 채워 주며 독을 타");
                case "rendezvous": return K($"{kr}(으)로 불러내");
                case "ambush": return "혼자 남은 틈을 노려";
                case "visit": return "한밤중에 방으로 찾아가";
            }
            if (sc.Approach != null && sc.Approach.StartsWith("method:")) return Methods.GrammarKor(sc.Approach.Substring(7)) ?? sc.Approach;
            return sc.Approach ?? "";
        }

        public static string MotiveKor(string m)
        {
            switch (m)
            {
                case "wish": return "계약한 소원"; case "escape": return "살아서 나가려는 마음"; case "grudge": return "원한"; case "fear": return "두려움";
                case "jealousy": return "질투"; case "secret": return "비밀을 지키려는 마음"; case "protect": return "누군가를 지키려는 마음"; case "love": return "사랑";
                case "defense": return "선수를 치려는 마음"; case "silence": return "입막음"; case "avenge": return "복수"; case "copycat": return "모방 — 규칙 여섯이라는 방패";
            }
            return m;
        }

        // ------------------------------------------------------------------ display: new grammar heads and layers (MethodsClues hook)
        public static string GrammarKor(string g)
        {
            switch (g)
            {
                case "Errand": return "심부름을 보내 놓고 뒤따라가 범행"; case "DarkStrike": return "모두가 있는 방, 불이 꺼진 사이의 일격"; case "Rendezvous": return "단둘이 만나자고 불러냄";
                case "Hosted": return "범인이 직접 연 모임"; case "Framed": return "처음부터 짜 둔 누명"; case "Witness": return "미리 약속해 둔 알리바이 증인";
                case "ClockAlibi": return "시계를 돌려 놓은 알리바이"; case "Helper": return "모르고 도운 사람"; case "Marked": return "어둠 속에서 찾아낼 표식";
                case "Garb": return "피를 받아 낸 겉옷"; case "Copycat": return "첫 사건을 흉내 냄"; case "Turnabout": return "피해자 자신의 계획을 되돌림";
            }
            return null;
        }
        public static (string conn, string fin)? Verb(string g)
        {
            switch (g)
            {
                case "Errand": return ("심부름을 보내 놓고 뒤따라가", "심부름을 보내 놓고 뒤따라간다");
                case "DarkStrike": return ("모두가 모인 방에서 불이 꺼진 틈에 다가가", "모두가 모인 방에서 불이 꺼진 틈에 다가간다");
                case "Rendezvous": return ("단둘이 만나자고 불러내고", "단둘이 만나자고 불러낸다");
                case "Hosted": return ("그 자리를 직접 열고", "그 자리를 직접 연다");
                case "Framed": return ("다른 사람에게 의심이 가도록 미리 꾸미고", "다른 사람에게 의심이 가도록 미리 꾸민다");
                case "Witness": return ("범행 직후 만날 증인을 미리 약속해 두고", "범행 직후 만날 증인을 미리 약속해 둔다");
                case "ClockAlibi": return ("약속한 방의 시계를 늦춰 놓고", "약속한 방의 시계를 늦춰 놓는다");
                case "Helper": return ("모르는 사람에게 불을 끄게 하고", "모르는 사람에게 불을 끄게 한다");
                case "Marked": return ("어둠 속에서 찾을 표식을 선물로 건네고", "어둠 속에서 찾을 표식을 선물로 건넨다");
                case "Garb": return ("피를 받아 낼 겉옷을 걸치고", "피를 받아 낼 겉옷을 걸친다");
                case "Copycat": return ("첫 사건을 흉내 내고", "첫 사건을 흉내 낸다");
                case "Turnabout": return ("상대가 꾸민 계획을 거꾸로 이용하고", "상대가 꾸민 계획을 거꾸로 이용한다");
            }
            return null;
        }
    }
}
