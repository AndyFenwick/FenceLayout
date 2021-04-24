using System.Collections.Generic;
using UnityEditor;
#if UNITY_2019_1_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(FenceLayout)), CanEditMultipleObjects]
public class FenceLayoutEditor : Editor
{
	private FenceLayout m_fenceLayout;
	private Terrain[] m_terrains;

	private FenceLayout.EditMode m_editMode = FenceLayout.EditMode.None;
	private Vector3 m_mousePos;

	// --------------------------------------------------------------------------

	protected virtual void OnSceneGUI()
	{
		EditorGUI.BeginChangeCheck();

		bool changed = false;

		for (int i = 0; i < m_fenceLayout.FencePoints.Count; i++)
		{
			Vector3 worldPos = m_fenceLayout.FencePoints[i] + m_fenceLayout.transform.position;

			Handles.color = Color.white;
			Vector3 labelPos = worldPos;
			labelPos.y -= 1.0f;
			Handles.Label(labelPos, "Point " + i);

			if (m_editMode != FenceLayout.EditMode.Delete)
			{
				Vector3 newPos = Handles.PositionHandle(worldPos, Quaternion.identity);

				if (newPos != worldPos)
				{
					newPos.y = SampleTerrainHeight(newPos);
					m_fenceLayout.FencePoints[i] = newPos - m_fenceLayout.transform.position;
				}
			}
		}

		if (m_fenceLayout.transform.hasChanged)
		{
			// Move all points onto the ground.
			for (int i = 0; i < m_fenceLayout.FencePoints.Count; i++)
			{
				Vector3 worldPos = m_fenceLayout.FencePoints[i] + m_fenceLayout.transform.position;
				worldPos.y = SampleTerrainHeight(worldPos);
				m_fenceLayout.FencePoints[i] = worldPos - m_fenceLayout.transform.position;
			}

			changed = true;
			m_fenceLayout.transform.hasChanged = false;
		}

		changed |= EditorGUI.EndChangeCheck();
		if (changed)
		{
			EditorUtility.SetDirty(m_fenceLayout);
			CreateFences();
		}
	}

	// --------------------------------------------------------------------------

	public override void OnInspectorGUI()
	{
		EditorGUI.BeginChangeCheck();

		GUIStyle italic = new GUIStyle(GUI.skin.label);
		italic.fontStyle = FontStyle.Italic;
		GUILayout.Label("Ctrl-Click to add points", italic);
		GUILayout.Label("Ctrl-Shift-Click to remove points", italic);

		GUILayout.BeginVertical();
		int numPrefabs = EditorGUILayout.IntField("Num fence prefabs", m_fenceLayout.FencePrefabs.Length);
		numPrefabs = Mathf.Clamp(numPrefabs, 0, 50);

		if (numPrefabs != m_fenceLayout.FencePrefabs.Length)
		{
			System.Array.Resize(ref m_fenceLayout.FencePrefabs, numPrefabs);
		}

		for (int i = 0; i < m_fenceLayout.FencePrefabs.Length; i++)
		{
			m_fenceLayout.FencePrefabs[i] = EditorGUILayout.ObjectField("Fence prefab " + i, m_fenceLayout.FencePrefabs[i], typeof(Transform), true) as Transform;
		}

		if (!m_fenceLayout.ObjectMode)
		{
			m_fenceLayout.PostPrefab = EditorGUILayout.ObjectField("End post prefab", m_fenceLayout.PostPrefab, typeof(Transform), true) as Transform;
		}
		GUILayout.EndVertical();

		if (!m_fenceLayout.ObjectMode)
		{
			m_fenceLayout.FenceEndOffset = EditorGUILayout.Vector3Field("End offset", m_fenceLayout.FenceEndOffset);
		}
		m_fenceLayout.FenceRotation = EditorGUILayout.FloatField("Fence rotation", m_fenceLayout.FenceRotation);
		m_fenceLayout.FenceLength = EditorGUILayout.FloatField("Fence length", m_fenceLayout.FenceLength);
		m_fenceLayout.FenceScale = EditorGUILayout.Vector3Field("Fence scale", m_fenceLayout.FenceScale);
		m_fenceLayout.CompleteLoop = EditorGUILayout.Toggle("Complete loop", m_fenceLayout.CompleteLoop);
		m_fenceLayout.ObjectMode = EditorGUILayout.Toggle("Object mode", m_fenceLayout.ObjectMode);

		if (m_fenceLayout.ObjectMode)
		{
			m_fenceLayout.PositionVariation = EditorGUILayout.Vector2Field("Position variation", m_fenceLayout.PositionVariation);
			m_fenceLayout.RotationVariation = EditorGUILayout.Vector2Field("Rotation variation", m_fenceLayout.RotationVariation);
			m_fenceLayout.ScaleVariation = EditorGUILayout.Vector2Field("Scale variation", m_fenceLayout.ScaleVariation);
		}
		else
		{
			m_fenceLayout.UseShear = EditorGUILayout.Toggle("Use shear", m_fenceLayout.UseShear);
		}

		GUILayout.BeginVertical();
		GUILayout.Label("Points");

		for (int i = 0; i < m_fenceLayout.FencePoints.Count; i++)
		{
			GUILayout.BeginHorizontal();
			Vector3 newPos = EditorGUILayout.Vector3Field("Point" + i, m_fenceLayout.FencePoints[i]);

			if (newPos != m_fenceLayout.FencePoints[i])
			{
				newPos.y = SampleTerrainHeight(newPos);
				m_fenceLayout.FencePoints[i] = newPos;
			}

			if (GUILayout.Button("X", GUILayout.Width(15)))
			{
				m_fenceLayout.FencePoints.RemoveAt(i);
				EditorUtility.SetDirty(m_fenceLayout);

			}

			GUILayout.EndHorizontal();
		}

		if (GUILayout.Button("Add point"))
		{
			Vector3 point;
			if (m_fenceLayout.FencePoints.Count > 1)
			{
				Vector3 dir = m_fenceLayout.FencePoints[m_fenceLayout.FencePoints.Count - 1] - m_fenceLayout.FencePoints[m_fenceLayout.FencePoints.Count - 2];
				point = m_fenceLayout.FencePoints[m_fenceLayout.FencePoints.Count - 1] + dir.normalized * 3.0f;
			}
			else if (m_fenceLayout.FencePoints.Count > 0)
			{
				point = m_fenceLayout.FencePoints[m_fenceLayout.FencePoints.Count - 1];
				point.x += 3.0f;
			}
			else
			{
				point = new Vector3();
			}

			m_fenceLayout.FencePoints.Add(point);
			EditorUtility.SetDirty(m_fenceLayout);
		}

		GUILayout.EndVertical();

		if (EditorGUI.EndChangeCheck())
		{
			// Force a save of the prefab.
			EditorUtility.SetDirty(m_fenceLayout);
			EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

			CreateFences();
		}
	}

