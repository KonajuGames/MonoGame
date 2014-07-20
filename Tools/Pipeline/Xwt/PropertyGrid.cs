using System;
using Xwt;

namespace MonoGame.Tools.Pipeline
{
	public class PropertyGrid : VBox
	{
		ToggleButton _catButton;
		ToggleButton _alphButton;

		VBox _propertyList;

		SortStyle _sorted;

		//object _propertyObject;

		public PropertyGrid()
		{
			CreateButtonBar();


			_propertyList = new VBox();

			var scrollView = new ScrollView() {
				BorderVisible = true,
				Content = _propertyList,
				HorizontalScrollPolicy = ScrollPolicy.Never,
				VerticalScrollPolicy = ScrollPolicy.Always,
				ExpandHorizontal = true,
			};

			this.PackStart(scrollView, true);

//			if (_propertyObject != null)
//				PopulatePropertyList();
			PopulateMockList();
		}

		enum SortStyle 
		{
			Categories,
			Alphabetic
		}

		void CreateButtonBar ()
		{
			var buttonBar = new HBox();
			_catButton = new SortButton("C");
			buttonBar.PackStart(_catButton);
			_alphButton = new SortButton("A");
			buttonBar.PackStart(_alphButton);
			this.PackStart(buttonBar);

			_catButton.Clicked += (sender, e) => SelectedSort(SortStyle.Categories);
			_alphButton.Clicked += (sender, e) => SelectedSort(SortStyle.Alphabetic);
		}

		void SelectedSort(SortStyle style) {
			_sorted = style;

			switch (style) {
			case SortStyle.Alphabetic:
				_catButton.Active = false;
				_alphButton.Active = true;
				break;
			case SortStyle.Categories:
				_alphButton.Active = false;
				_catButton.Active = true;
				break;
			default:
				// Should not enter here.
				break;
			}
		}

		void PopulateMockList() {

			for (int i = 0; i < 10; i++) {
				var firstExpander = new Expander() {
					Label = "Category 1",
					Expanded = true,
					ExpandHorizontal = true,
					MarginLeft = 6
				};

				var firstTable = new Table() {
					DefaultRowSpacing = 1
				};
				firstTable.Add(new Label("Name"), 0, 1, hpos: WidgetPlacement.End, hexpand: true);
				firstTable.Add(new TextEntry() { Text = "New Name" }, 1, 1, hpos: WidgetPlacement.Fill, hexpand: true);
				firstTable.Add(new Label("Type"), 0, 2, hpos: WidgetPlacement.End);
				firstTable.Add(new TextEntry() { Text = "Obj Type", Sensitive = false, ExpandHorizontal=true }, 1, 2, hpos: WidgetPlacement.Fill);
				firstTable.Add(new Label("Select"), 0, 3, hpos: WidgetPlacement.End);

				var cbe = new ComboBoxEntry();
				cbe.Items.Add("One");
				cbe.Items.Add("Two");
				cbe.Items.Add("Three");
				cbe.SelectedIndex = 0;
				firstTable.Add(cbe, 1, 3, hpos: WidgetPlacement.Fill);

				firstTable.Add(new Label("Color"), 0, 4, hpos: WidgetPlacement.End);
				firstTable.Add(new ColorEntry(), 1, 4, hpos: WidgetPlacement.Fill);

				firstExpander.Content = firstTable;
				_propertyList.PackStart(firstExpander);
			}
		}
	}

	class DialogEntry : HBox {

		protected TextEntry _textEntry;
		protected Button _button;

		public DialogEntry(string text = "") {
			_textEntry = new TextEntry();
			_button = new Button("...");

			PackEnd(_button);
			PackStart(_textEntry, true);

		}
	}

	class ColorEntry : DialogEntry {
		public ColorEntry() : base() {
			_button.Clicked += ShowColorDialog;
		}

		void ShowColorDialog (object sender, EventArgs e)
		{
			var colorDialog = new SelectColorDialog() {
				SupportsAlpha = true
			};
			colorDialog.Run();

			var color = colorDialog.Color;
			_textEntry.Text = color.ToString();
		}
	}

	class SortButton : ToggleButton
	{
		public SortButton(string label) {
			Label = label;

			Margin = new WidgetSpacing(2, 2, 2, 2);
			HeightRequest = 24;
			WidthRequest = 24;
		}
	}
}

