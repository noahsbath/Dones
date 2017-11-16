﻿using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class Note : MonoBehaviour
{
	protected ActionManager actionManager_ = new ActionManager();
	protected List<LineField> heapFields_ = new List<LineField>();
	
	protected LayoutElement layout_;
	protected ContentSizeFitter contentSizeFitter_;
	protected ScrollRect scrollRect_;
	protected float targetScrollValue_ = 1.0f;
	protected bool isScrollAnimating_;

	protected virtual void Awake()
	{
		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
		scrollRect_ = GetComponentInParent<ScrollRect>();
	}


	// Update is called once per frame
	protected virtual void Update()
	{
		if( isScrollAnimating_ )
		{
			scrollRect_.verticalScrollbar.value = Mathf.Lerp(scrollRect_.verticalScrollbar.value, targetScrollValue_, 0.2f);
			if( Mathf.Abs(scrollRect_.verticalScrollbar.value - targetScrollValue_) < 0.01f )
			{
				scrollRect_.verticalScrollbar.value = targetScrollValue_;
				isScrollAnimating_ = false;
			}
		}
	}


	public virtual void ScrollTo(Line targetLine)
	{
		float scrollHeight = scrollRect_.GetComponent<RectTransform>().rect.height;
		float targetAbsolutePositionY = targetLine.TargetAbsolutePosition.y;
		float targetHeight = -(targetAbsolutePositionY - this.transform.position.y);
		float heightPerLine = GameContext.Config.HeightPerLine;

		// focusLineが下側に出て見えなくなった場合
		float targetUnderHeight = -(targetAbsolutePositionY - scrollRect_.transform.position.y) + heightPerLine / 2 - scrollHeight;
		if( targetUnderHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01(1.0f - (targetHeight + heightPerLine * 1.5f - scrollHeight) / (layout_.preferredHeight - scrollHeight));
			isScrollAnimating_ = true;
			return;
		}

		// focusLineが上側に出て見えなくなった場合
		float targetOverHeight = (targetAbsolutePositionY - scrollRect_.transform.position.y);
		if( targetOverHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01((layout_.preferredHeight - scrollHeight - targetHeight) / (layout_.preferredHeight - scrollHeight));
			isScrollAnimating_ = true;
			return;
		}
	}


	public void CheckScrollbarEnabled()
	{
		if( scrollRect_.verticalScrollbar.isActiveAndEnabled == false )
		{
			scrollRect_.verticalScrollbar.value = 1.0f;
		}
	}

	public virtual void UpdateLayoutElement()
	{
	}
}
