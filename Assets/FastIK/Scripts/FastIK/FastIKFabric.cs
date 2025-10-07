#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace DitzelGames.FastIK
{
    /// <summary>Fabrik IK Solver</summary>
    public class FastIKFabric : MonoBehaviour
    {
        /// <summary>Chain length of bones</summary>
        public int ChainLength = 2;

        /// <summary>Target the chain should bend to</summary>
        public Transform Target;
        public Transform Pole;

        [Header("Solver Parameters")]
        public int Iterations = 10;      // solver iterations per update
        public float Delta = 0.001f;     // distance tolerance
        [Range(0, 1)] public float SnapBackStrength = 1f; // strength of returning toward start pose

        protected float[] BonesLength;       // distances between bones
        protected float CompleteLength;    // sum of bone lengths
        protected Transform[] Bones;
        protected Vector3[] Positions;
        protected Vector3[] StartDirectionSucc;
        protected Quaternion[] StartRotationBone;
        protected Quaternion StartRotationTarget;
        protected Transform Root;

        void Awake()
        {
            Init();
        }

        void Init()
        {
            ChainLength = Mathf.Max(1, ChainLength);

            Bones = new Transform[ChainLength + 1];
            Positions = new Vector3[ChainLength + 1];
            BonesLength = new float[ChainLength];
            StartDirectionSucc = new Vector3[ChainLength + 1];
            StartRotationBone = new Quaternion[ChainLength + 1];

            // find root
            Root = transform;
            for (int i = 0; i <= ChainLength; i++)
            {
                if (Root == null)
                    throw new UnityException("The chain value is longer than the ancestor chain!");
                Root = Root.parent;
            }

            // init target
            if (Target == null)
            {
                Target = new GameObject(gameObject.name + " Target").transform;
                SetPositionRootSpace(Target, GetPositionRootSpace(transform));
            }
            StartRotationTarget = GetRotationRootSpace(Target);

            // init bone data
            var current = transform;
            CompleteLength = 0f;
            for (int i = Bones.Length - 1; i >= 0; i--)
            {
                Bones[i] = current;
                StartRotationBone[i] = GetRotationRootSpace(current);

                if (i == Bones.Length - 1)
                {
                    // leaf
                    StartDirectionSucc[i] = GetPositionRootSpace(Target) - GetPositionRootSpace(current);
                }
                else
                {
                    // mid bone
                    StartDirectionSucc[i] = GetPositionRootSpace(Bones[i + 1]) - GetPositionRootSpace(current);
                    BonesLength[i] = StartDirectionSucc[i].magnitude;
                    CompleteLength += BonesLength[i];
                }

                current = current.parent;
            }
        }

        void LateUpdate()
        {
            ResolveIK();
        }

        void ResolveIK()
        {
            if (!Target) return;
            if (BonesLength == null || BonesLength.Length != ChainLength) Init();

            // get positions in root space
            for (int i = 0; i < Bones.Length; i++)
                Positions[i] = GetPositionRootSpace(Bones[i]);

            var targetPosition = GetPositionRootSpace(Target);
            var targetRotation = GetRotationRootSpace(Target);

            // reachability check
            if ((targetPosition - GetPositionRootSpace(Bones[0])).sqrMagnitude >= CompleteLength * CompleteLength)
            {
                // stretch toward target
                var direction = (targetPosition - Positions[0]).normalized;
                for (int i = 1; i < Positions.Length; i++)
                    Positions[i] = Positions[i - 1] + direction * BonesLength[i - 1];
            }
            else
            {
                // snap back toward start
                for (int i = 0; i < Positions.Length - 1; i++)
                    Positions[i + 1] = Vector3.Lerp(Positions[i + 1], Positions[i] + StartDirectionSucc[i], SnapBackStrength);

                // FABRIK iterations
                for (int iteration = 0; iteration < Iterations; iteration++)
                {
                    // back
                    for (int i = Positions.Length - 1; i > 0; i--)
                    {
                        if (i == Positions.Length - 1)
                            Positions[i] = targetPosition;
                        else
                            Positions[i] = Positions[i + 1] + (Positions[i] - Positions[i + 1]).normalized * BonesLength[i];
                    }

                    // forward
                    for (int i = 1; i < Positions.Length; i++)
                        Positions[i] = Positions[i - 1] + (Positions[i] - Positions[i - 1]).normalized * BonesLength[i - 1];

                    // close enough?
                    if ((Positions[Positions.Length - 1] - targetPosition).sqrMagnitude < Delta * Delta)
                        break;
                }
            }

            // pole constraint
            if (Pole)
            {
                var polePosition = GetPositionRootSpace(Pole);
                for (int i = 1; i < Positions.Length - 1; i++)
                {
                    var plane = new Plane(Positions[i + 1] - Positions[i - 1], Positions[i - 1]);
                    var projectedPole = plane.ClosestPointOnPlane(polePosition);
                    var projectedBone = plane.ClosestPointOnPlane(Positions[i]);
                    var angle = Vector3.SignedAngle(projectedBone - Positions[i - 1],
                                                    projectedPole - Positions[i - 1],
                                                    plane.normal);
                    Positions[i] = Quaternion.AngleAxis(angle, plane.normal) * (Positions[i] - Positions[i - 1]) + Positions[i - 1];
                }
            }

            // write back pose
            for (int i = 0; i < Positions.Length; i++)
            {
                if (i == Positions.Length - 1)
                    SetRotationRootSpace(Bones[i], Quaternion.Inverse(targetRotation) * StartRotationTarget * Quaternion.Inverse(StartRotationBone[i]));
                else
                    SetRotationRootSpace(Bones[i], Quaternion.FromToRotation(StartDirectionSucc[i], Positions[i + 1] - Positions[i]) * Quaternion.Inverse(StartRotationBone[i]));
                SetPositionRootSpace(Bones[i], Positions[i]);
            }
        }

        // --- Root-space helpers ---
        Vector3 GetPositionRootSpace(Transform current)
        {
            if (!Root) return current.position;
            return Quaternion.Inverse(Root.rotation) * (current.position - Root.position);
        }

        void SetPositionRootSpace(Transform current, Vector3 position)
        {
            if (!Root) current.position = position;
            else current.position = Root.rotation * position + Root.position;
        }

        Quaternion GetRotationRootSpace(Transform current)
        {
            // inverse(root) * current  => rotation from root to current
            if (!Root) return current.rotation;
            return Quaternion.Inverse(Root.rotation) * current.rotation; // fixed
        }

        void SetRotationRootSpace(Transform current, Quaternion rotation)
        {
            if (!Root) current.rotation = rotation;
            else current.rotation = Root.rotation * rotation;
        }

#if UNITY_EDITOR
        // Editor-only gizmos (wrapped fully to avoid build errors)
        void OnDrawGizmos()
        {
            var current = this.transform;
            for (int i = 0; i < ChainLength && current != null && current.parent != null; i++)
            {
                float len = Vector3.Distance(current.position, current.parent.position);
                float scale = len * 0.1f;

                Handles.matrix = Matrix4x4.TRS(
                    current.position,
                    Quaternion.FromToRotation(Vector3.up, current.parent.position - current.position),
                    new Vector3(scale, len, scale)
                );

                Handles.color = Color.green;
                Handles.DrawWireCube(Vector3.up * 0.5f, Vector3.one);

                current = current.parent;
            }
        }
#endif
    }
}
