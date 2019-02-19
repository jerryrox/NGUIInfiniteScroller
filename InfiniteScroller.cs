using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component which allows a normal scrollview to scroll infinitely using a few cells.
/// When you need to reset the scrollview's position, use this class's ResetPosition() method instead.
/// </summary>
public class InfiniteScroller : MonoBehaviour {

	/// <summary>
	/// Scrollview component which will have the infinite scrolling enabled.
	/// </summary>
	public UIScrollView ScrollView;

	/// <summary>
	/// Prefab of the cell which will be instantiated.
	/// </summary>
	public GameObject Prefab;

	/// <summary>
	/// Listener function called whenever there is an update on an item.
	/// </summary>
	public ItemUpdateHandler OnItemUpdate;

	/// <summary>
	/// Panel component attached with the scrollview.
	/// </summary>
	private UIPanel panel;

	/// <summary>
	/// Transform component of the panel for performance.
	/// </summary>
	private Transform panelTransform;

	/// <summary>
	/// Total number of items to be displayed throughout the scroll.
	/// </summary>
	private int totalSize;

	/// <summary>
	/// Index of the bound position.
	/// </summary>
	private int boundIndex;

	/// <summary>
	/// Size of the individual cell, with axis dependent on the scrollview's scrolling direction.
	/// </summary>
	[SerializeField]
	private float itemSize;

	/// <summary>
	/// X or Y position of the scrollview at the origin pivot when reset.
	/// </summary>
	private float originPosition;

	/// <summary>
	/// Last X or Y position of the panel.
	/// </summary>
	private float lastPosition;

	/// <summary>
	/// Prev and Next X or Y positions of the scrollview which triggers the item shifting.
	/// </summary>
	private float[] boundPositions;

	/// <summary>
	/// Whether the scrollview's content pivot indicates sorting items in negative axis.
	/// </summary>
	private bool isNegativeSortDir;

	/// <summary>
	/// List of items currently created and being managed.
	/// </summary>
	private List<ScrollerItem> items;


	/// <summary>
	/// Delegate used for receiving item update events to update the given child based on the index.
	/// </summary>
	public delegate void ItemUpdateHandler(ScrollerItem item);


	void Awake()
	{
		// Find scrollview if not assigned.
		if(ScrollView == null)
		{
			ScrollView = GetComponent<UIScrollView>();
			// If not attached on this object, we won't spend any more effort finding one.
			if(ScrollView == null)
			{
				Debug.LogError("InfiniteScroller - UIScrollView component not found! Please assign it in the " +
					"inspector or attach it on the object where this component exists.");
				Destroy(this);
				return;
			}
		}

		// Check unsupported movement type
		if(ScrollView.movement != UIScrollView.Movement.Horizontal &&
			ScrollView.movement != UIScrollView.Movement.Vertical)
		{
			Debug.LogError("InfiniteScroller - Only Horizontal or Vertical scrollview movements are allowed!");
			Destroy(this);
			return;
		}

		panel = ScrollView.GetComponent<UIPanel>();
		panelTransform = panel.transform;
		items = new List<ScrollerItem>();
		boundPositions = new float[2];

		// Pre-evaluate relatively heavy checks.
		isNegativeSortDir = IsNegativeSortDirection();
	}

	/// <summary>
	/// Initializes the scroller.
	/// </summary>
	public void Initialize(int totalSize, ItemUpdateHandler onUpdate = null)
	{
		OnItemUpdate = onUpdate;
		SetTotalSize(totalSize);
	}

	/// <summary>
	/// Sets the total number of items that will be displayed throughout scrolling.
	/// This will reset the scrollview's position.
	/// </summary>
	public void SetTotalSize(int totalSize)
	{
		this.totalSize = totalSize;

		// Rebuild items in case more cells are required.
		RebuildItems();
	}

	/// <summary>
	/// Creates new item cells as needed, based on the panel's current bounds.
	/// This will reset the scrollview's position.
	/// </summary>
	public void RebuildItems()
	{
		float panelSize = GetPanelSize();
		int itemCount = Mathf.CeilToInt(panelSize / itemSize) + 2;

		// Create missing items if needed.
		for(int i=items.Count; i<itemCount; i++)
		{
			ScrollerItem item = NGUITools.AddChild(gameObject, Prefab).GetComponent<ScrollerItem>();
			item.Initialize();
			items.Add(item);
		}

		// Toggle items on/off based on whether the item is pooled more than what we need to display.
		for(int i=0; i<items.Count; i++)
			items[i].Object.SetActive(i < totalSize);

		// Reset scrollview position
		ResetPosition();
	}

