using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Script.item
{
    // Host simulates dropped items; every client receives their position through Fusion.
    public sealed class PrototypeWorldItem : NetworkBehaviour
    {
        [Networked] private Vector3 Position { get; set; }
        [Networked] private TickTimer PickupDelay { get; set; }
        private Vector3 velocity;
        private bool moving;
        private Transform releasedBy;
        private readonly RaycastHit[] hits = new RaycastHit[64];

        public override void Spawned()
        {
            if (Object.HasStateAuthority) Position = transform.position;
        }

        public void Release(Vector3 launchVelocity, Transform owner)
        {
            if (!Object.HasStateAuthority) return;
            Position = transform.position;
            velocity = launchVelocity;
            releasedBy = owner;
            moving = true;
            PickupDelay = TickTimer.CreateFromSeconds(Runner, 0.7f);
        }

        public bool CanPickup => Object != null && Object.IsValid && PickupDelay.ExpiredOrNotRunning(Runner);

        public static bool AllowsPickup(Component item)
        {
            var world = item.GetComponent<PrototypeWorldItem>();
            return world == null || world.CanPickup;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || !moving) return;
            float delta = Runner.DeltaTime;
            velocity += Vector3.down * 9.81f * delta;
            Vector3 step = velocity * delta;
            float distance = step.magnitude;
            if (distance < 0.0001f) return;
            int count = gameObject.scene.GetPhysicsScene().SphereCast(Position, 0.15f, step / distance,
                hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].transform;
                if (hit.IsChildOf(transform) || (releasedBy != null && hit.IsChildOf(releasedBy))) continue;
                if (hits[i].distance > nearest) continue;
                nearest = Mathf.Max(0, hits[i].distance - 0.01f);
                blocked = true;
            }
            Position += step.normalized * nearest;
            transform.position = Position;
            if (blocked) { moving = false; velocity = Vector3.zero; }
            if (Position.y < -200f) Runner.Despawn(Object);
        }

        public override void Render()
        {
            if (!Object.HasStateAuthority) transform.position = Position;
        }
    }
}
