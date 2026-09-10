using System;
using UnityEditor;
using UnityEngine;

namespace IV.BaseUnity
{
	public class MultiPivotWindow : EditorWindow
	{
		private float _x = .5f;
		private float _y = .5f;
		private float _z = .5f;

		private static MultiPivotWindow _window;

		private MeshFilter[] _filters;
		private Bounds[] _bounds;

		[MenuItem("Tools/Pivot Editor/Multi Pivot")]
		static void Init()
		{
			_window = GetWindow<MultiPivotWindow>();
			_window.titleContent = new GUIContent("Multi Pivot Editor");
			_window.Show();
		}

		private void OnGUI()
		{
			DrawBoundingBoxGUI();
		}

		void DrawBoundingBoxGUI()
		{
			EditorGUILayout.LabelField("Boundind Box", EditorStyles.toolbarButton);
			
			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("X", GUILayout.MaxWidth(20));

			float x = EditorGUILayout.Slider(_x, 0, 1);
			_x = x;

			if (GUILayout.Button(new GUIContent("Center", "Center pivot on X axis."), GUILayout.MinWidth(75)))
			{
				_x = .5f;
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Y", GUILayout.MaxWidth(20));
			float y = EditorGUILayout.Slider(_y, 0, 1);
			_y = y;

			if (GUILayout.Button(new GUIContent("Center", "Center pivot on Y axis."), GUILayout.MinWidth(75)))
			{
				_y = .5f;
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Z", GUILayout.MaxWidth(20));
			float z = EditorGUILayout.Slider(_z, 0, 1);
			_z = z;

			if (GUILayout.Button(new GUIContent("Center", "Center pivot on Z axis."), GUILayout.MinWidth(75)))
			{
				_z = .5f;
			}

			EditorGUILayout.EndHorizontal();

			if (GUILayout.Button(new GUIContent("Set Pivot", "Sets the pivot for the selected objects"), GUILayout.MinWidth(75)))
			{
				foreach (GameObject obj in Selection.gameObjects)
				{
					if (obj.IsAPrefab())
						continue;

					if (!obj.TryGetComponent(out MeshFilter filter) || !obj.TryGetComponent(out MeshRenderer renderer))
						continue;

					Bounds bounds = renderer.bounds;
					Vector3 min = bounds.min;
					Vector3 max = bounds.max;

					Vector3 pivot = Vector3.zero;
					pivot.x = Mathf.Lerp(min.x, max.x, _x);
					pivot.y = Mathf.Lerp( min.y, max.y, _y);
					pivot.z = Mathf.Lerp( min.z, max.z, _z);

					Transform target = obj.transform;
					target.Freeze(pivot, target.rotation, target.localScale, SetPivotWindow.SaveMeshToFbxFolder);
				}
			}
		}
	}
}