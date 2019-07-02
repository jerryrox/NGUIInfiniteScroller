using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Displays the related InfiniteScroller component's scrolling state.
/// </summary>
public class InfiniteScrollbar : MonoBehaviour {

	public UISprite Background;
	public UISprite Foreground;

	private Vector4 drawRegion = new Vector4();


	/// <summary>
	/// Draws the scrollbar using specified information.
	/// </summary>
	public void Draw(bool isVertical, float min, float max, float cur, float size)
	{
		if(Foreground == null)
			return;
		
		float totalSize = Mathf.Abs(min - max) + size;
		float spriteSizeRate = size / totalSize;
		float fromStart = 1f - Mathf.InverseLerp(min, max, cur) * (1f - spriteSizeRate);
		float fromEnd = Mathf.InverseLerp(max, min, cur) * (1f - spriteSizeRate);

		if(min > max)
		{
			float temp = 1f - fromStart;
			fromStart = 1f - fromEnd;
			fromEnd = temp;
		}

		if(isVertical)
		{
			drawRegion.x = 0f;
			drawRegion.y = fromEnd;
			drawRegion.z = 1f;
			drawRegion.w = fromStart;
		}
		else
		{
			drawRegion.x = fromEnd;
			drawRegion.y = 0f;
			drawRegion.z = fromStart;
			drawRegion.w = 1f;
		}

		Foreground.drawRegion = drawRegion;
	}
}
