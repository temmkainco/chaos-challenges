using UnityEngine;
namespace ButtonPack
{

	public class HandleClick : MonoBehaviour
	{
		public PressController controller;
		public int handleIndex = 1;
		private void Awake()
		{
			controller = GetComponentInParent<PressController>();
		}
		void OnMouseDown()
		{
			if (!controller) return;
			if (handleIndex == 1) controller.Handle1();
			else controller.Handle2();
		}
	}
}