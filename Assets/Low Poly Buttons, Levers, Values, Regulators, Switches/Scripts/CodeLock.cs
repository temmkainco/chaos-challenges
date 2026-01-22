using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace ButtonPack
{
	public class CodeLock3D : MonoBehaviour
	{
		[Header("Code Settings")]
		[Tooltip("Correct 4-digit code")]
		public string correctCode = "1234";
		[Tooltip("Reference to the button that will be unlocked")]
		public PressController targetButton;
		public int maxAttempts = 3;
		private int _currentAttempts = 0;

		[Header("Lock Buttons")]
		public List<Transform> buttons;
		public TMP_Text enteredCodeText;

		[Header("Debug")]
		[SerializeField] private string enteredCode = "";

		public void PressButton(Transform button)
		{
			if (buttons == null || !buttons.Contains(button)) return;

			int index = buttons.IndexOf(button) + 1;
			targetButton.animator.SetTrigger(index.ToString());
			if (index >= 0 && index <= 9)
			{
				AddDigit(index.ToString());
			}
		}

		void AddDigit(string d)
		{
			if (enteredCode.Length >= 4) return;
			enteredCode += d;
			enteredCodeText.text = enteredCode;
			if (enteredCode.Length == 4)
			{
				CheckCode();
			}
		}

		IEnumerator ClearCode(string code, Color color, float time)
		{
			foreach (var button in buttons)
			{
				button.GetComponent<Collider>().enabled = false;
			}
			enteredCode = "";
			enteredCodeText.text = code;
			enteredCodeText.color = color;
			yield return new WaitForSeconds(time);
			foreach (var button in buttons)
			{
				button.GetComponent<Collider>().enabled = true;
			}
			enteredCodeText.text = "";
			enteredCodeText.color = Color.white;
		}

		void CheckCode()
		{
			if (enteredCode == correctCode)
			{
				Debug.Log("Code correct — unlocked");
				if (targetButton) targetButton.locked = false;
				StartCoroutine(ClearCode("Access", Color.green, 3));

			}
			else
			{
				Debug.Log("Wrong code");
				_currentAttempts++;
				if (_currentAttempts < maxAttempts)
				{
					StartCoroutine(ClearCode("XXXX", Color.red, 3));
				}
				else
				{
					StartCoroutine(ClearCode("BLOCKED", Color.red, 999));
				}
			}
		}
	}
}