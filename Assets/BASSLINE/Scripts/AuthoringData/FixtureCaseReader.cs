using UnityEngine;
namespace BASSLINE.AuthoringData
{
 // Authored sensor / physical trace binding. No knowledge is stored in presentation.
 public sealed class FixtureCaseReader:MonoBehaviour
 {
  public string StableId;public string Kind;public Transform Sensor;
  public float Range=10,FieldOfView=95;
 }
}
