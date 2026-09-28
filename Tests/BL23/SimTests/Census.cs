using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;

/// <summary>
/// AT-051: counts how often each of the 36 grammar combinations (IG01–12 × 3 real conditions) actually happens
/// in unscripted headless campaigns. A combination counts only when its real state/event signature occurs.
/// </summary>
public static partial class Program
{
    sealed class Combo { public string Id, Desc; public int Count; public string First; }

    static int Census(string[] args)
    {
        int seeds = args.Length > 1 ? int.Parse(args[1]) : 8; int days = args.Length > 2 ? int.Parse(args[2]) : 8;
        var fri = Array.IndexOf(args, "force"); if (fri >= 0 && fri + 1 < args.Length) Rules.ForceRule = args[fri + 1];
        var combos = new List<Combo>();
        Combo C(string id, string d) { var c = new Combo { Id = id, Desc = d }; combos.Add(c); return c; }
        var c01a = C("IG01-a", "빌려 간 물건이 오래 돌아오지 않아 빌린 사람을 의심"); var c01b = C("IG01-b", "엉뚱한 곳에 반납 → 소지=절도 오해"); var c01c = C("IG01-c", "반납·되찾음으로 오해 해소");
        var c02a = C("IG02-a", "초대 수정(장소/시각) 후 재통지"); var c02b = C("IG02-b", "옛 안내대로 간 사람이 바람맞음"); var c02c = C("IG02-c", "엇갈린 초대 해명");
        var c03a = C("IG03-a", "녹음기 재생으로 목소리=재실 오인"); var c03b = C("IG03-b", "권능 메아리로 소리 시각 조작"); var c03c = C("IG03-c", "잔향(CH22) 소음 속 사건");
        var c04a = C("IG04-a", "사건 창 안의 출입 기록 존재"); var c04b = C("IG04-b", "기록기 방에서 범행, 범인은 기록 없음(다른 문)"); var c04c = C("IG04-c", "기록기에 범인 출입이 남음");
        var c05a = C("IG05-a", "사생활 문단속(나중에 잠긴 방)"); var c05b = C("IG05-b", "범인이 밖에서 잠가 만든 밀실"); var c05c = C("IG05-c", "유스티 야간 잠금");
        var c06a = C("IG06-a", "외투를 빌려 입음(옷=신원 오인)"); var c06b = C("IG06-b", "권능 외피로 다른 사람 옷차림"); var c06c = C("IG06-c", "무대 의상 변장");
        var c07a = C("IG07-a", "수리 미완 상태에서 정전 범행"); var c07b = C("IG07-b", "수리 완료 허위 주장"); var c07c = C("IG07-c", "수리 완료(작업 Rev 기록)");
        var c08a = C("IG08-a", "시신 이동(은폐)"); var c08b = C("IG08-b", "구조를 위한 운반"); var c08c = C("IG08-c", "프레스대로 운반");
        var c09a = C("IG09-a", "시계가 어긋난 방에서 사건"); var c09b = C("IG09-b", "유스티 시계 조정 기록"); var c09c = C("IG09-c", "시간차 공지(CH20)");
        var c10a = C("IG10-a", "범인이 대리 전달을 부탁"); var c10b = C("IG10-b", "전달자가 뒤늦게 깨닫고 두려워함"); var c10c = C("IG10-c", "전달자가 진술에서 숨김");
        var c11a = C("IG11-a", "한 챕터 두 사건, 서로 다른 가해자"); var c11b = C("IG11-b", "한 챕터 두 사건, 같은 가해자"); var c11c = C("IG11-c", "CH21 챕터의 다중 피해");
        var c12a = C("IG12-a", "실종자 수색(응답 없는 방)"); var c12b = C("IG12-b", "사후 훼손으로 흐려진 신원·사인"); var c12c = C("IG12-c", "변장한 인물 목격(신원 불확실)");
        for (int si = 0; si < seeds; si++)
        {
            ulong seed = 900001UL + (ulong)si * 7919UL;
            var sim = Simulation.NewCampaign(seed, 4); sim.Headless = true; var S = sim.S; S.Phase = Phase.Daily;
            var seen = new HashSet<long>(); var seenInc = new HashSet<string>(); var seenCh = new HashSet<string>();
            void Hit(Combo c) { c.Count++; if (c.First == null) c.First = $"seed {seed} {ClockFmt.DayHM(S.Clock)}"; }
            void Scan()
            {
                foreach (var e in S.Ledger)
                {
                    if (!seen.Add(e.Seq)) continue;
                    switch (e.Type)
                    {
                        case "ItemMissingNoticed": { var l = S.Loans.FirstOrDefault(x => e.Data != null && e.Data.StartsWith(x.Id)); if (l != null && l.LeftRoom >= 0) Hit(c01b); else Hit(c01a); break; }
                        case "ItemFound": case "Return": Hit(c01c); break;
                        case "GatheringRevised": Hit(c02a); break; case "GatheringStoodUp": Hit(c02b); break; case "GatheringExplained": Hit(c02c); break;
                        case "RecorderPlay": Hit(c03a); break; case "EchoPlay": Hit(c03b); break;
                        case "Lock": if (e.Data != null && e.Data.Contains("사생활")) Hit(c05a); else if (e.Actor == Cast.Butler) Hit(c05c); break;
                        case "LockedRoomMade": Hit(c05b); break;
                        case "LendCoat": Hit(c06a); break; case "GuiseAs": Hit(c06b); break; case "DisguiseOn": Hit(c06c); break;
                        case "Lie": if (e.Data != null && e.Data.Contains("수리")) Hit(c07b); break;
                        case "CarryEnd": if (e.Data == "dump") Hit(c08a); else if (e.Data == "pressbed") Hit(c08c); break;
                        case "Rescued": Hit(c08b); break;
                        case "ClockAdjust": Hit(c09b); break; case "NoticeDelivered": Hit(c09c); break;
                        case "CourierAsk": Hit(c10a); break; case "CourierRealised": Hit(c10b); break;
                        case "Omit": if (e.Data == "courier-fear") Hit(c10c); break;
                        case "Report": if (e.Data == "missing-bell") Hit(c12a); break;
                        case "PostmortemDamage": Hit(c12b); break;
                    }
                }
                foreach (var r in S.Repairs.Where(r => r.Done)) if (seenCh.Add("rep:" + r.Id)) Hit(c07c);
                // per incident (once confirmed)
                foreach (var inc in S.Incidents.Values.Where(i => i.Confirmed))
                {
                    if (!seenInc.Add(inc.Id)) continue;
                    var room = S.Layout.Room(inc.CauseRoom >= 0 ? inc.CauseRoom : inc.DeathRoom);
                    if (room != null)
                    {
                        double t0 = inc.CauseClock - 60, t1 = inc.CauseClock + 30;
                        var logged = S.DoorLoggers.Where(kv => S.Layout.Furniture[kv.Value].Room == room.Id).ToList();
                        if (logged.Count > 0)
                        {
                            var recs = S.DeviceLog.Where(d => d.Room == room.Id && d.Clock >= t0 && d.Clock <= t1).ToList();
                            if (recs.Count > 0) Hit(c04a);
                            if (inc.Culprit != null && recs.Any(d => d.Actor == inc.Culprit)) Hit(c04c); else if (inc.Culprit != null) Hit(c04b);
                        }
                        if (room.Furniture.Any(f => S.ClockOffset.TryGetValue(f, out var off) && Math.Abs(off) > 0.5)) Hit(c09a);
                    }
                    if (S.Ch.Rules.Any(r => r.Rule == "CH22")) Hit(c03c);
                    var plan = inc.PlanId != null && S.Plans.TryGetValue(inc.PlanId, out var p) ? p : null;
                    if (plan != null && plan.Grammar != null && plan.Grammar.StartsWith("Blackout") && S.Repairs.Any(r => !r.Done && S.Layout.Furniture.ElementAtOrDefault(r.Furniture)?.Type == "Switchboard")) Hit(c07a);
                }
                foreach (var g in S.Incidents.Values.GroupBy(i => i.Loop + ":" + i.Chapter).Where(g => g.Count() >= 2))
                {
                    if (!seenCh.Add("ch:" + g.Key)) continue;
                    var cul = g.Select(i => i.Culprit).Where(x => x != null).ToList();
                    if (cul.Distinct().Count() >= 2) Hit(c11a); else if (cul.Count >= 2) Hit(c11b);
                    if (S.Ch.Rules.Any(r => r.Rule == "CH21")) Hit(c11c);
                }
                foreach (var k in S.Know) foreach (var s in k.Value.Sightings.Where(s => s.Disguise != null)) if (seenCh.Add("dis:" + k.Key + ":" + s.Root)) Hit(c12c);
            }
            long ticks = 0; double end = days * 1440;
            while (S.Clock < end && ticks < 8_000_000)
            {
                if (S.Phase == Phase.Trial) { Scan(); TrialSystem.RunHeadless(sim, true); Replay.BuildSegments(sim); Settlements.AfterReveal(sim); if (S.Phase == Phase.LoopEpilogue) { Settlements.NextLoop(sim); S.Phase = Phase.Daily; end = S.Clock + days * 1440; } continue; }
                sim.Step(); ticks++; if (ticks % 5000 == 0) Scan(); if (S.Out.Count > 2000) S.Out.Clear();
            }
            Scan();
            Console.WriteLine($"seed {seed} done ({ClockFmt.DayHM(S.Clock)}) faults={sim.Faults}"); foreach (var l in S.DevLog.Where(x => x.Contains("EXC")).Take(3)) Console.WriteLine("   " + l);
        }
        Console.WriteLine();
        Console.WriteLine("combo,count,first,description");
        foreach (var c in combos) Console.WriteLine($"{c.Id},{c.Count},{c.First ?? "-"},{c.Desc}");
        Console.WriteLine($"observed {combos.Count(c => c.Count > 0)}/{combos.Count}");
        return 0;
    }
}
