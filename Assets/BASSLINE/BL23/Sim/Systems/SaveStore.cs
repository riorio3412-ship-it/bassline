using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BL23.Sim
{
    /// <summary>Whole-state saves: temp file → flush → checksum → atomic replace; previous valid save kept as .previous.</summary>
    public static class SaveStore
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None, FloatFormatHandling = FloatFormatHandling.DefaultValue, Formatting = Formatting.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public static string Serialize(GameState s) => JsonConvert.SerializeObject(s, Settings);
        public static GameState Deserialize(string json) => JsonConvert.DeserializeObject<GameState>(json, Settings);

        // Fastest: a few percent bigger than Optimal, several times quicker on a multi-megabyte state (saves sit on transitions)
        static string Pack(string json)
        {
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Fastest, true)) { var b = Encoding.UTF8.GetBytes(json); gz.Write(b, 0, b.Length); }
                return Convert.ToBase64String(ms.ToArray());
            }
        }
        static string Unpack(string b64)
        {
            using (var ms = new MemoryStream(Convert.FromBase64String(b64.Trim())))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var sr = new StreamReader(gz, Encoding.UTF8)) return sr.ReadToEnd();
        }

        public static string Hash(string text) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", ""); }

        public sealed class SlotInfo { public string Path; public bool Exists; public string Label; public DateTime Saved; public int Loop, Chapter; public string Clock; public bool Corrupt; }

        public static void Write(string path, GameState s, string label)
        {
            Flush();
            var job = Prepare(path, s, label);
            Commit(job);
        }

        /// <summary>The part that must read the live state (JSON and the header fields), on the caller's thread.</summary>
        sealed class Job { public string Path, Json, Head; public DateTime Saved; }
        static Job Prepare(string path, GameState s, string label)
        {
            var saved = DateTime.Now;
            return new Job { Path = path, Json = Serialize(s), Saved = saved,
                Head = JsonConvert.SerializeObject(new { v = s.Schema, label, loop = s.Loop, chapter = s.Chapter, clock = ClockFmt.DayHM(s.Clock), phase = s.Phase.ToString(), hash = "#HASH#", saved, z = 1 }) };
        }

        /// <summary>Compress, hash, write to a temp file, flush to disk, check the bytes, then swap it in (keeping the previous save).</summary>
        static void Commit(Job job)
        {
            // the header stays readable text (slot lists read only it); the body is gzip+base64 (~8x smaller), hash covers the raw JSON
            string header = job.Head.Replace("#HASH#", Hash(job.Json));
            string payload = header + "\n" + Pack(job.Json);
            string path = job.Path;
            var dir = Path.GetDirectoryName(path); if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs, new UTF8Encoding(false))) { sw.Write(payload); sw.Flush(); fs.Flush(true); }
            // verify what hit the disk before replacing: the exact bytes we meant to write (the hash inside them was taken from the
            // same JSON, so a full re-parse of the state adds nothing but a second of stutter)
            var check = File.ReadAllText(tmp, Encoding.UTF8);
            if (!string.Equals(check, payload, StringComparison.Ordinal)) throw new IOException("save verification failed");
            if (File.Exists(path)) { File.Copy(path, path + ".previous", true); File.Replace(tmp, path, null); }
            else File.Move(tmp, path);
        }

        // ---- background saves (autosaves on transitions): the state is turned into JSON at once; the rest runs on a worker
        static System.Threading.Tasks.Task _pending; static readonly object _lock = new object();
        static string _doneNote; static long _doneMs = -1; static bool _doneOk;

        /// <summary>Serializes now (the caller's thread), writes in the background. One write at a time; a second one waits for
        /// the first. Poll <see cref="TakeResult"/> for how it went.</summary>
        public static void WriteInBackground(string path, GameState s, string label)
        {
            Flush();
            var job = Prepare(path, s, label);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            lock (_lock) { _doneMs = -1; _doneNote = null; }   // an older result nobody took is not this save's
            lock (_lock)
                _pending = System.Threading.Tasks.Task.Run(() =>
                {
                    try { Commit(job); lock (_lock) { _doneOk = true; _doneNote = null; _doneMs = sw.ElapsedMilliseconds; } }
                    catch (Exception e) { lock (_lock) { _doneOk = false; _doneNote = e.Message; _doneMs = sw.ElapsedMilliseconds; } }
                });
        }

        /// <summary>Wait for a background save still being written (reads, writes and quitting call this first).</summary>
        public static void Flush()
        {
            System.Threading.Tasks.Task t; lock (_lock) t = _pending;
            if (t == null) return;
            try { t.Wait(); } catch (Exception) { }
            lock (_lock) if (_pending == t) _pending = null;
        }

        /// <summary>A finished background save, once: (ok, milliseconds on the worker, error text).</summary>
        public static bool TakeResult(out bool ok, out long ms, out string note)
        {
            lock (_lock)
            {
                ok = _doneOk; ms = _doneMs; note = _doneNote;
                if (_doneMs < 0) return false;
                _doneMs = -1; _doneNote = null; return true;
            }
        }

        public static bool Verify(string payload, out GameState state)
        {
            state = null;
            int nl = payload.IndexOf('\n'); if (nl < 0) return false;
            try
            {
                var h = JObject.Parse(payload.Substring(0, nl));
                string json = payload.Substring(nl + 1);
                if (h["z"] != null && (int)h["z"] == 1) json = Unpack(json);
                if ((string)h["hash"] != Hash(json)) return false;
                state = Deserialize(json);
                return state != null;
            }
            catch { return false; }
        }

        /// <summary>Load a slot; falls back to .previous on corruption. Returns null and a reason if nothing valid.</summary>
        public static GameState Read(string path, out string note)
        {
            note = null; Flush();
            foreach (var p in new[] { path, path + ".previous" })
            {
                if (!File.Exists(p)) continue;
                try { if (Verify(File.ReadAllText(p, Encoding.UTF8), out var st)) { if (p != path) note = "저장 파일이 손상되어, 그 전에 저장해 둔 파일을 대신 불러왔습니다."; return st; } }
                catch (Exception e) { note = e.Message; }
                note = note ?? "저장 파일이 손상되었습니다.";
            }
            return null;
        }

        public static SlotInfo Info(string path)
        {
            Flush();
            var info = new SlotInfo { Path = path, Exists = File.Exists(path) };
            if (!info.Exists) return info;
            try
            {
                using (var sr = new StreamReader(path, Encoding.UTF8))
                {
                    var line = sr.ReadLine(); var h = JObject.Parse(line);
                    info.Label = (string)h["label"]; info.Loop = (int)h["loop"]; info.Chapter = (int)h["chapter"]; info.Clock = (string)h["clock"]; info.Saved = (DateTime)h["saved"];
                }
            }
            catch { info.Corrupt = true; }
            return info;
        }
    }
}
