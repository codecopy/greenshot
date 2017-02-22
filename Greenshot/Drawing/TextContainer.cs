#region Greenshot GNU General Public License

// Greenshot - a free and open source screenshot tool
// Copyright (C) 2007-2017 Thomas Braun, Jens Klingen, Robin Krom
// 
// For more information see: http://getgreenshot.org/
// The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 1 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#endregion

#region Usings

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Greenshot.Drawing.Fields;
using Greenshot.Helpers;
using Greenshot.Memento;
using GreenshotPlugin.Interfaces.Drawing;

#endregion

namespace Greenshot.Drawing
{
	/// <summary>
	///     Represents a textbox (extends RectangleContainer for border/background support
	/// </summary>
	[Serializable]
	public class TextContainer : RectangleContainer, ITextContainer
	{
		[NonSerialized] private Font _font;

		/// <summary>
		///     The StringFormat object is not serializable!!
		/// </summary>
		[NonSerialized] private StringFormat _stringFormat = new StringFormat();

		[NonSerialized] private TextBox _textBox;

		// If makeUndoable is true the next text-change will make the change undoable.
		// This is set to true AFTER the first change is made, as there is already a "add element" on the undo stack
		// Although the name is wrong, we can't change it due to file serialization
		// ReSharper disable once InconsistentNaming
		private bool makeUndoable;

		// Although the name is wrong, we can't change it due to file serialization
		// ReSharper disable once InconsistentNaming
		private string text;

		public TextContainer(Surface parent) : base(parent)
		{
			Init();
		}

		public Font Font => _font;

		public StringFormat StringFormat => _stringFormat;
		// there is a binding on the following property!
		public string Text
		{
			get { return text; }
			set { ChangeText(value, true); }
		}


		public override void Invalidate()
		{
			base.Invalidate();
			if (_textBox != null && _textBox.Visible)
			{
				_textBox.Invalidate();
			}
		}

		public void FitToText()
		{
			var textSize = TextRenderer.MeasureText(text, _font);
			var lineThickness = GetFieldValueAsInt(FieldType.LINE_THICKNESS);
			Width = textSize.Width + lineThickness;
			Height = textSize.Height + lineThickness;
		}

		/// <summary>
		///     Make sure the size of the font is scaled
		/// </summary>
		/// <param name="matrix"></param>
		public override void Transform(Matrix matrix)
		{
			var rect = GuiRectangle.GetGuiRectangle(Left, Top, Width, Height);
			var pixelsBefore = rect.Width * rect.Height;

			// Transform this container
			base.Transform(matrix);
			rect = GuiRectangle.GetGuiRectangle(Left, Top, Width, Height);

			var pixelsAfter = rect.Width * rect.Height;
			var factor = pixelsAfter / (float) pixelsBefore;

			var fontSize = GetFieldValueAsFloat(FieldType.FONT_SIZE);
			fontSize *= factor;
			SetFieldValue(FieldType.FONT_SIZE, fontSize);
			UpdateFormat();
		}

		public override void ApplyBounds(RectangleF newBounds)
		{
			base.ApplyBounds(newBounds);
			UpdateTextBoxPosition();
		}

		public override bool ClickableAt(int x, int y)
		{
			var r = GuiRectangle.GetGuiRectangle(Left, Top, Width, Height);
			r.Inflate(5, 5);
			return r.Contains(x, y);
		}

		internal void ChangeText(string newText, bool allowUndoable)
		{
			if (text == null && newText != null || !string.Equals(text, newText))
			{
				if (makeUndoable && allowUndoable)
				{
					makeUndoable = false;
					_parent.MakeUndoable(new TextChangeMemento(this), false);
				}
				text = newText;
				OnPropertyChanged("Text");
			}
		}

		protected override void InitializeFields()
		{
			AddField(GetType(), FieldType.LINE_THICKNESS, 2);
			AddField(GetType(), FieldType.LINE_COLOR, Color.Red);
			AddField(GetType(), FieldType.SHADOW, true);
			AddField(GetType(), FieldType.FONT_ITALIC, false);
			AddField(GetType(), FieldType.FONT_BOLD, false);
			AddField(GetType(), FieldType.FILL_COLOR, Color.Transparent);
			AddField(GetType(), FieldType.FONT_FAMILY, FontFamily.GenericSansSerif.Name);
			AddField(GetType(), FieldType.FONT_SIZE, 11f);
			AddField(GetType(), FieldType.TEXT_HORIZONTAL_ALIGNMENT, StringAlignment.Center);
			AddField(GetType(), FieldType.TEXT_VERTICAL_ALIGNMENT, StringAlignment.Center);
		}

