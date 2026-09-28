using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Physicality
{
    /// <summary>One in-flight visual transfer per item. Never changes simulation ownership or duplicates an item.</summary>
    [DisallowMultipleComponent]
    public sealed class PhysicalItemTransfer : MonoBehaviour
    {
        ItemView _item;
        PhysicalActionController _driver;
        int _generation;
        bool _pending;
        string _expectedHolder;
        float _elapsed;

        static PhysicalItemTransfer For(ItemView item)
        {
            var transfer = item.GetComponent<PhysicalItemTransfer>() ?? item.gameObject.AddComponent<PhysicalItemTransfer>();
            transfer._item = item;
            return transfer;
        }

        static PhysicalActionController Actions(ActorRig rig)
        {
            if (rig == null) return null;
            var actions = rig.GetComponent<PhysicalActionController>() ?? rig.gameObject.AddComponent<PhysicalActionController>();
            actions.Bind(rig);
            return actions;
        }

        public static void Attach(ItemView item, ActorRig recipient, bool left, bool firstPerson)
        {
            if (item == null || recipient == null || item.Visual == null) return;
            var transfer = For(item);
            if (transfer._pending && transfer._expectedHolder == recipient.ActorId) return;
            Transform anchor = left ? recipient.HandAnchorL : recipient.HandAnchorR;
            if (anchor == null) { item.AttachTo(recipient.transform, firstPerson); return; }
            if (item.HeldAnchor == anchor && !transfer._pending)
            {
                Actions(recipient).SetCarriedObject(item.Visual.transform, left, Mass(item));
                return;
            }
            var previous = item.HeldAnchor != null ? item.HeldAnchor.GetComponentInParent<ActorRig>() : null;
            bool formerLeft = previous != null && item.HeldAnchor == previous.HandAnchorL;
            if (!item.Held && !item.Visual.activeSelf && !item.InContainer)
            {
                // An item drawn from a pocket follows the owner's body, not the world position
                // where it was hidden before the actor walked to another room.
                float height = recipient.Height > 0.5f ? recipient.Height : 1.75f;
                item.Visual.transform.position = recipient.transform.TransformPoint(new Vector3(left ? -0.18f : 0.18f, height * 0.51f, 0.05f));
            }
            transfer.Begin();
            int generation = transfer._generation;
            System.Action contact = () => transfer.Complete(generation, true);
            System.Action<bool> finished = ok => transfer.Complete(generation, false);
            bool began;
            if (previous != null && previous != recipient)
            {
                transfer._driver = Actions(previous);
                began = transfer._driver.BeginHandover(Actions(recipient), formerLeft, contact, finished, left);
            }
            else
            {
                transfer._driver = Actions(recipient);
                began = transfer._driver.BeginPickup(item.Visual.transform, left, contact, finished);
            }
            if (!began) transfer.Complete(generation, false);
        }

        public static void Release(ItemView item, ActorRig formerOwner, bool left)
        {
            if (item == null || item.Visual == null) return;
            var state = Session.I?.S?.I(item.Id);
            if (state != null && state.Holder != null)
            {
                Actor holder = Session.I.S.A(state.Holder);
                var recipient = item.World?.ViewOf(state.Holder)?.Rig;
                if (recipient != null && (holder?.HandL == item.Id || holder?.HandR == item.Id))
                {
                    Attach(item, recipient, holder.HandL == item.Id, IsFirstPerson(recipient));
                    return;
                }
            }
            var transfer = For(item);
            if (state?.Holder == null && !item.Held)
            {
                bool wasPending = transfer._pending;
                Abort(item);
                Actions(formerOwner)?.SetCarriedObject(null, left);
                if (wasPending) item.ReleaseFromContact();
                return; // a throw or an already loose item must keep its physical trajectory
            }
            if (transfer._pending && transfer._expectedHolder == state?.Holder) return;
            transfer.Begin();
            int generation = transfer._generation;
            transfer._driver = Actions(formerOwner);
            var motor = formerOwner != null ? formerOwner.GetComponentInParent<PhysicalCharacter>() : null;
            if (state?.Holder == null && (motor != null && motor.BlocksActions || transfer._driver != null && !transfer._driver.CanUseHand(left)))
            {
                transfer.Complete(generation, true); // involuntary release falls from the actual hand
                return;
            }
            Vector3 point = PlacePoint(item, state);
            bool began = state?.Holder == null && transfer._driver != null && item.Held &&
                transfer._driver.BeginPlace(point, left, () => transfer.Complete(generation, true),
                    ok => transfer.Complete(generation, false));
            if (!began) transfer.Complete(generation, false);
        }

        public static void Abort(ItemView item)
        {
            if (item == null) return;
            var transfer = item.GetComponent<PhysicalItemTransfer>();
            if (transfer == null || !transfer._pending) return;
            transfer._generation++; transfer._pending = false;
            item.EndContactTransfer();
            var driver = transfer._driver; transfer._driver = null;
            if (driver != null) driver.Cancel();
        }

        void Begin()
        {
            _generation++; // invalidate old callbacks before cancelling the old action
            var previous = _driver; _driver = null;
            if (_pending && previous != null) previous.Cancel();
            _pending = true; _elapsed = 0f;
            _expectedHolder = Session.I?.S?.I(_item.Id)?.Holder;
            _item.BeginContactTransfer();
        }

        void Complete(int generation, bool atContact)
        {
            if (!_pending || generation != _generation || _item == null) return;
            _pending = false;
            var oldRig = _item.HeldAnchor != null ? _item.HeldAnchor.GetComponentInParent<ActorRig>() : null;
            bool oldLeft = oldRig != null && _item.HeldAnchor == oldRig.HandAnchorL;
            var oldMotor = oldRig != null ? oldRig.GetComponentInParent<PhysicalCharacter>() : null;
            if (oldRig != null) Actions(oldRig).SetCarriedObject(null, oldLeft);
            _item.EndContactTransfer();
            var state = Session.I?.S?.I(_item.Id);
            if (state == null) { _item.gameObject.SetActive(false); return; }
            var actor = state.Holder != null ? Session.I.S.A(state.Holder) : null;
            var rig = actor != null ? _item.World?.ViewOf(actor.Id)?.Rig : null;
            bool left = actor != null && actor.HandL == _item.Id;
            bool inHand = actor != null && (left || actor.HandR == _item.Id);
            if (rig != null && inHand)
            {
                var anchor = (left ? rig.HandAnchorL : rig.HandAnchorR) ?? rig.transform;
                _item.AttachTo(anchor, IsFirstPerson(rig));
                Actions(rig).SetCarriedObject(_item.Visual.transform, left, Mass(_item));
            }
            else if (state.Holder == null && (atContact || oldMotor != null && oldMotor.BlocksActions))
                _item.ReleaseFromContact(oldMotor?.Body != null && !oldMotor.Body.isKinematic ? oldMotor.Body.linearVelocity : Vector3.zero);
            else
            {
                _item.Detach();
                _item.Refresh("contact_complete");
            }
        }

        void LateUpdate()
        {
            if (!_pending || _item == null || PhysicalCharacter.WorldPaused) return;
            _elapsed += Time.deltaTime;
            var state = Session.I?.S?.I(_item.Id);
            if (state?.Holder != _expectedHolder || _elapsed > 3f) Complete(_generation, false);
        }

        void OnDisable() { if (_pending) Complete(_generation, false); }
        static bool IsFirstPerson(ActorRig rig) => rig.ActorId == Cast.Player && Session.I?.Player != null && Session.I.Player.FirstPerson;
        static float Mass(ItemView item) => Mathf.Max(0.05f, Session.I?.S?.I(item.Id)?.Def?.Mass ?? 1f);

        static Vector3 PlacePoint(ItemView item, Item state)
        {
            Vector3 point = state != null && item.World != null ? item.World.ToWorld(state.Pos) : item.Visual.transform.position;
            var hits = Physics.RaycastAll(point + Vector3.up * 1.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(item.transform) ||
                    hit.collider.GetComponentInParent<ActorRig>() != null || hit.normal.y < 0.45f || hit.distance >= best) continue;
                best = hit.distance; point.y = hit.point.y + 0.025f;
            }
            return point;
        }
    }
}
