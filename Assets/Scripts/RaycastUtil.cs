using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Unity.Burst;

public class RaycastUtil : MonoBehaviour
{
    // From: http://blog.lidia-martinez.com/use-the-results-of-raycastcommand-schedule-on-a-job
    // by Lidia Martinez

    [StructLayout(LayoutKind.Sequential)]
    internal struct RaycastHitPublic
    {
        public Vector3 m_Point;
        public Vector3 m_Normal;
        public int m_FaceID;
        public float m_Distance;
        public Vector2 m_UV;
        public int m_ColliderID;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetColliderID(RaycastHit hit)
    {
        unsafe
        {
            RaycastHitPublic h = *(RaycastHitPublic*)&hit;
            return h.m_ColliderID;
        }
    }
}
