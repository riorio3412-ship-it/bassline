using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Marks a restraint loop that lives under a limb bone but belongs to a toggle group.</summary>
    public sealed class RestraintPart : MonoBehaviour
    {
        public GameObject Group;
        void LateUpdate() { bool on = Group != null && Group.activeSelf; var r = GetComponent<MeshRenderer>(); if (r != null && r.enabled != on) r.enabled = on; }
    }
}
