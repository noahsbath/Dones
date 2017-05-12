﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UniRx;
using UniRx.Triggers;

public class Tree : MonoBehaviour {
	
	public TextField FieldPrefab;
	public List<TextField> Fields = new List<TextField>();

	Line rootLine_;
	Line focusedLine_;
	Line selectionStartLine_, selectionEndLine_;
	List<Line> selectedLines_ = new List<Line>();

	/// <summary>
	/// startから見たendの方向。上(Prev)がプラス。下(Next)がマイナス。
	/// </summary>
	int SelectionSign
	{
		get
		{
			if( selectionStartLine_ == null || selectionEndLine_ == null || selectionStartLine_ == selectionEndLine_ ) return 0;
			else return selectionStartLine_.Field.RectY < selectionEndLine_.Field.RectY ? 1 : -1;
		}
	}

	bool HasSelection { get { return selectedLines_.Count > 0; } }

	// Use this for initialization
	void Awake () {
		// test
		rootLine_ = new Line("CategoryName");
		rootLine_.Add(new Line("Hello World"));
		rootLine_.Add(new Line("Hello1"));
		rootLine_.Add(new Line("Hello2"));
		rootLine_.Add(new Line("Hello3"));
		rootLine_.Add(new Line("Hello4"));
		rootLine_.Add(new Line("Hello5"));
		rootLine_.Add(new Line("Hello6"));
		rootLine_.Bind(this.gameObject);

		foreach( Line line in rootLine_.VisibleTree )
		{
			InstantiateLine(line);
		}

		OnFocused(Fields[0].BindedLine);

		SubscribeKeyInput();
	}

