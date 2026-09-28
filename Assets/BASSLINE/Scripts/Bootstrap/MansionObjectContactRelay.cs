using System;
using UnityEngine;
namespace BASSLINE.Bootstrap
{
    public sealed class MansionObjectContactRelay:MonoBehaviour
    {
        public Action<Collision,bool> Receive;
        void OnCollisionEnter(Collision collision)=>Receive?.Invoke(collision,true);
        void OnCollisionExit(Collision collision)=>Receive?.Invoke(collision,false);
    }
}
