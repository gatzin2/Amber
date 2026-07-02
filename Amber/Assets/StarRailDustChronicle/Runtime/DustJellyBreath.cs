using UnityEngine;

namespace Amber.StarRailDustChronicle
{
    public sealed class DustJellyBreath : MonoBehaviour
    {
        [Header("Breath")]
        [SerializeField] private float breathSpeed = 1.8f;
        [SerializeField] private float breathAmplitude = 0.055f;

        [Header("Jelly Pulse")]
        [SerializeField] private float pulseFrequency = 34f;
        [SerializeField] private float pulseDecay = 4.8f;
        [SerializeField] private float pulseWobbleAngle = 8f;

        private Vector3 baseScale = Vector3.one;
        private Quaternion baseRotation = Quaternion.identity;
        private float pulseStrength;
        private float pulseAge;
        private Vector3 pulseDirectionLocal = Vector3.forward;
        private float seed;

        private void Awake()
        {
            baseScale = transform.localScale;
            baseRotation = transform.localRotation;
            seed = Random.value * 10f;
        }

        public void SetBaseScale(Vector3 scale)
        {
            baseScale = new Vector3(
                Mathf.Max(0.05f, scale.x),
                Mathf.Max(0.05f, scale.y),
                Mathf.Max(0.05f, scale.z));
        }

        public void ResetVisual()
        {
            baseRotation = Quaternion.identity;
            pulseStrength = 0f;
            pulseAge = 0f;
            pulseDirectionLocal = Vector3.forward;
            transform.localScale = baseScale;
            transform.localRotation = baseRotation;
        }

        public void Pulse(float strength)
        {
            Pulse(strength, Vector3.zero);
        }

        public void Pulse(float strength, Vector3 localDirection)
        {
            pulseStrength = Mathf.Max(pulseStrength, Mathf.Max(0f, strength));
            pulseAge = 0f;

            localDirection.y = 0f;
            if (localDirection.sqrMagnitude > 0.0001f)
            {
                pulseDirectionLocal = localDirection.normalized;
            }
        }

        private void Update()
        {
            var breath = Mathf.Sin((Time.time + seed) * breathSpeed) * breathAmplitude;
            var squash = 0f;
            var stretch = 0f;
            var signedPulse = 0f;
            if (pulseStrength > 0f)
            {
                pulseAge += Time.deltaTime;
                var envelope = Mathf.Exp(-pulseAge * pulseDecay);
                signedPulse = Mathf.Sin(pulseAge * pulseFrequency) * pulseStrength * envelope;
                squash = Mathf.Max(0f, signedPulse);
                stretch = Mathf.Max(0f, -signedPulse);

                if (pulseStrength * envelope < 0.01f)
                {
                    pulseStrength = 0f;
                    pulseAge = 0f;
                }
            }

            var directionalAmount = squash * 0.35f + stretch * 0.75f;
            var xDirection = Mathf.Abs(pulseDirectionLocal.x);
            var zDirection = Mathf.Abs(pulseDirectionLocal.z);
            var horizontalBase = 1f + breath * 0.45f + squash * 0.95f - stretch * 0.18f;
            var xScale = horizontalBase + directionalAmount * xDirection;
            var zScale = horizontalBase + directionalAmount * zDirection;
            var vertical = 1f - breath * 0.35f - squash * 0.9f + stretch * 0.48f;
            var wobble = signedPulse * pulseWobbleAngle;

            transform.localScale = new Vector3(
                baseScale.x * Mathf.Max(0.62f, xScale),
                baseScale.y * Mathf.Max(0.52f, vertical),
                baseScale.z * Mathf.Max(0.62f, zScale));
            transform.localRotation = baseRotation * Quaternion.Euler(
                pulseDirectionLocal.z * wobble,
                0f,
                -pulseDirectionLocal.x * wobble);
        }
    }
}