	/// <summary>
	/// Updates all visible items on the scrollview by invoking the item update event.
	/// </summary>
	public void UpdateItems()
	{
		for(int i=0; i<items.Count; i++)
			UpdateItem(items[i]);
	}

	/// <summary>
	/// Resets the scrollview's position to the initial point, based on its Content Origin value.
	/// </summary>
	[ContextMenu("Reset Position")]
	public void ResetPosition()
	{
		#if UNITY_EDITOR
		if(!Application.isPlaying)
			return;
		#endif

		// Determine whether items should be sorted in the direction of negative axis.
		float signDirection = isNegativeSortDir ? -1 : 1;

		// Reset all items' position and index.
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
		{
			for(int i=0; i<items.Count; i++)
			{
				items[i].Index = i;
				items[i].Transform.localPosition = new Vector3(itemSize * i * signDirection, 0f);
			}
		}
		else
		{
			for(int i=0; i<items.Count; i++)
			{
				items[i].Index = i;
				items[i].Transform.localPosition = new Vector3(0f, itemSize * i * signDirection);
			}
		}

		// Reset the scrollview's position.
		ScrollView.ResetPosition();

		// Store the origin point of the scrollview
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
			originPosition = panelTransform.localPosition.x;
		else
			originPosition = panelTransform.localPosition.y;

		// Last position should be set to origin position to prevent item index miscalculation.
		lastPosition = originPosition;

		// Reset bound positions to adapt to the new origin position.
		ResetBoundPositions();

		// Update all items
		UpdateItems();
	}

	/// <summary>
	/// Updates the specified item by invoking the item update event.
	/// </summary>
	void UpdateItem(ScrollerItem item)
	{
		// If index is out of bounds, we don't fire the event.
		if(item.Index < 0 || item.Index >= totalSize)
			return;
		if(OnItemUpdate != null)
			OnItemUpdate(item);
	}

	/// <summary>
	/// Increments bound positions by ItemSize step to the direction based on specified option.
	/// </summary>
	void IncrementBoundPositions(float signDirection, bool increaseBoundIndex)
	{
		boundPositions[0] += signDirection * itemSize;
		boundPositions[1] += signDirection * itemSize;
		boundIndex += increaseBoundIndex ? 1 : -1;
	}

	/// <summary>
	/// Resets bound positions array relative to the origin position.
	/// </summary>
	void ResetBoundPositions()
	{
		float signDirection = isNegativeSortDir ? 1f : -1f;
		boundPositions[0] = originPosition + signDirection * itemSize * 0.5f;
		boundPositions[1] = originPosition + signDirection * itemSize * 1.5f;
		boundIndex = 0;
	}

	/// <summary>
	/// Sets the first item in the items list to the last.
	/// </summary>
	void SetFirstItemToLast(float signDirection)
	{
		// Shift all items towards index 0.
		ScrollerItem firstItem = items[0];
		ScrollerItem lastItem = items[items.Count-1];
		for(int i=0; i<items.Count-1; i++)
			items[i] = items[i+1];
		// Move the first item to last.
		items[items.Count-1] = firstItem;

		// Make the first item's index come after the last
		firstItem.Index = lastItem.Index + 1;

		// Position the item
		Vector3 pos = lastItem.Transform.localPosition;
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
			pos.x += itemSize * signDirection;
		else
			pos.y += itemSize * signDirection;
		firstItem.Transform.localPosition = pos;

		// Trigger update on the first item.
		UpdateItem(firstItem);
	}

	/// <summary>
	/// Sets the last item in the list to the first.
	/// </summary>
	void SetLastItemToFirst(float signDirection)
	{
		// Shift all items towards last index.
		ScrollerItem lastItem = items[items.Count-1];
		ScrollerItem firstItem = items[0];
		for(int i=items.Count-1; i>0; i--)
			items[i] = items[i-1];
		// Move the last item to first.
		items[0] = lastItem;

		// Make the last item's index come before the first
		lastItem.Index = firstItem.Index - 1;

		// Position the item
		Vector3 pos = firstItem.Transform.localPosition;
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
			pos.x += itemSize * signDirection;
		else
			pos.y += itemSize * signDirection;
		lastItem.Transform.localPosition = pos;

		// Trigger update on the last item.
		UpdateItem(lastItem);
	}

