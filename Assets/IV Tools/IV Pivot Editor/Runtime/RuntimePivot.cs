using UnityEngine;

namespace IV.PivotEditor
{
	public static class RuntimePivot
	{
		public static void SetPivot(GameObject target, Vector3 position, Quaternion rotation)
		{
			Transform transform = target.transform;
			Transform[] children = new Transform[transform.childCount];

			for (var i = 0; i < children.Length; i++)
			{
				children[i] = transform.GetChild(0);
				children[i].parent = null;
			}
			
			var filter = target.GetComponent<MeshFilter>();
			Mesh mesh = filter != null ? filter.mesh : null;
			
			bool hasMesh = mesh != null;
			Vector3[] verts = hasMesh ? mesh.vertices : null;
			Vector3[] norms = hasMesh ? mesh.normals : null;

			if (hasMesh)
			{
				for (var i = 0; i < verts.Length; i++)
				{
					verts[i] = transform.TransformPoint(verts[i]);
					norms[i] = transform.TransformDirection(norms[i]);
				}
			}
			
			transform.position = position;
			transform.rotation = rotation;

			if (hasMesh)
			{
				for (var i = 0; i < verts.Length; i++)
				{
					verts[i] = transform.InverseTransformPoint(verts[i]);
					norms[i] = transform.InverseTransformDirection(norms[i]);
				}

				mesh.vertices = verts;
				mesh.normals = norms;
				mesh.RecalculateTangents();
				mesh.RecalculateBounds();
			}

			foreach (Transform child in children)
			{
				child.parent = transform;
			}
		}
	}
}