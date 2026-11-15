using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SeekerCatchPhysicsTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly HashSet<Transform> targets = new HashSet<Transform>();
    private readonly Vector3 origin = new Vector3(11000f, 11000f, 11000f);
    private readonly SeekerCatchQuery query = new SeekerCatchQuery();

    private GameObject Box(Vector3 offset, bool target = false, bool trigger = false)
    {
        GameObject obj = new GameObject("Catch physics test");
        objects.Add(obj);
        obj.transform.position = origin + offset;
        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = Vector3.one * 0.2f;
        collider.isTrigger = trigger;
        if (target) targets.Add(obj.transform);
        return obj;
    }

    private Transform Find(Transform seeker = null, Vector3? direction = null)
    {
        Physics.SyncTransforms();
        return query.FindTarget(Physics.defaultPhysicsScene, origin, direction ?? Vector3.forward,
            2f, 50f, seeker, collider => targets.Contains(collider.transform) ? collider.transform : null);
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (GameObject obj in objects)
            if (obj != null) Object.DestroyImmediate(obj);
        objects.Clear();
        targets.Clear();
    }

    [Test]
    public void SwingReachesAnOffCenterTargetInFront()
    {
        GameObject target = Box(new Vector3(0.7f, 0f, 1.2f), true);
        Assert.That(Find(), Is.EqualTo(target.transform));
    }

    [TestCase(0f, 0f, 2.2f)]
    [TestCase(0f, 0f, -1f)]
    [TestCase(1.5f, 0f, 0.2f)]
    [TestCase(0f, 2.2f, 0f)]
    public void SwingRejectsTargetsOutsideReachOrForwardArc(float x, float y, float z)
    {
        Box(new Vector3(x, y, z), true);
        Assert.That(Find(), Is.Null);
    }

    [Test]
    public void OnlyNearestVisibleTargetIsSelected()
    {
        Box(Vector3.forward * 1.8f, true);
        GameObject near = Box(Vector3.forward, true);
        Assert.That(Find(), Is.EqualTo(near.transform));
    }

    [Test]
    public void WallBlocksCatch()
    {
        Box(Vector3.forward * 1.5f, true);
        Box(Vector3.forward * 0.7f);
        Assert.That(Find(), Is.Null);
    }

    [Test]
    public void WallContainingSwingOriginBlocksCatch()
    {
        Box(Vector3.forward * 1.5f, true);
        Box(Vector3.zero);
        Assert.That(Find(), Is.Null);
    }

    [Test]
    public void OwnBodyAndPortalTriggersDoNotBlockCatch()
    {
        GameObject self = Box(Vector3.forward * 0.4f, true);
        Box(Vector3.forward * 0.7f, false, true);
        GameObject target = Box(Vector3.forward * 1.5f, true);
        Assert.That(Find(self.transform), Is.EqualTo(target.transform));
    }

    [Test]
    public void IneligibleTargetIsNotCaught()
    {
        Box(Vector3.forward); // Resolver rejects dead, escaped, downed and other-runner players.
        Assert.That(Find(), Is.Null);
    }

    [Test]
    public void TargetLeavingReachBeforeImpactIsMissed()
    {
        GameObject target = Box(Vector3.forward, true);
        Assert.That(Find(), Is.EqualTo(target.transform));
        target.transform.position = origin + Vector3.forward * 3f;
        Assert.That(Find(), Is.Null);
    }

    [Test]
    public void InvalidDirectionIsRejected()
    {
        Box(Vector3.forward, true);
        Assert.That(Find(direction: Vector3.zero), Is.Null);
        Assert.That(Find(direction: new Vector3(float.NaN, 0f, 1f)), Is.Null);
    }

    [Test]
    public void CrowdedOverlapDoesNotSilentlyDropTarget()
    {
        for (int i = 0; i < 40; i++) Box(Vector3.back * (0.5f + i * 0.02f));
        GameObject target = Box(Vector3.forward, true);
        Assert.That(Find(), Is.EqualTo(target.transform));
    }

    [Test]
    public void VisibleTargetCanBeCaughtWhenNearerTargetIsBehindAWall()
    {
        Box(Vector3.forward, true);
        Box(Vector3.forward * 0.5f);
        GameObject visible = Box(new Vector3(0.9f, 0f, 1.3f), true);
        Assert.That(Find(), Is.EqualTo(visible.transform));
    }

    [Test]
    public void ChildCollidersResolveToOnePlayerRoot()
    {
        var root = new GameObject("Player root");
        objects.Add(root);
        root.transform.position = origin + Vector3.forward;
        Box(Vector3.forward).transform.SetParent(root.transform, true);
        Box(new Vector3(0.3f, 0f, 1f)).transform.SetParent(root.transform, true);
        Physics.SyncTransforms();
        Assert.That(query.FindTarget(Physics.defaultPhysicsScene, origin, Vector3.forward,
            2f, 50f, null, collider => collider.transform.IsChildOf(root.transform) ? root.transform : null),
            Is.EqualTo(root.transform));
    }

    [Test]
    public void PlayerPrefabHasCatchSettingsAndEmptyPresentationHooks()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player.prefab");
        Assert.That(prefab, Is.Not.Null);
        PlayerGameState state = prefab.GetComponent<PlayerGameState>();
        Assert.That(state, Is.Not.Null);
        Assert.That(prefab.GetComponent<Un>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<PlayerCondition>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Collider>().isTrigger, Is.False);

        var settings = new SerializedObject(state);
        Assert.That(settings.FindProperty("catchRange").floatValue, Is.EqualTo(2f));
        Assert.That(settings.FindProperty("catchWindup").floatValue, Is.EqualTo(0.18f).Within(0.001f));
        Assert.That(settings.FindProperty("catchActiveDuration").floatValue, Is.GreaterThan(0f));
        foreach (string hook in new[] { "onCatchSwingStarted", "onCatchSwingEnded", "onCatchSucceeded", "onCatchMissed", "onCatchCancelled" })
        {
            Assert.That(settings.FindProperty(hook).FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize,
                Is.Zero, hook + " must remain unassigned until the art team supplies the motion.");
        }
    }
}