	// --------------------------------------------------------------------------

	public void OnEnable()
	{
		m_fenceLayout = target as FenceLayout;
		m_terrains = FindObjectsOfType<Terrain>();

#if UNITY_2019_1_OR_NEWER
		SceneView.duringSceneGui += InputUpdate;
#else
		SceneView.onSceneGUIDelegate += InputUpdate;
#endif
	}

	// --------------------------------------------------------------------------

	public void OnDisable()
	{
#if UNITY_2019_1_OR_NEWER
		SceneView.duringSceneGui -= InputUpdate;
#else
		SceneView.onSceneGUIDelegate -= InputUpdate;
#endif
	}

	// --------------------------------------------------------------------------

	void InputUpdate(SceneView sceneview)
	{
		Event e = Event.current;
		int controlID = GUIUtility.GetControlID(GetHashCode(), FocusType.Passive);

		if (e.control)
		{
			HandleUtility.AddDefaultControl(controlID);
		}

		if (e.type != EventType.Repaint && e.type != EventType.Layout)
		{
			GameObject fenceObj = Selection.activeObject as GameObject;
			if (fenceObj)
			{
				FenceLayout fence = fenceObj.GetComponent<FenceLayout>();

				if (fence)
				{
					m_editMode = FenceLayout.EditMode.None;
					fence.SetEditMode(FenceLayout.EditMode.None, new Vector3());

					if (e.control)
					{
						Ray r = HandleUtility.GUIPointToWorldRay(e.mousePosition);

						RaycastHit result;
						if (Physics.Raycast(r, out result))
						{
							m_editMode = (e.control && e.shift) ? FenceLayout.EditMode.Delete : (e.control ? FenceLayout.EditMode.Add : FenceLayout.EditMode.None);
							m_mousePos = result.point;
							fence.SetEditMode(m_editMode, result.point);

							if (e.control && e.button == 0)
							{
								if (e.type == EventType.MouseDown)
								{
									ApplyEdit();
									EditorUtility.SetDirty(fence);
								}
							}
						}
					}
				}
			}
		}
	}

	// --------------------------------------------------------------------------

