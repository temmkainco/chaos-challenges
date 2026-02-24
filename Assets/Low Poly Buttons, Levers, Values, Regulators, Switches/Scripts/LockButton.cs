using UnityEngine;
namespace ButtonPack
{

	public class LockButton : MonoBehaviour
	{
		public CodeLock3D codeLock;

		void OnMouseDown()
		{
			if (codeLock)
			{
				codeLock.PressButton(transform);
			}
		}
	}
}