using System;
using System.Linq;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    public sealed partial class MansionView
    {
        void BuildCourtroom()
        {
            var cr = Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.Courtroom);
            if (cr == null) return;
            try { Courtroom = CourtroomView.Build(this, Rooms[cr.Id]); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