	/// <summary>
	/// Returns the panel's size based on the scrollview's movement direction.
	/// </summary>
	float GetPanelSize()
	{
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
			return panel.width;
		else
			return panel.height;
	}

	/// <summary>
	/// Returns the item's local position x or y at specified index.
	/// </summary>
	float GetItemPosition(int index)
	{
		return itemSize * index;
	}

	/// <summary>
	/// Returns whether item cells should be sorted in the negative direction along the scrollview movement axis.
	/// </summary>
	bool IsNegativeSortDirection()
	{
		UIWidget.Pivot pivot = ScrollView.contentPivot;
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
		{
			return pivot == UIWidget.Pivot.BottomRight ||
				pivot == UIWidget.Pivot.Right ||
				pivot == UIWidget.Pivot.TopRight;
		}
		else
		{
			return pivot == UIWidget.Pivot.Top ||
				pivot == UIWidget.Pivot.TopLeft ||
				pivot == UIWidget.Pivot.TopRight;
		}
	}

	void Update()
	{
		int boundIndexLimit = totalSize - items.Count;

		// If total size fits within the item cells count, don't process anything.
		if(boundIndexLimit <= 0)
			return;

		// Do movement-specific preparations first
		float curPosition = 0f;
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
			curPosition = panelTransform.localPosition.x;
		else
			curPosition = panelTransform.localPosition.y;

		// If the scrollview is moving
		float deltaPosition = curPosition - lastPosition;
		if(deltaPosition != 0)
		{
			// Whether scrollview is moving towards the content sort direction.
			// If Vertical movement && Top origin && deltaPosition > 0, this should be false.
			bool isMovingToSortDir = (isNegativeSortDir && deltaPosition < 0) || (!isNegativeSortDir && deltaPosition > 0);

			// If moving in a way which triggers the next item's update
			if(!isMovingToSortDir)
			{
				// If we didn't reach the bound index high limit
				if(boundIndex < boundIndexLimit)
				{
					// If Top->Down or Right->Left sorting method
					if(isNegativeSortDir)
					{
						while(curPosition > boundPositions[1])
						{
							// Change bound positions to positive direction
							IncrementBoundPositions(1f, true);
							// Bring first item to last
							SetFirstItemToLast(-1f);

							// If reached the bound index high limit, break out
							if(boundIndex >= boundIndexLimit)
								break;
						}
					}
					// Else, Down->Top or Left->Right sorting method
					else
					{
						while(curPosition < boundPositions[1])
						{
							// Change bound positions to negative direction
							IncrementBoundPositions(-1f, true);
							// Bring first item to last
							SetFirstItemToLast(1f);

							// If reached the bound index high limit, break out
							if(boundIndex >= boundIndexLimit)
								break;
						}
					}

					// Invalidate the scrollview so we don't face any bound-related issues while dragging.
					ScrollView.InvalidateBounds();
				}
			}
			// Else, it triggers the previous item's update.
			else
			{
				// If we didn't reach the bound index low limit
				if(boundIndex > 0)
				{
					// If Top->Down or Right->Left sorting method
					if(isNegativeSortDir)
					{
						while(curPosition < boundPositions[0])
						{
							// Change bound positions to negative direction
							IncrementBoundPositions(-1f, false);
							// Bring last item to first
							SetLastItemToFirst(1f);

							// If reached the bound index low limit, break out
							if(boundIndex <= 0)
								break;
						}
					}
					// Else, Down->Top or Left->Right sorting method
					else
					{
						while(curPosition > boundPositions[0])
						{
							// Change bound positions to positive direction
							IncrementBoundPositions(1f, false);
							// Bring last item to first
							SetLastItemToFirst(-1f);

							// If reached the bound index low limit, break out
							if(boundIndex <= 0)
								break;
						}
					}

					// Invalidate the scrollview so we don't face any bound-related issues while dragging.
					ScrollView.InvalidateBounds();
				}
			}
		}

		// Store last position for later use.
		lastPosition = curPosition;
	}
}