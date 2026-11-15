using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Water : NetworkBehaviour
{

    public override void Spawned()
    {
        
    }

    public override void FixedUpdateNetwork()
    {
        transform.position += transform.forward * Runner.DeltaTime;
    }   
}
