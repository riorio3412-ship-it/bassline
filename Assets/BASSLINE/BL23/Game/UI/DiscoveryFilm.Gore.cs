using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// The film's one window onto the gore renderer (Game/Gore): GoreScene's hotspots, readiness and film mode, and
    /// GorePieces. While the renderer has nothing for a victim (no pieces skinned yet) the film falls back to the kernel's
    /// own severed-part items and their plain views, so it works from the murder-sim's interim items too.
    /// </summary>
    public sealed partial class DiscoveryFilm
    {
        internal struct FilmHotspot { public GoreKind Kind; public Vector3 Pos, Normal; public float Size, Weight; public int Room; public string Label; }
        internal struct FilmPiece { public SeverPart Part; public string ItemId; public Transform T; public Vector3 Center, CapNormal; public float Radius; public int Room; public bool Hidden; public string Surface; }

        /// <summary>GoreScene.Ready (atlas loaded and first build done).</summary>
        internal static bool GoreReady() => GoreScene.Ready;

        /// <summary>GoreScene.I.Hotspots(victim): pool centre, wall spray, cast-off, toppled furniture, handprint, nails, saw kerf.</summary>
        internal static List<FilmHotspot> Hotspots(string victim)
        {
            var list = new List<FilmHotspot>();
            try
            {
                var hs = GoreScene.I != null ? GoreScene.I.Hotspots(victim) : null; if (hs == null) return list;
                foreach (var h in hs) list.Add(new FilmHotspot { Kind = h.Kind, Pos = h.Pos, Normal = h.Normal, Size = h.Size, Weight = h.Weight, Room = h.Room, Label = h.Label });
            }
            catch (Exception ex) { Debug.LogWarning("[DiscoveryFilm] hotspots: " + ex.Message); }
            return list;
        }

        /// <summary>GorePieces.Of(victim), or — while the renderer has none — the kernel's severed-part items and their plain views.</summary>
        internal static List<FilmPiece> PiecesOf(string victim)
        {
            var list = new List<FilmPiece>();
            try
            {
                var ps = GorePieces.Of(victim);
                if (ps != null) foreach (var p in ps) list.Add(new FilmPiece { Part = p.Part, ItemId = p.ItemId, T = p.T, Center = p.Center, CapNormal = p.CapNormal, Radius = p.Radius, Room = p.Room, Hidden = p.Hidden, Surface = p.Surface });
                if (list.Count > 0) return list;
            }
            catch (Exception ex) { Debug.LogWarning("[DiscoveryFilm] pieces: " + ex.Message); list.Clear(); }
            // the kernel's pieces as they are drawn today (a wrapped bundle): centre of the view, no cap direction
            var ses = Session.I; if (ses?.Sim == null || ses.World == null) return list;
            foreach (var it in Gore.PiecesOf(ses.S, victim))
            {
                if (!ses.World.Items.TryGetValue(it.Id, out var iv) || iv == null) continue;
                var c = iv.transform.position; var r = iv.GetComponentsInChildren<Renderer>(); if (r.Length > 0) { var b = r[0].bounds; foreach (var x in r) b.Encapsulate(x.bounds); c = b.center; }
                SeverPart part; if (!Gore.TryPart(it, out part)) Enum.TryParse(it.Note ?? "", out part);
                list.Add(new FilmPiece { Part = part, ItemId = it.Id, T = iv.transform, Center = c, CapNormal = Vector3.zero, Radius = 0.06f, Room = it.Room, Hidden = it.Hidden || it.Holder != null, Surface = string.Join(",", it.Surface) });
            }
            return list;
        }

        /// <summary>GoreScene.I.FilmMode(on): all of the victim's gore drawn regardless of room culling.</summary>
        internal static void GoreFilmMode(bool on) { try { GoreScene.I?.FilmMode(on); } catch (Exception ex) { Debug.LogWarning("[DiscoveryFilm] film mode: " + ex.Message); } }

        /// <summary>GorePieces.LastBakeMs (probe).</summary>
        internal static float GoreLastBakeMs() => GorePieces.LastBakeMs;

        /// <summary>GoreScene.OnFirstGore (the film warms its clips on the first blood; gore-render never references the film).</summary>
        static void LinkFirstGore(Action a) { GoreScene.OnFirstGore -= a; GoreScene.OnFirstGore += a; }
    }
}
