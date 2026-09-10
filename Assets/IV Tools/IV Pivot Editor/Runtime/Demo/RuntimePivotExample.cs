using UnityEngine;

namespace IV.PivotEditor
{
	public class RuntimePivotExample : MonoBehaviour
	{
		[SerializeField] private Transform _pivot;

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Space))
			{
				RuntimePivot.SetPivot(gameObject, _pivot.position, _pivot.rotation);
			}
		}
	}
}