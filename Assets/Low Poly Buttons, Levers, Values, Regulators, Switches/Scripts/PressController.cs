using System;
using UnityEngine;
using UnityEngine.Events;
namespace ButtonPack
{

	[RequireComponent(typeof(Animator))]
	public class PressController : MonoBehaviour
	{
		public enum Mode { Toggle, Momentary }

		public MeshRenderer buttonMeshRenderer;

        [Header("Materials")]
		public Material unpressedMaterial;
		public Material pressedMaterial;

        [Header("Setup")]
		public bool locked = false;
		public bool twoWayExclusive = true;
		public Animator animator;
		public Mode mode = Mode.Toggle;

		[Header("Animator Params")]
		public string boolNameB1 = "isPressB1";
		public string boolNameB2 = "isPressB2";
		public string triggerNameB1 = "PressB1";
		public string triggerNameB2 = "PressB2";

		public event Action OnPress1;
		public event Action OnRelease1;
		public event Action OnPress2;
		public event Action OnRelease2;

		bool s1;
		bool s2;

		void Reset()
		{
			animator = GetComponent<Animator>();
		}

        private void Start()
        {
            if (buttonMeshRenderer && unpressedMaterial)
			{
				buttonMeshRenderer.material = unpressedMaterial;
				OnPress1 += () => ChangeButtonMaterial(pressedMaterial);
				OnRelease1 += () => ChangeButtonMaterial(unpressedMaterial);
            }

        }

        public void Handle1()
		{
			if (mode == Mode.Momentary) Press1Momentary();
			else Toggle1();
		}

		public void Handle2()
		{
			if (mode == Mode.Momentary) Press2Momentary();
			else Toggle2();
		}

		public void Toggle1()
		{
			if (locked) return;
			s1 = !s1;
			if (animator && !string.IsNullOrEmpty(boolNameB1))
				animator.SetBool(boolNameB1, s1);

			if (twoWayExclusive && s1 && s2)
			{
				s2 = false;
				if (animator && !string.IsNullOrEmpty(boolNameB2))
					animator.SetBool(boolNameB2, false);
				OnRelease2?.Invoke();
			}

			if (s1) OnPress1?.Invoke(); else OnRelease1?.Invoke();
		}

		public void Toggle2()
		{
			if (locked) return;
			s2 = !s2;
			if (animator && !string.IsNullOrEmpty(boolNameB2))
				animator.SetBool(boolNameB2, s2);

			if (twoWayExclusive && s2 && s1)
			{
				s1 = false;
				if (animator && !string.IsNullOrEmpty(boolNameB1))
					animator.SetBool(boolNameB1, false);
				OnRelease1?.Invoke();
			}

			if (s2) OnPress2?.Invoke(); else OnRelease2?.Invoke();
		}

		// --- Momentary mode (trigger) ---
		public void Press1Momentary()
		{
			if (locked) return;
			if (animator && !string.IsNullOrEmpty(triggerNameB1))
				animator.SetTrigger(triggerNameB1);
			OnPress1?.Invoke();
			OnRelease1?.Invoke();
		}

		public void Press2Momentary()
		{
			if (locked) return;
			if (animator && !string.IsNullOrEmpty(triggerNameB2))
				animator.SetTrigger(triggerNameB2);
			OnPress2?.Invoke();
			OnRelease2?.Invoke();
		}

		public void ChangeButtonMaterial(Material material)
		{
			buttonMeshRenderer.material = material;
        }
	}
}