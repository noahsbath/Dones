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

// Window > [ Note ] > LineTree > Line
public class Note : MonoBehaviour
{
	public ActionManager ActionManager { get { return actionManager_; } }
	protected ActionManager actionManager_ = new ActionManager();
	
	protected LayoutElement layout_;
	protected ContentSizeFitter contentSizeFitter_;
	protected ScrollRect scrollRect_;
	protected RectTransform scrollRectTransform_;

	public Rect Rect { get { return new Rect((Vector2)scrollRectTransform_.position + scrollRectTransform_.rect.position, scrollRectTransform_.rect.size); } }

	public float TargetScrollValue { get { return targetScrollValue_; } }
	protected float targetScrollValue_ = 1.0f;

	protected bool isScrollAnimating_;

	protected List<LineTree> saveRequestedTrees_ = new List<LineTree>();
	protected float lastSaveRequestedTime_ = 0;

	protected virtual void Awake()
	{
		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
		scrollRect_ = GetComponentInParent<ScrollRect>();
		scrollRectTransform_ = scrollRect_.GetComponent<RectTransform>();
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


	public virtual void ScrollTo(Line targetLine, bool immediate = false)
	{
		float scrollHeight = scrollRectTransform_.rect.height;
		float targetAbsolutePositionY = targetLine.TargetAbsolutePosition.y;
		float targetHeight = -(targetAbsolutePositionY - this.transform.position.y);
		float heightPerLine = GameContext.Config.HeightPerLine;

		// focusLineが下側に出て見えなくなった場合
		float targetUnderHeight = -(targetAbsolutePositionY - scrollRect_.transform.position.y) + heightPerLine / 2 - scrollHeight;
		if( targetUnderHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01(1.0f - (targetHeight + heightPerLine * 1.5f - scrollHeight) / (layout_.preferredHeight - scrollHeight));
			if( immediate )
			{
				scrollRect_.verticalScrollbar.value = targetScrollValue_;
			}
			else
			{
				isScrollAnimating_ = true;
			}
			return;
		}

		// focusLineが上側に出て見えなくなった場合
		float targetOverHeight = (targetAbsolutePositionY - scrollRect_.transform.position.y);
		if( targetOverHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01((layout_.preferredHeight - scrollHeight - targetHeight) / (layout_.preferredHeight - scrollHeight));
			if( immediate )
			{
				scrollRect_.verticalScrollbar.value = targetScrollValue_;
			}
			else
			{
				isScrollAnimating_ = true;
			}
			return;
		}
	}
	
	public void CheckScrollbarEnabled()
	{
		if( scrollRect_.verticalScrollbar.enabled == false )
		{
			scrollRect_.verticalScrollbar.value = 1.0f;
		}
	}

	public virtual void UpdateLayoutElement()
	{
	}


	public virtual void Activate()
	{
		this.gameObject.SetActive(true);
		UpdateLayoutElement();
	}

	public virtual void Deactivate()
	{
		this.gameObject.SetActive(false);
		if( saveRequestedTrees_.Count > 0 )
		{
			DoAutoSave();
		}
	}

	public virtual void SetNoteViewParam(NoteViewParam param)
	{
		scrollRect_.verticalScrollbar.value = param.TargetScrollValue;
	}

	public virtual void CacheNoteViewParam(NoteViewParam param)
	{
		param.TargetScrollValue = scrollRect_.verticalScrollbar.gameObject.activeInHierarchy ? scrollRect_.verticalScrollbar.value : 1.0f;
	}

	public virtual void Destroy()
	{
		Destroy(this.gameObject);
	}

	public virtual void OnEdited(object sender, EventArgs e)
	{
		LineTree tree = sender as LineTree;
		if( saveRequestedTrees_.Contains(tree) == false )
			saveRequestedTrees_.Add(tree);
		
		lastSaveRequestedTime_ = Time.time;
		GameContext.Window.SaveText.StartSaving();
	}

	public virtual void ReloadNote() { }

	public float TimeFromRequestedAutoSave()
	{
		return saveRequestedTrees_.Count > 0  ? Time.time - lastSaveRequestedTime_ : -1;
	}

	public void DoAutoSave()
	{
		foreach( LineTree tree in saveRequestedTrees_ )
		{
			tree.SaveFile();
		}
		saveRequestedTrees_.Clear();
		lastSaveRequestedTime_ = 0;
		GameContext.Window.SaveText.Saved();
	}
}
