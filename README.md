# NGUIInfiniteScroller

InfiniteScroller is a component which allows standard UIScrollViews to support infinite scrolling.  
By default, NGUI also ships their "infinite scrolling" feature using UIWrapContent but it does not work properly and is full of bugs. (From the point there are bugs in the example scene, I'd personally consider it useless.)  
  
### How to use
1. Attach the InfiniteScroller component on the scrollview object you wish to support infinite scrolling.
2. Attach the ScrollerItem component on the prefab you wish to scroll around.
3. Setup the references and Item Size value on the InfiniteScroller instance.
4. Make reference to the InfiniteScroller instance from the component you wish to control from.
5. Call Initialize(X, Y) with X being the total number of data you want to display (e.g. myTestData.Count) and Y being the callback function for initializing your cells.
  
Further guides will be posted on my blog in the near future.  
  
### Features
1. Infinite scrolling (obviously)
2. Simple, quick setup
3. Preserves almost all features of a standard UIScrollView.
  
### Limitations
1. Currently, it only supports one row/column of cells, depending on the scrollview's movement, but this will be fixed in future updates.
2. Only Horizontal and Vertical movements are supported on the scrollview.
3. Scrollbars will not be displayed properly, but this is also planned for fix.
4. Does not support "Center" value of UIScrollView's Content Origin. I might support it when I need it myself.
5. When you need to call ResetPosition() on the UIScrollView, you should use the InfiniteScroller's ResetPosition() instead, just to make sure it doesn't break.
6. Only supports fixed item size. This means all cells will be positioned at a fixed interval; pretty much the same as UIGrid.
