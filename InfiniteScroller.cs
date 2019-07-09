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
	/// Panel position springer to support focusing on a child object.
	/// </summary>
	public SpringPanel Springer;

	/// <summary>
	/// Prefab of the cell which will be instantiated.
	/// </summary>
	public GameObject Prefab;

	/// <summary>
	/// The scrollbar which will represent this object's scrolling progress.
	/// </summary>
	public InfiniteScrollbar Scrollbar;

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
	/// The number of cells that will be created on each row/column based on scrollview settings.
	/// </summary>
	[SerializeField]
	private int groupSize = 1;

	/// <summary>
	/// Size of the area which an individual cell would occupy.
	/// </summary>
	[SerializeField]
	private Vector2 itemSize;

	/// <summary>
	/// Item's size in terms of the movement direction.
	/// </summary>
	private float itemSizeToMoveDir;

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
	/// Sort offset which modifies the cells' position on the adjacent axis.
	/// </summary>
	private float groupSortOffset;

	/// <summary>
	/// Whether the scrollview's content pivot indicates sorting items in negative axis.
	/// </summary>
	private bool isNegativeSortDir;

	/// <summary>
	/// List of items currently created and being managed.
	/// </summary>
	private List<ScrollerItem> items;

	/// <summary>
	/// Temporary list of items used for moving items during scroll.
	/// </summary>
	private List<ScrollerItem> firstItems;

	/// <summary>
	/// Temporary list of items used for moving items during scroll.
	/// </summary>
	private List<ScrollerItem> lastItems;


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

		if(groupSize < 1)
		{
			Debug.Log("InfiniteScroller - groupSize must be equal to or greater than 1. Force-assigning groupSize to 1.");
			groupSize = 1;
		}

		panel = ScrollView.GetComponent<UIPanel>();
		panelTransform = panel.transform;
		items = new List<ScrollerItem>();
		firstItems = new List<ScrollerItem>();
		lastItems = new List<ScrollerItem>();
		boundPositions = new float[2];
	}

	/// <summary>
	/// Initializes the scroller.
	/// </summary>
	public void Initialize(int totalSize, ItemUpdateHandler onUpdate = null)
	{
		// Pre-evaluate relatively heavy checks.
		isNegativeSortDir = IsNegativeSortDirection();
		groupSortOffset = GetGroupSortOffset();
		itemSizeToMoveDir = ScrollView.movement == UIScrollView.Movement.Horizontal ? itemSize.x : itemSize.y;

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
	/// Sets the number of cells to display on a single row/column based on current scrollview settings.
	/// </summary>
	public void SetGroupSize(int groupSize)
	{
		if(groupSize < 1)
		{
			Debug.LogWarning("InfiniteScroller.SetGroupSize - groupSize must be equal to or greater than 1!");
			return;
		}
		this.groupSize = groupSize;

		// Adjust cell count for new group size
		RebuildItems();
	}

	/// <summary>
	/// Creates new item cells as needed, based on the panel's current bounds.
	/// This will reset the scrollview's position.
	/// </summary>
	public void RebuildItems()
	{
		float size = ScrollView.movement == UIScrollView.Movement.Horizontal ? itemSize.x : itemSize.y;
		float panelSize = GetPanelSize();
		int itemCount = (Mathf.CeilToInt(panelSize / size) + 2) * groupSize;

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

		// Calculate variables for cell grouping.
		int displayedGroups = (items.Count-1) / groupSize + 1;
		float groupItemDirection = groupSortOffset == 1f ? -1f : 1f;

		// Reset all items' position and index.
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
		{
			float groupItemFirstPos = (groupSize-1) * itemSize.y / 2f;

			// If pivot at the bottom, reverse first pos
			if(groupSortOffset == 1f)
				groupItemFirstPos = -groupItemFirstPos;
			
			for(int g=0; g<displayedGroups; g++)
			{
				for(int i=0; i<groupSize; i++)
				{
					// The number of cells will always be equal to or greater than totalGroup * groupSize.
					int index = g * groupSize + i;
					items[index].Index = index;
					items[index].Transform.localPosition = new Vector3(
						itemSize.x * g * signDirection,
						itemSize.y * i * groupItemDirection + groupItemFirstPos
					);
				}
			}
		}
		else
		{
			float groupItemFirstPos = (groupSize-1) * itemSize.x / -2f;

			// If pivot at the right, reverse first pos
			if(groupSortOffset == 1f)
				groupItemFirstPos = -groupItemFirstPos;

			for(int g=0; g<displayedGroups; g++)
			{
				for(int i=0; i<groupSize; i++)
				{
					// The number of cells will always be equal to or greater than totalGroup * groupSize.
					int index = g * groupSize + i;
					items[index].Index = index;
					items[index].Transform.localPosition = new Vector3(
						itemSize.x * i * groupItemDirection + groupItemFirstPos,
						itemSize.y * g * signDirection
					);
				}
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
	/// Focuses the scrollview position on the child of specified index.
	/// </summary>
	public void FocusOnChild(int index)
	{
		// There must be a valid spring panel component.
		if(Springer == null)
			return;

		Springer.enabled = false;
		
		// Make sure it's within bounds.
		index = Mathf.Clamp(index / groupSize, 0, (totalSize-1) / groupSize);

		float panelSize = GetPanelSize();
		float sign = isNegativeSortDir ? -1f : 1f;
		float cellSize = 0;
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
			cellSize = itemSize.x;
		else
			cellSize = itemSize.y;
		
		float originBound = originPosition;
		float lastBound = originBound + (panelSize - totalSize * cellSize) * sign;
		float targetPos = index * cellSize * -sign + originBound + (panelSize * 0.5f * sign) - (cellSize * 0.5f * sign);
		if(isNegativeSortDir)
			targetPos = Mathf.Clamp(targetPos, originBound, lastBound);
		else
			targetPos = Mathf.Clamp(targetPos, lastBound, originBound);
		
		Springer.target = panelTransform.localPosition;
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
			Springer.target.x = targetPos;
		else
			Springer.target.y = targetPos;
		
		Springer.enabled = true;
	}

	/// <summary>
	/// Updates the specified item by invoking the item update event.
	/// </summary>
	void UpdateItem(ScrollerItem item)
	{
		// If index is out of bounds, we don't fire the event.
		if(item.Index < 0 || item.Index >= totalSize)
		{
			item.Object.SetActive(false);
			return;
		}

		item.Object.SetActive(true);
		if(OnItemUpdate != null)
			OnItemUpdate(item);
	}

	/// <summary>
	/// Increments bound positions by ItemSize step to the direction based on specified option.
	/// </summary>
	void IncrementBoundPositions(float signDirection, bool increaseBoundIndex)
	{
		boundPositions[0] += signDirection * itemSizeToMoveDir;
		boundPositions[1] += signDirection * itemSizeToMoveDir;
		boundIndex += increaseBoundIndex ? 1 : -1;
	}

	/// <summary>
	/// Resets bound positions array relative to the origin position.
	/// </summary>
	void ResetBoundPositions()
	{
		float size = ScrollView.movement == UIScrollView.Movement.Horizontal ? itemSize.x : itemSize.y;
		float signDirection = isNegativeSortDir ? 1f : -1f;
		boundPositions[0] = originPosition + signDirection * size * 0.5f;
		boundPositions[1] = originPosition + signDirection * size * 1.5f;
		boundIndex = 0;
	}

	/// <summary>
	/// Sets the first item in the items list to the last.
	/// </summary>
	void SetFirstItemToLast(float signDirection)
	{
		// Store items at the first and the last group.
		for(int g=0; g<groupSize; g++)
		{
			firstItems.Add(items[g]);
			lastItems.Add(items[items.Count-groupSize+g]);
		}
		// Move items toward first index.
		for(int i=0; i<items.Count-groupSize; i++)
			items[i] = items[i+groupSize];
		
		for(int g=0; g<groupSize; g++)
		{
			var first = firstItems[g];
			var last = lastItems[g];

			// Move the first item to last.
			items[items.Count-groupSize+g] = first;

			// Make the first item's index come after the last
			first.Index = last.Index + groupSize;

			// Position the item
			Vector3 pos = last.Transform.localPosition;
			if(ScrollView.movement == UIScrollView.Movement.Horizontal)
				pos.x += itemSize.x * signDirection;
			else
				pos.y += itemSize.y * signDirection;
			first.Transform.localPosition = pos;

			// Trigger update on the first item.
			UpdateItem(first);
		}

		firstItems.Clear();
		lastItems.Clear();
	}

	/// <summary>
	/// Sets the last item in the list to the first.
	/// </summary>
	void SetLastItemToFirst(float signDirection)
	{
		// Store items at the first and the last group.
		for(int g=0; g<groupSize; g++)
		{
			firstItems.Add(items[g]);
			lastItems.Add(items[items.Count-groupSize+g]);
		}
		// Move items toward last index.
		for(int i=items.Count-1; i>groupSize-1; i--)
			items[i] = items[i-groupSize];

		for(int g=0; g<groupSize; g++)
		{
			var first = firstItems[g];
			var last = lastItems[g];

			// Move the last item to first.
			items[g] = last;

			// Make the last item's index come before the first
			last.Index = first.Index - groupSize;

			// Position the item
			Vector3 pos = first.Transform.localPosition;
			if(ScrollView.movement == UIScrollView.Movement.Horizontal)
				pos.x += itemSize.x * signDirection;
			else
				pos.y += itemSize.y * signDirection;
			last.Transform.localPosition = pos;

			// Trigger update on the last item.
			UpdateItem(last);
		}

		firstItems.Clear();
		lastItems.Clear();
	}

	/// <summary>
	/// Updates the scrollbar's display.
	/// </summary>
	void UpdateScrollbar()
	{
		if(Scrollbar == null)
			return;
		
		float fullSize = (totalSize-1) / groupSize + 1;
		fullSize *= itemSizeToMoveDir;

		float max = originPosition;
		float panelSize = GetPanelSize();
		if(panelSize < fullSize)
			max += (fullSize - panelSize) * (isNegativeSortDir ? 1f : -1f);

		float cur = (
			ScrollView.movement == UIScrollView.Movement.Vertical ?
			panelTransform.localPosition.y :
			panelTransform.localPosition.x
		);
		
		Scrollbar.Draw(
			ScrollView.movement == UIScrollView.Movement.Vertical,
			originPosition,
			max,
			cur,
			panelSize
		);
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

	/// <summary>
	/// Returns the offset which moves the cells by a certain factor of the cells' total size in a group.
	/// </summary>
	float GetGroupSortOffset()
	{
		UIWidget.Pivot pivot = ScrollView.contentPivot;
		if(ScrollView.movement == UIScrollView.Movement.Horizontal)
		{
			switch(pivot)
			{
			case UIWidget.Pivot.Top:
			case UIWidget.Pivot.TopLeft:
			case UIWidget.Pivot.TopRight:
				return 0f;
			case UIWidget.Pivot.Center:
			case UIWidget.Pivot.Left:
			case UIWidget.Pivot.Right:
				return 0.5f;
			default:
				return 1f;
			}
		}
		else
		{
			switch(pivot)
			{
			case UIWidget.Pivot.TopLeft:
			case UIWidget.Pivot.Left:
			case UIWidget.Pivot.BottomLeft:
				return 0f;
			case UIWidget.Pivot.Top:
			case UIWidget.Pivot.Center:
			case UIWidget.Pivot.Bottom:
				return 0.5f;
			default:
				return 1f;
			}
		}
	}

	void Update()
	{
		int boundIndexLimit = totalSize - items.Count;

		// Cell repositioning should only occur when there are more number of items to display than the allocated cells.
		if(boundIndexLimit > 0)
		{
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

		// Intercept scrollbar update
		UpdateScrollbar();
	}
}