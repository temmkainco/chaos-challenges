using UnityEngine;
using UnityEngine.Events;
namespace ButtonPack
{

	[RequireComponent(typeof(Collider))]
	public class RotateController : MonoBehaviour
	{
		public enum Axis { X, Y, Z }

		[Header("Setup")]
		public Transform handle;
		public Axis rotationAxis = Axis.Z;
		public bool invertDirection = false;
		public float maxAngle = 360f;
		[Range(0f, 1f)] public float startNormalized = 0f;

		[Header("Interaction")]
		public bool locked = false;
		public float maxSpeed = 180f;

		[Header("Events")]
		public UnityEvent OnFullyOpened;
		public UnityEvent OnFullyClosed;
		[System.Serializable] public class FloatEvent : UnityEvent<float> { }
		public FloatEvent OnValueChanged;

		float currentAngle;
		float startAngle;
		Vector3 baseEuler;
		bool dragging;

		Camera cam;
		float pendingDelta;

		float EffectiveMaxAngle => Mathf.Max(0.0001f, maxAngle);

		void Awake()
		{
			if (!handle) handle = transform;
			baseEuler = handle.localEulerAngles;
			currentAngle = Mathf.Clamp01(startNormalized) * EffectiveMaxAngle;
			ApplyRotation();
			cam = Camera.main;
		}

		void OnMouseDown()
		{
			if (locked) return;
			dragging = true;
			startAngle = GetMouseAngle();
		}

		void OnMouseUp()
		{
			dragging = false;
		}

		void Update()
		{
			if (locked) return;

			if (dragging)
			{
				float newAngle = GetMouseAngle();
				float rawDelta = Mathf.DeltaAngle(startAngle, newAngle);
				startAngle = newAngle;

				float dir = invertDirection ? -1f : 1f;
				pendingDelta += rawDelta * dir;
			}

			if (Mathf.Abs(pendingDelta) > 0.001f)
			{
				float maxStep = maxSpeed * Time.deltaTime;
				float step = Mathf.Clamp(pendingDelta, -maxStep, maxStep);

				pendingDelta -= step;
				currentAngle = Mathf.Clamp(currentAngle + step, 0f, maxAngle);

				ApplyRotation();
				InvokeEvents();
			}
		}

		float GetMouseAngle()
		{
			if (!cam) cam = Camera.main;

			Vector3 axis = rotationAxis == Axis.X ? handle.right :
						   rotationAxis == Axis.Y ? handle.up :
						   handle.forward;

			Plane plane = new Plane(axis, handle.position);
			Ray ray = cam.ScreenPointToRay(Input.mousePosition);

			if (plane.Raycast(ray, out float enter))
			{
				Vector3 hit = ray.GetPoint(enter);
				Vector3 dir = (hit - handle.position).normalized;

				Vector3 localRight = Vector3.Cross(axis, Vector3.up);
				if (localRight.sqrMagnitude < 0.001f)
					localRight = Vector3.Cross(axis, Vector3.forward);

				localRight.Normalize();
				Vector3 localUp = Vector3.Cross(axis, localRight);

				float angle = Mathf.Atan2(Vector3.Dot(dir, localUp), Vector3.Dot(dir, localRight)) * Mathf.Rad2Deg;
				return angle;
			}

			return 0f;
		}

		void ApplyRotation()
		{
			Vector3 e = baseEuler;
			float angle = currentAngle;

			switch (rotationAxis)
			{
				case Axis.X: e.x = baseEuler.x + angle; break;
				case Axis.Y: e.y = baseEuler.y + angle; break;
				case Axis.Z: e.z = baseEuler.z + angle; break;
			}
			handle.localEulerAngles = e;
		}

		void InvokeEvents()
		{
			float t = Mathf.Clamp01(currentAngle / EffectiveMaxAngle);
			OnValueChanged?.Invoke(t);

			if (Mathf.Approximately(currentAngle, 0f)) OnFullyClosed?.Invoke();
			if (Mathf.Approximately(currentAngle, maxAngle)) OnFullyOpened?.Invoke();
		}
	}
}