		/// <summary>
		///     Do some logic to make sure all field are initiated correctly
		/// </summary>
		/// <param name="streamingContext">StreamingContext</param>
		protected override void OnDeserialized(StreamingContext streamingContext)
		{
			base.OnDeserialized(streamingContext);
			Init();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (_font != null)
				{
					_font.Dispose();
					_font = null;
				}
				if (_stringFormat != null)
				{
					_stringFormat.Dispose();
					_stringFormat = null;
				}
				if (_textBox != null)
				{
					_textBox.Dispose();
					_textBox = null;
				}
			}
			base.Dispose(disposing);
		}

		private void Init()
		{
			_stringFormat = new StringFormat
			{
				Trimming = StringTrimming.EllipsisWord
			};

			CreateTextBox();

			UpdateFormat();
			UpdateTextBoxFormat();

			PropertyChanged += TextContainer_PropertyChanged;
			FieldChanged += TextContainer_FieldChanged;
		}

		private void TextContainer_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (_textBox == null)
			{
				return;
			}

			if (_textBox.Visible)
			{
				_textBox.Invalidate();
			}

			UpdateTextBoxPosition();
			UpdateTextBoxFormat();
			if (e.PropertyName.Equals("Selected"))
			{
				if (!Selected && _textBox.Visible)
				{
					HideTextBox();
				}
				else if (Selected && Status == EditStatus.DRAWING)
				{
					ShowTextBox();
				}
				else if (_parent != null && Selected && Status == EditStatus.IDLE && _textBox.Visible)
				{
					// Fix (workaround) for BUG-1698
					_parent.KeysLocked = true;
				}
			}
			if (_textBox.Visible)
			{
				_textBox.Invalidate();
			}
		}

		private void TextContainer_FieldChanged(object sender, FieldChangedEventArgs e)
		{
			if (_textBox == null)
			{
				return;
			}
			if (_textBox.Visible)
			{
				_textBox.Invalidate();
			}
			// Only dispose the font, and re-create it, when a font field has changed.
			if (e.Field.FieldType.Name.StartsWith("FONT"))
			{
				if (_font != null)
				{
					_font.Dispose();
					_font = null;
				}
				UpdateFormat();
			}
			else
			{
				UpdateAlignment();
			}
			UpdateTextBoxFormat();

			if (_textBox.Visible)
			{
				_textBox.Invalidate();
			}
		}

		public override void OnDoubleClick()
		{
			ShowTextBox();
		}

		private void CreateTextBox()
		{
			_textBox = new TextBox
			{
				ImeMode = ImeMode.On,
				Multiline = true,
				AcceptsTab = true,
				AcceptsReturn = true,
				BorderStyle = BorderStyle.None,
				Visible = false
			};

			_textBox.DataBindings.Add("Text", this, "Text", false, DataSourceUpdateMode.OnPropertyChanged);
			_textBox.LostFocus += textBox_LostFocus;
			_textBox.KeyDown += textBox_KeyDown;
		}

		private void ShowTextBox()
		{
			if (_parent != null)
			{
				_parent.KeysLocked = true;
				_parent.Controls.Add(_textBox);
			}
			EnsureTextBoxContrast();
			if (_textBox != null)
			{
				_textBox.Show();
				_textBox.Focus();
			}
		}

		/// <summary>
		///     Makes textbox background dark if text color is very bright
		/// </summary>
		private void EnsureTextBoxContrast()
		{
			if (_textBox == null)
			{
				return;
			}
			var lc = GetFieldValueAsColor(FieldType.LINE_COLOR);
			if (lc.R > 203 && lc.G > 203 && lc.B > 203)
			{
				_textBox.BackColor = Color.FromArgb(51, 51, 51);
			}
			else
			{
				_textBox.BackColor = Color.White;
			}
		}

		private void HideTextBox()
		{
			_parent.Focus();
			_textBox?.Hide();
			_parent.KeysLocked = false;
			_parent.Controls.Remove(_textBox);
		}

		private Font CreateFont(string fontFamilyName, bool fontBold, bool fontItalic, float fontSize)
		{
			var fontStyle = FontStyle.Regular;

			var hasStyle = false;
			using (var fontFamily = new FontFamily(fontFamilyName))
			{
				var boldAvailable = fontFamily.IsStyleAvailable(FontStyle.Bold);
				if (fontBold && boldAvailable)
				{
					fontStyle |= FontStyle.Bold;
					hasStyle = true;
				}

				var italicAvailable = fontFamily.IsStyleAvailable(FontStyle.Italic);
				if (fontItalic && italicAvailable)
				{
					fontStyle |= FontStyle.Italic;
					hasStyle = true;
				}

				if (!hasStyle)
				{
					var regularAvailable = fontFamily.IsStyleAvailable(FontStyle.Regular);
					if (regularAvailable)
					{
						fontStyle = FontStyle.Regular;
					}
					else
					{
						if (boldAvailable)
						{
							fontStyle = FontStyle.Bold;
						}
						else if (italicAvailable)
						{
							fontStyle = FontStyle.Italic;
						}
					}
				}
				return new Font(fontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
			}
		}

		/// <summary>
		///     Generate the Font-Formal so we can draw correctly
		/// </summary>
		protected void UpdateFormat()
		{
			if (_textBox == null)
			{
				return;
			}
			var fontFamily = GetFieldValueAsString(FieldType.FONT_FAMILY);
			var fontBold = GetFieldValueAsBool(FieldType.FONT_BOLD);
			var fontItalic = GetFieldValueAsBool(FieldType.FONT_ITALIC);
			var fontSize = GetFieldValueAsFloat(FieldType.FONT_SIZE);
			try
			{
				var newFont = CreateFont(fontFamily, fontBold, fontItalic, fontSize);
				_font?.Dispose();
				_font = newFont;
				_textBox.Font = _font;
			}
			catch (Exception ex)
			{
				// Problem, try again with the default
				try
				{
					fontFamily = FontFamily.GenericSansSerif.Name;
					SetFieldValue(FieldType.FONT_FAMILY, fontFamily);
					var newFont = CreateFont(fontFamily, fontBold, fontItalic, fontSize);
					_font?.Dispose();
					_font = newFont;
					_textBox.Font = _font;
				}
				catch (Exception)
				{
					// When this happens... the PC is broken
					ex.Data.Add("fontFamilyName", fontFamily);
					ex.Data.Add("fontBold", fontBold);
					ex.Data.Add("fontItalic", fontItalic);
					ex.Data.Add("fontSize", fontSize);
					throw ex;
				}
			}

			UpdateAlignment();
		}

		private void UpdateAlignment()
		{
			_stringFormat.Alignment = (StringAlignment) GetFieldValue(FieldType.TEXT_HORIZONTAL_ALIGNMENT);
			_stringFormat.LineAlignment = (StringAlignment) GetFieldValue(FieldType.TEXT_VERTICAL_ALIGNMENT);
		}

		/// <summary>
		///     This will create the textbox exactly to the inner size of the element
		///     is a bit of a hack, but for now it seems to work...
		/// </summary>
		private void UpdateTextBoxPosition()
		{
			if (_textBox == null)
			{
				return;
			}
			var lineThickness = GetFieldValueAsInt(FieldType.LINE_THICKNESS);

			var lineWidth = (int) Math.Floor(lineThickness / 2d);
			var correction = (lineThickness + 1) % 2;
			if (lineThickness <= 1)
			{
				lineWidth = 1;
				correction = -1;
			}
			var absRectangle = GuiRectangle.GetGuiRectangle(Left, Top, Width, Height);
			_textBox.Left = absRectangle.Left + lineWidth;
			_textBox.Top = absRectangle.Top + lineWidth;
			if (lineThickness <= 1)
			{
				lineWidth = 0;
			}
			_textBox.Width = absRectangle.Width - 2 * lineWidth + correction;
			_textBox.Height = absRectangle.Height - 2 * lineWidth + correction;
		}

		private void UpdateTextBoxFormat()
		{
			if (_textBox == null)
			{
				return;
			}
			var alignment = (StringAlignment) GetFieldValue(FieldType.TEXT_HORIZONTAL_ALIGNMENT);
			switch (alignment)
			{
				case StringAlignment.Near:
					_textBox.TextAlign = HorizontalAlignment.Left;
					break;
				case StringAlignment.Far:
					_textBox.TextAlign = HorizontalAlignment.Right;
					break;
				case StringAlignment.Center:
					_textBox.TextAlign = HorizontalAlignment.Center;
					break;
			}

			var lineColor = GetFieldValueAsColor(FieldType.LINE_COLOR);
			_textBox.ForeColor = lineColor;
		}

		private void textBox_KeyDown(object sender, KeyEventArgs e)
		{
			// ESC and Enter/Return (w/o Shift) hide text editor
			if (e.KeyCode == Keys.Escape || (e.KeyCode == Keys.Return || e.KeyCode == Keys.Enter) && e.Modifiers == Keys.None)
			{
				HideTextBox();
				e.SuppressKeyPress = true;
			}
		}

		private void textBox_LostFocus(object sender, EventArgs e)
		{
			// next change will be made undoable
			makeUndoable = true;
			HideTextBox();
		}

		public override void Draw(Graphics graphics, RenderMode rm)
		{
			base.Draw(graphics, rm);

			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.CompositingQuality = CompositingQuality.HighQuality;
			graphics.PixelOffsetMode = PixelOffsetMode.None;
			graphics.TextRenderingHint = TextRenderingHint.SystemDefault;

			var rect = GuiRectangle.GetGuiRectangle(Left, Top, Width, Height);
			if (Selected && rm == RenderMode.EDIT)
			{
				DrawSelectionBorder(graphics, rect);
			}

			if (string.IsNullOrEmpty(text))
			{
				return;
			}

			// we only draw the shadow if there is no background
			var shadow = GetFieldValueAsBool(FieldType.SHADOW);
			var fillColor = GetFieldValueAsColor(FieldType.FILL_COLOR);
			var lineThickness = GetFieldValueAsInt(FieldType.LINE_THICKNESS);
			var lineColor = GetFieldValueAsColor(FieldType.LINE_COLOR);
			var drawShadow = shadow && (fillColor == Color.Transparent || fillColor == Color.Empty);

			DrawText(graphics, rect, lineThickness, lineColor, drawShadow, _stringFormat, text, _font);
		}

		/// <summary>
		///     This method can be used from other containers
		/// </summary>
		/// <param name="graphics"></param>
		/// <param name="drawingRectange"></param>
		/// <param name="lineThickness"></param>
		/// <param name="fontColor"></param>
		/// <param name="drawShadow"></param>
		/// <param name="stringFormat"></param>
		/// <param name="text"></param>
		/// <param name="font"></param>
		public static void DrawText(Graphics graphics, Rectangle drawingRectange, int lineThickness, Color fontColor, bool drawShadow, StringFormat stringFormat, string text,
			Font font)
		{
#if DEBUG
			Debug.Assert(font != null);
#else
			if (font == null)
			{
				return;
			}
#endif
			var textOffset = lineThickness > 0 ? (int) Math.Ceiling(lineThickness / 2d) : 0;
			// draw shadow before anything else
			if (drawShadow)
			{
				var basealpha = 100;
				var alpha = basealpha;
				var steps = 5;
				var currentStep = 1;
				while (currentStep <= steps)
				{
					var offset = currentStep;
					var shadowRect = GuiRectangle.GetGuiRectangle(drawingRectange.Left + offset, drawingRectange.Top + offset, drawingRectange.Width, drawingRectange.Height);
					if (lineThickness > 0)
					{
						shadowRect.Inflate(-textOffset, -textOffset);
					}
					using (Brush fontBrush = new SolidBrush(Color.FromArgb(alpha, 100, 100, 100)))
					{
						graphics.DrawString(text, font, fontBrush, shadowRect, stringFormat);
						currentStep++;
						alpha = alpha - basealpha / steps;
					}
				}
			}

			if (lineThickness > 0)
			{
				drawingRectange.Inflate(-textOffset, -textOffset);
			}
			using (Brush fontBrush = new SolidBrush(fontColor))
			{
				if (stringFormat != null)
				{
					graphics.DrawString(text, font, fontBrush, drawingRectange, stringFormat);
				}
				else
				{
					graphics.DrawString(text, font, fontBrush, drawingRectange);
				}
			}
		}
	}
}