	protected void SubscribeKeyInput()
	{
		KeyCode[] throttleKeys = new KeyCode[]
		{
			KeyCode.UpArrow,
			KeyCode.DownArrow,
			KeyCode.RightArrow,
			KeyCode.LeftArrow,
			KeyCode.Return,
			KeyCode.Backspace,
			KeyCode.Delete
		};

		foreach( KeyCode key in throttleKeys )
		{
			// 最初の入力
			this.UpdateAsObservable()
				.Where(x => Input.GetKeyDown(key))
				.Merge(
			// 押しっぱなしにした時の自動連打
			this.UpdateAsObservable()
				.Where(x => Input.GetKey(key))
				.Delay(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamDelayTime))
				.ThrottleFirst(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamIntervalTime)))
				.TakeUntil(this.UpdateAsObservable().Where(x => Input.GetKeyUp(key)))
				.RepeatUntilDestroy(this)
				.Subscribe(_ => OnThrottleInput(key));
		}
	}
	
	// Update is called once per frame
	void Update()
	{
		bool ctrl = Input.GetKey(KeyCode.LeftControl);
		bool shift = Input.GetKey(KeyCode.LeftShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt);
		bool ctrlOnly = ctrl && !alt && !shift;

		if( Input.GetKeyDown(KeyCode.V) && ctrlOnly )
		{
			Paste();
		}
		else if( Input.GetKeyDown(KeyCode.Tab) )
		{
			if( shift == false )
			{
				int IndexInParent = focusedLine_.IndexInParent;
				if( IndexInParent > 0 )
				{
					Line newParent = focusedLine_.Parent[IndexInParent - 1];
					newParent.Add(focusedLine_);
					
					if( newParent.IsFolded )
					{
						newParent.IsFolded = false;
						newParent.AdjustLayoutRecursive();
					}
					else
					{
						focusedLine_.AdjustLayout(Line.Direction.X);
					}
				}
			}
			else
			{
				if( focusedLine_.Parent != null && focusedLine_.Parent.Parent != null )
				{
					Line newParent = focusedLine_.Parent.Parent;
					int insertIndex = focusedLine_.Parent.IndexInParent + 1;

					int index = focusedLine_.IndexInParent + 1;
					Line nextOfFocusedLine = focusedLine_.Parent[index];
					newParent.Insert(insertIndex, focusedLine_);
					if( nextOfFocusedLine != null )
					{
						nextOfFocusedLine.Parent.AdjustLayoutRecursive(index - 1);
					}
					else
					{
						focusedLine_.AdjustLayout(Line.Direction.X);
					}
				}
			}
		}

		if( Input.GetMouseButtonDown(0) )
		{
			if( shift )
			{
				if( ctrl == false )
				{
					ClearSelection(clearStartAndEnd: false);
				}
				if( selectionStartLine_ != null )
				{
					foreach( Line line in selectionStartLine_.GetUntil(Input.mousePosition.y) )
					{
						UpdateSelection(line, true);
						selectionEndLine_ = line;
					}
				}
			}
			else if( ctrl )
			{
				Line line = null;
				TextField field = EventSystem.current.currentSelectedGameObject.GetComponent<TextField>();
				if( field != null ) line = field.BindedLine;
				if( line != null )
				{
					UpdateSelection(line, true);
					selectionStartLine_ = selectionEndLine_ = line;
				}
			}
			else
			{
				ClearSelection();
			}

			if( focusedLine_ != null && focusedLine_.Field.Rect.Contains(Input.mousePosition) )
			{
				selectionStartLine_ = focusedLine_;
			}
		}
		else if( Input.GetMouseButton(0) && selectionStartLine_ != null )
		{
			if( selectionEndLine_ == null )
				selectionEndLine_ = selectionStartLine_;

			// 現在の選択末尾から上に移動したか下に移動したかを見る
			int moveSign = 0;
			Rect rect = selectionEndLine_.Field.Rect;
			if( rect.yMin > Input.mousePosition.y )
			{
				moveSign = -1;
			}
			else if( rect.yMax < Input.mousePosition.y )
			{
				moveSign = 1;
			}

			// 移動していたら
			if( moveSign != 0 )
			{
				foreach( Line line in selectionEndLine_.GetUntil(Input.mousePosition.y) )
				{
					UpdateSelection(line, moveSign * SelectionSign >= 0/* 移動方向と選択方向が逆なら選択解除 */ || line == selectionStartLine_);
					selectionEndLine_ = line;
				}
			}
		}
	}

	protected void UpdateSelection(Line line, bool isSelected)
	{
		line.Field.IsSelected = isSelected;
		if( isSelected && selectedLines_.Contains(line) == false )
		{
			selectedLines_.Add(line);
		}
		else if( isSelected == false && selectedLines_.Contains(line) )
		{
			selectedLines_.Remove(line);
		}
	}

	protected void ClearSelection(bool clearStartAndEnd = true)
	{
		foreach( Line line in selectedLines_ )
		{
			line.Field.IsSelected = false;
		}
		selectedLines_.Clear();

		if( clearStartAndEnd ) selectionStartLine_ = selectionEndLine_ = null;
	}

	protected void InstantiateLine(Line line)
	{
		line.Bind(Instantiate(FieldPrefab.gameObject));
		Fields.Add(line.Field);
	}

	protected void OnThrottleInput(KeyCode key)
	{
		if( focusedLine_ == null ) return;

		int caretPos = focusedLine_.Field.CaretPosision;
		bool ctrl = Input.GetKey(KeyCode.LeftControl);
		bool shift = Input.GetKey(KeyCode.LeftShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt);
		bool ctrlOnly = ctrl && !alt && !shift;

		switch( key )
		{
		case KeyCode.Return:
			{
				Line line = new Line();
				if( caretPos == 0 && focusedLine_.TextLength > 0 )
				{
					focusedLine_.Parent.Insert(focusedLine_.IndexInParent, line);
					focusedLine_.Parent.AdjustLayoutRecursive(focusedLine_.IndexInParent);
				}
				else
				{
					string subString = focusedLine_.Text.Substring(0, caretPos);
					string newString = focusedLine_.Text.Substring(caretPos, focusedLine_.TextLength - caretPos);
					focusedLine_.Text = subString;
					line.Text = newString;
					if( focusedLine_.HasVisibleChild )
					{
						focusedLine_.Insert(0, line);
						focusedLine_.AdjustLayoutRecursive();
					}
					else
					{
						focusedLine_.Parent.Insert(focusedLine_.IndexInParent + 1, line);
						focusedLine_.Parent.AdjustLayoutRecursive(focusedLine_.IndexInParent + 1);
					}
				}
				InstantiateLine(line);
				line.Field.IsFocused = (caretPos == 0 && focusedLine_.TextLength > 0) == false;
			}
			break;
		
		// backspace & delete
		case KeyCode.Backspace:
			if( caretPos == 0 )
			{
				Line prev = focusedLine_.PrevVisibleLine;
				if( prev != null )
				{
					prev.Field.CaretPosision = prev.TextLength;
					prev.Text += focusedLine_.Text;
					prev.Field.IsFocused = true;
					
					List<Line> children = new List<Line>(focusedLine_);
					for( int i = 0; i < children.Count; ++i )
					{
						prev.Insert(i, children[i]);
						children[i].AdjustLayout();
					}

					Line layoutStart = focusedLine_.NextVisibleLine;
					focusedLine_.Parent.Remove(focusedLine_);
					if( layoutStart != null )
					{
						layoutStart.Parent.AdjustLayoutRecursive(layoutStart.IndexInParent);
					}
				}
			}
			break;
		case KeyCode.Delete:
			if( caretPos == focusedLine_.TextLength )
			{
				Line next = focusedLine_.NextVisibleLine;
				if( next != null )
				{
					focusedLine_.Text += next.Text;
					
					List<Line> children = new List<Line>(next);
					for( int i = 0; i < children.Count; ++i )
					{
						focusedLine_.Insert(i, children[i]);
						children[i].AdjustLayout();
					}
					
					Line layoutStart = next.NextVisibleLine;
					next.Parent.Remove(next);
					if( layoutStart != null )
					{
						layoutStart.Parent.AdjustLayoutRecursive(layoutStart.IndexInParent);
					}
				}
			}
			break;
		
		// arrow keys
		case KeyCode.DownArrow:
			if( shift )
			{
				if( selectionEndLine_ == null )
				{
					selectionStartLine_ = selectionEndLine_ = focusedLine_;
				}
				Line next = selectionEndLine_.NextVisibleLine;
				if( next != null )
				{
					if( SelectionSign > 0 ) UpdateSelection(selectionEndLine_, false);
					selectionEndLine_ = next;
					if( SelectionSign < 0 ) UpdateSelection(next, true);
				}
				UpdateSelection(selectionStartLine_, true);
			}
			else
			{
				if( selectionEndLine_ != null )
				{
					selectionEndLine_.Field.IsFocused = true;
					focusedLine_ = selectionEndLine_;
					ClearSelection();
				}
				Line next = focusedLine_.NextVisibleLine;
				if( next != null )
				{
					next.Field.IsFocused = true;
				}
			}
			break;
		case KeyCode.UpArrow:
			if( shift )
			{
				if( selectionEndLine_ == null )
				{
					selectionStartLine_ = selectionEndLine_ = focusedLine_;
				}
				Line prev = selectionEndLine_.PrevVisibleLine;
				if( prev != null )
				{
					if( SelectionSign < 0 ) UpdateSelection(selectionEndLine_, false);
					selectionEndLine_ = prev;
					if( SelectionSign > 0 ) UpdateSelection(prev, true);
				}
				UpdateSelection(selectionStartLine_, true);
			}
			else
			{
				if( HasSelection )
				{
					selectionEndLine_.Field.IsFocused = true;
					focusedLine_ = selectionEndLine_;
					ClearSelection();
				}
				Line prev = focusedLine_.PrevVisibleLine;
				if( prev != null )
				{
					prev.Field.IsFocused = true;
				}
			}
			break;
		case KeyCode.RightArrow:
			if( shift )
			{
				if( selectionEndLine_ == null && caretPos >= focusedLine_.TextLength )
				{
					UpdateSelection(focusedLine_, true);
					selectionStartLine_ = selectionEndLine_ = focusedLine_;
				}
			}
			else
			{
				if( HasSelection )
				{
					selectionEndLine_.Field.IsFocused = true;
					focusedLine_ = selectionEndLine_;
					ClearSelection();
				}
				if( caretPos >= focusedLine_.TextLength )
				{
					Line next = focusedLine_.NextVisibleLine;
					if( next != null )
					{
						next.Field.CaretPosision = 0;
						next.Field.IsFocused = true;
					}
				}
			}
			break;
		case KeyCode.LeftArrow:
			if( shift )
			{
				if( selectionEndLine_ == null && caretPos <= 0 )
				{
					UpdateSelection(focusedLine_, true);
					selectionStartLine_ = selectionEndLine_ = focusedLine_;
				}
			}
			else
			{
				if( selectionEndLine_ != null )
				{
					selectionEndLine_.Field.IsFocused = true;
					focusedLine_ = selectionEndLine_;
					ClearSelection();
				}
				if( caretPos <= 0 )
				{
					Line prev = focusedLine_.PrevVisibleLine;
					if( prev != null )
					{
						prev.Field.CaretPosision = prev.TextLength;
						prev.Field.IsFocused = true;
					}
				}
			}
			break;
		}
	}

	protected static string Clipboard
	{
		get
		{
			return GUIUtility.systemCopyBuffer;
		}
		set
		{
			GUIUtility.systemCopyBuffer = value;
		}
	}
	protected static string[] separator = new string[] { System.Environment.NewLine };
	protected static string[] tabstrings = new string[] { "	", "    " };
	protected void Paste()
	{
		string[] cilpboardLines = Clipboard.Split(separator, System.StringSplitOptions.None);
		focusedLine_.Field.Paste(cilpboardLines[0]);

		int oldLevel = 0;
		int currentLevel = 0;
		Line parent = focusedLine_.Parent;
		Line brother = focusedLine_;
		Line layoutStart = focusedLine_.NextVisibleLine;
		for( int i = 1; i < cilpboardLines.Length; ++i )
		{
			string text = cilpboardLines[i];
			currentLevel = 0;
			while( text.StartsWith("	") )
			{
				++currentLevel;
				text = text.Remove(0, 1);
			}

			Line line = new Line(text);
			if( currentLevel > oldLevel )
			{
				brother.Add(line);
				parent = brother;
			}
			else if( currentLevel == oldLevel )
			{
				parent.Insert(brother.IndexInParent + 1, line);
			}
			else// currentLevel < oldLevel 
			{
				for( int level = oldLevel; level > currentLevel; --level )
				{
					if( parent.Parent == null ) break;

					brother = parent;
					parent = parent.Parent;
				}
				parent.Insert(brother.IndexInParent + 1, line);
			}
			InstantiateLine(line);
			brother = line;
			oldLevel = currentLevel;
		}

		if( layoutStart != null )
		{
			layoutStart.Parent.AdjustLayoutRecursive(layoutStart.IndexInParent);
		}
	}
	

	public void OnFocused(Line line)
	{
		focusedLine_ = line;
		selectionStartLine_ = line;
	}
}
