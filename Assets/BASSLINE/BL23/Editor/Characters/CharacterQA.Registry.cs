using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// charpolish step 0 (frozen; change only through the integrator): extension points of CharacterQA so each
    /// implementer adds sheets and automatic checks in its OWN partial file (CharacterQA.Face.cs, .Proportions.cs,
    /// .Motion.cs, .Physics.cs, .Cast.cs) without editing CharacterQA.cs.
    ///
    /// Register sheets from a private static method whose name starts with "Register_" (called once per ShotsQuick run
    /// via reflection), e.g.  static void Register_Face() { AddSheet("faceqa", ids => ...); }
    /// A sheet named in -qaSheets runs after the built-in ones, with the -qaIds list.
    ///
    /// Automatic checks: call Check(...) from a sheet. Every result is logged as "[QA-CHECK] PASS|FAIL ..." and appended
    /// to Shots/qa_checks.tsv (time, sheet/check, id, pass, value, limit, detail).
    /// charpolish step 0b: a check whose name starts with "!" is a BLOCKER. With -qaStrict the run throws at the end when a
    /// blocker failed; with -qaStrictAll it throws when any check failed (run.sh then prints the exception), so a gate can
    /// be scripted on it. Non-blocker failures are reported only.
    /// </summary>
    public static partial class CharacterQA
    {
        static readonly Dictionary<string, Action<string[]>> _extraSheets = new Dictionary<string, Action<string[]>>();
        static int _checkFails, _checkCount, _blockerFails;
        static bool _registered;

        /// <summary>Batch: bake (-bakeIds) then render sheets (-qaIds / -qaSheets) in ONE editor session (saves ~1 min of
        /// Unity start-up and one lock cycle): -executeMethod BL23.EditorTools.Characters.CharacterQA.BakeThenShots</summary>
        public static void BakeThenShots() { CharacterBaker.BakeSome(); ShotsQuick(); }

        /// <summary>Registers a sheet name for -qaSheets (call from a Register_* method).</summary>
        static void AddSheet(string name, Action<string[]> run) { _extraSheets[name] = run; }

        static void RegisterAll()
        {
            if (_registered) return;
            _registered = true;
            foreach (var m in typeof(CharacterQA).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                if (m.Name.StartsWith("Register_") && m.GetParameters().Length == 0) m.Invoke(null, null);
        }

        static void RunExtraSheets(string[] ids, string[] sheets)
        {
            RegisterAll();
            foreach (var s in sheets)
                if (_extraSheets.TryGetValue(s, out var run))
                {
                    try { run(ids); }
                    catch (Exception e) { Debug.LogException(e); Check("!sheet:" + s, "-", false, 0f, 0f, "sheet threw " + e.GetType().Name); }
                }
        }

        /// <summary>Records one automatic check (value vs limit are informational; pass decides). Name it "!..." to make it a
        /// blocker for -qaStrict.</summary>
        public static void Check(string check, string id, bool pass, float value, float limit, string detail = "")
        {
            _checkCount++; if (!pass) { _checkFails++; if (check != null && check.StartsWith("!")) _blockerFails++; }
            string line = $"{DateTime.Now:HH:mm:ss}\t{check}\t{id}\t{(pass ? "PASS" : "FAIL")}\t{value:0.####}\t{limit:0.####}\t{detail}";
            Debug.Log("[QA-CHECK] " + (pass ? "PASS " : "FAIL ") + check + " " + id + " value " + value.ToString("0.####") + " limit " + limit.ToString("0.####") + " " + detail);
            try
            {
                string path = Path.Combine(ShotDir, "qa_checks.tsv");
                if (!File.Exists(path)) File.WriteAllText(path, "time\tcheck\tid\tresult\tvalue\tlimit\tdetail\n");
                File.AppendAllText(path, line + "\n");
            }
            catch (Exception e) { Debug.LogWarning("[QA-CHECK] could not write qa_checks.tsv: " + e.Message); }
        }

        static void FinishChecks(string[] args)
        {
            if (_checkCount == 0) return;
            Debug.Log($"[QA-CHECK] summary: {_checkCount - _checkFails}/{_checkCount} passed, {_blockerFails} blocker failure(s)");
            if (_blockerFails > 0 && (args.Contains("-qaStrict") || args.Contains("-qaStrictAll"))) throw new Exception($"[QA-CHECK] {_blockerFails} blocker check(s) failed (see Shots/qa_checks.tsv)");
            if (_checkFails > 0 && args.Contains("-qaStrictAll")) throw new Exception($"[QA-CHECK] {_checkFails} check(s) failed (see Shots/qa_checks.tsv)");
        }
    }
}
