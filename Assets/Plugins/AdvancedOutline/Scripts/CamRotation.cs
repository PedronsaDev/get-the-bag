using UnityEngine;

namespace ITISKIRUHERE
{
    public class CamRotation : MonoBehaviour
    {
        void Update() => transform.Rotate(0, .1f, 0);
    }
}