	public void ApplyEdit()
	{
		if (m_editMode == FenceLayout.EditMode.Add)
		{
			int idx = m_fenceLayout.GetAddPointIdx();
			Vector3 addPos = m_mousePos - m_fenceLayout.transform.position;

			if (idx >= m_fenceLayout.FencePoints.Count)
			{
				m_fenceLayout.FencePoints.Add(addPos);
			}
			else
			{
				m_fenceLayout.FencePoints.Insert(idx, addPos);
			}
		}
		else if (m_editMode == FenceLayout.EditMode.Delete)
		{
			int idx = m_fenceLayout.GetDeletePointIdx();
			m_fenceLayout.FencePoints.RemoveAt(idx);
		}

		CreateFences();
	}

	// --------------------------------------------------------------------------

	public void CreateFences()
	{
		// Recreate fences.
		Vector3 lastPos = new Vector3();
		bool gotLast = false;

		for (int i = m_fenceLayout.transform.childCount - 1; i >= 0; i--)
		{
			DestroyImmediate(m_fenceLayout.transform.GetChild(i).gameObject);
		}

		Quaternion rot = new Quaternion();
		float scale = 1.0f;

		if (m_fenceLayout.FencePrefabs.Length > 0 && m_fenceLayout.FencePrefabs[0] != null)
		{
			Random.InitState(123);

			int numLines = m_fenceLayout.FencePoints.Count + (m_fenceLayout.CompleteLoop ? 1 : 0);
			for (int p = 0; p < numLines; p++)
			{
				Vector3 point = m_fenceLayout.FencePoints[p % m_fenceLayout.FencePoints.Count] + m_fenceLayout.transform.position;

				if (gotLast)
				{
					// Fixed XZ length, and shear in Y.
					Vector3 toNext = point - lastPos;
					toNext.y = 0;

					float scaledLength = m_fenceLayout.FenceLength * m_fenceLayout.FenceScale.z;
					float numFences = toNext.magnitude / Mathf.Max(scaledLength, 0.1f);

					// Stretch to fit.
					int numFencesInt = (int)Mathf.Max(1, Mathf.Round(numFences));
					float fenceLength = toNext.magnitude / numFencesInt;
					scale = fenceLength / scaledLength;

					float angle = Mathf.Atan2(toNext.z, toNext.x);
					float rotDeg = (-angle * Mathf.Rad2Deg) + 90.0f;

					Vector3 step = toNext.normalized * fenceLength;
					float stepMag = step.magnitude;

					Vector3 endOffset = Vector3.Scale(m_fenceLayout.FenceEndOffset, m_fenceLayout.FenceScale) * scale;

					if (m_fenceLayout.ObjectMode)
					{
						// Draw the final 'fence post' on the last stretch if not a complete loop.
						int fenceCount = numFencesInt;
						if (!m_fenceLayout.CompleteLoop && p == (m_fenceLayout.FencePoints.Count - 1))
						{
							fenceCount++;
						}

						Quaternion offsetRot = Quaternion.Euler(0, rotDeg, 0);

						for (int i = 0; i < fenceCount; i++)
						{
							// Pos variance is local to the line direction.
							Vector3 offset = new Vector3(Random.Range(-m_fenceLayout.PositionVariation.x, m_fenceLayout.PositionVariation.x), 0.0f, Random.Range(-m_fenceLayout.PositionVariation.y, m_fenceLayout.PositionVariation.y));
							offset = offsetRot * offset;

							float rotVariance = Random.Range(m_fenceLayout.RotationVariation.x, m_fenceLayout.RotationVariation.y);
							rot = Quaternion.Euler(0, m_fenceLayout.FenceRotation + rotDeg + rotVariance, 0);
							float scaleVariance = Random.Range(m_fenceLayout.ScaleVariation.x, m_fenceLayout.ScaleVariation.y);

							Vector3 createPos = lastPos + offset + step * i;
							float y1 = SampleTerrainHeight(createPos);

							createPos.y = y1;
#if UNITY_2019_1_OR_NEWER
							Transform fence = PrefabUtility.InstantiatePrefab(GetRandomFencePrefab(), m_fenceLayout.transform) as Transform;
#else
							Transform fence = Object.Instantiate(GetRandomFencePrefab(), m_fenceLayout.transform) as Transform;
#endif
							if (fence)
							{
								fence.position = createPos;
								fence.localRotation = rot;
								fence.localScale = m_fenceLayout.FenceScale * scaleVariance;
							}
						}
					}
					else
					{
						rot = Quaternion.Euler(0, rotDeg, 0);

						for (int i = 0; i < numFencesInt; i++)
						{
							// Find both ends.
							Vector3 createPos = lastPos + step * i;
							float y1 = SampleTerrainHeight(createPos);
							float y2 = SampleTerrainHeight(createPos + step);

							if (m_fenceLayout.UseShear)
							{
								createPos.y = y1;

								float shearAngleRad = (Mathf.PI * 0.5f) - Mathf.Atan2(y2 - y1, stepMag);

								// Avoid divide by zero on ridiculous angles.
								if (Mathf.Abs(shearAngleRad) > 0.01f)
								{
									// Build the shear.
									// (a) Rotate by theta/2 counter - clockwise.
									// (b) Scale with x - scaling factor = sin(theta / 2) and y-scaling factor = cos(theta / 2).
									// (c) Rotate by 45 degree clockwise.
									// (d) Scale with x-scaling factor = sqrt(2) / sin(theta) , and y-scaling factor = sqrt(2)
									GameObject t1 = new GameObject("Shear1");
									GameObject t2 = new GameObject("Shear2");
									t1.transform.parent = m_fenceLayout.transform;
									t2.transform.parent = t1.transform;

									float shearAngleDeg = shearAngleRad * Mathf.Rad2Deg;
									t1.transform.localRotation = Quaternion.Euler(shearAngleDeg * 0.5f, rotDeg, 0);
									t1.transform.localScale = new Vector3(1.0f, Mathf.Cos(shearAngleRad * 0.5f), Mathf.Sin(shearAngleRad * 0.5f));
									t2.transform.localRotation = Quaternion.Euler(-45.0f, 0, 0);
									t2.transform.localScale = new Vector3(1.0f, Mathf.Sqrt(2.0f), Mathf.Sqrt(2.0f) / Mathf.Sin(shearAngleRad));
#if UNITY_2019_1_OR_NEWER
									Transform fence = PrefabUtility.InstantiatePrefab(GetRandomFencePrefab(), t2.transform) as Transform;
#else
									Transform fence = Object.Instantiate(GetRandomFencePrefab(), t2.transform) as Transform;
#endif
									if (fence)
									{
										fence.localScale = new Vector3(m_fenceLayout.FenceScale.x, m_fenceLayout.FenceScale.y, m_fenceLayout.FenceScale.z * scale);
										fence.localRotation = Quaternion.Euler(0.0f, m_fenceLayout.FenceRotation, 0.0f);
										fence.localPosition = endOffset;
										t2.transform.position = createPos;
									}
								}
							}
							else
							{
								// Use the lowest point so it's definitely in the floor.
								createPos.y = Mathf.Min(y1, y2);

								// Apply the offset.
								Vector3 offset = endOffset;
								offset += rot * offset;
								createPos += offset;

								Transform fence = PrefabUtility.InstantiatePrefab(GetRandomFencePrefab()) as Transform;
								if (fence)
								{
									fence.localPosition = createPos;
									fence.localRotation = rot;
									fence.localScale = new Vector3(m_fenceLayout.FenceScale.x, m_fenceLayout.FenceScale.y, m_fenceLayout.FenceScale.z * scale);
									fence.parent = m_fenceLayout.transform;
								}
							}
						}
					}
				}

				lastPos = point;
				gotLast = true;
			}
		}

		// Create the final post, following the rot/scale of the fence.
		if (!m_fenceLayout.CompleteLoop && !m_fenceLayout.ObjectMode && m_fenceLayout.PostPrefab)
		{
			if (m_fenceLayout.FencePoints.Count > 0)
			{
#if UNITY_2019_1_OR_NEWER
				Transform post = PrefabUtility.InstantiatePrefab(m_fenceLayout.PostPrefab, m_fenceLayout.transform) as Transform;
#else
				Transform post = Object.Instantiate(m_fenceLayout.PostPrefab, m_fenceLayout.transform) as Transform;
#endif
				if (post)
				{
					post.localPosition = m_fenceLayout.FencePoints[m_fenceLayout.FencePoints.Count - 1];
					post.localRotation = rot;
					post.localScale = m_fenceLayout.FenceScale * scale;
				}
			}
		}
	}

	// --------------------------------------------------------------------------

	private Transform GetRandomFencePrefab()
	{
		int idx = Random.Range(0, m_fenceLayout.FencePrefabs.Length);
		return m_fenceLayout.FencePrefabs[idx] != null ? m_fenceLayout.FencePrefabs[idx] : m_fenceLayout.FencePrefabs[0];
	}

	// --------------------------------------------------------------------------

	private float SampleTerrainHeight(Vector3 pos)
	{
		// Find which terrain we're in. Assumes that no terrains overlap in XZ.
		foreach (Terrain terrain in m_terrains)
		{
			Vector3 minPos = terrain.GetPosition();
			Vector3 maxPos = minPos + terrain.terrainData.size;

			if (pos.x >= minPos.x && pos.x < maxPos.x && pos.z >= minPos.z && pos.z < maxPos.z)
			{
				// Ensure terrain Y position is included.
				return terrain.SampleHeight(pos) + terrain.GetPosition().y;
			}
		}

		return 0.0f;
	}
}
