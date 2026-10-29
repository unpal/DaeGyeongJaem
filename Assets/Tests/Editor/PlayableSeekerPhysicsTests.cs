using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PlayableSeekerPhysicsTests
{
    private readonly List<GameObject> objects = new();
    private readonly Vector3 origin = new Vector3(10000f, 10000f, 10000f);

    private GameObject Block(float distance, bool trigger = false)
    {
        var block = new GameObject("Shotgun test collider");
        objects.Add(block);
        block.transform.position = origin + Vector3.forward * distance;
        block.AddComponent<BoxCollider>().isTrigger = trigger;
        return block;
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (GameObject obj in objects)
            if (obj != null) Object.DestroyImmediate(obj);
        objects.Clear();
    }

    [Test]
    public void ShotStopsAtNearestWallRegardlessOfCreationOrder()
    {
        Block(7f);
        Block(4f);
        Physics.SyncTransforms();
        bool hitPlayer = Shotgun.FireAt(origin, Vector3.forward, null, out Vector3 end);
        Assert.That(hitPlayer, Is.False);
        Assert.That((end - origin).z, Is.EqualTo(3.25f).Within(0.01f));
    }

    [Test]
    public void ShotIgnoresOwnChildCollidersAndPortalTriggers()
    {
        var shooter = new GameObject("Seeker test root");
        objects.Add(shooter);
        Block(1f).transform.SetParent(shooter.transform, true);
        Block(2f, true);
        Block(5f);
        Physics.SyncTransforms();
        Shotgun.FireAt(origin, Vector3.forward, shooter.transform, out Vector3 end);
        Assert.That((end - origin).z, Is.EqualTo(4.25f).Within(0.01f));
    }

    [Test]
    public void ShotCannotReachBeyondTenMeters()
    {
        Block(12f);
        Physics.SyncTransforms();
        Assert.That(Shotgun.FireAt(origin, Vector3.forward, null, out Vector3 end), Is.False);
        Assert.That(Vector3.Distance(origin, end), Is.EqualTo(10f).Within(0.01f));
    }
}
