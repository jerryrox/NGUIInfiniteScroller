using System;
using UnityEngine;

/// <summary>
/// Info which represents a single item in the InfiniteScroller module.
/// </summary>
public class ScrollerItem : MonoBehaviour {

	[HideInInspector]
	public float Position;
	[HideInInspector]
	public int Index;
	[HideInInspector]
	public GameObject Object;
	[HideInInspector]
	public Transform Transform;


	void Awake()
	{
		Object = gameObject;
		Transform = transform;
	}
}