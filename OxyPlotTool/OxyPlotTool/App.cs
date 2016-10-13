using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

using PngExporter = OxyPlot.WindowsForms.PngExporter;
using PdfExporter = OxyPlot.Pdf.PdfExporter;
using SvgExporter = OxyPlot.WindowsForms.SvgExporter;

// ReSharper disable LocalizableElement

namespace OxyPlotTool
{
	public partial class App : Form
	{
		public App()
		{
			InitializeComponent();
			dataGridView.DataSource = dataTable;
			AddTypeDescriptors();
			plotView.Model = plotModel;
			propertyGrid.SelectedObject = dataList;
			dataList.PlotModel = plotModel;
		}

		private readonly PlotModel plotModel = new PlotModel();
		private readonly DataTable dataTable = new DataTable();
		private readonly SeriesDataList dataList = new SeriesDataList();

		class SeriesTypeConverter : ExpandableObjectConverter
		{
			private static readonly string[] names;
			private static readonly Dictionary<string, Series> map;

			static SeriesTypeConverter()
			{
				// Find all the series types
				var types = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(a => a.GetExportedTypes()
						.Where(t => t.IsSubclassOf(typeof(Series)) && !t.IsAbstract))
					.Where(t => t != typeof(FunctionSeries))
					.ToArray();

				// Hide all the properties we're not interested in
				foreach (var type in types)
				{
					var dtd = DynamicTypeDescriptor.Install(type);
					foreach (var prop in dtd.GetProperties())
					{
						var dprop = (DynamicPropertyDescriptor)prop;
						if (dprop.IsReadOnly ||
							dprop.PropertyType.IsSubclassOf(typeof(Delegate)) ||
							dprop.PropertyType == typeof(object) ||
							dprop.Name == "Parent" ||
							dprop.Name.EndsWith("Field"))
							dprop.SetIsBrowsable(false);
					}
				}

				// Create the name mapping
				map = types.ToDictionary(ToString, t => (Series)Activator.CreateInstance(t));
				names = types.Select(ToString).ToArray();
			}

			static string ToString(Type type)
			{
				if (type == null) return "";
				var sb = new StringBuilder();
				var name = type.Name;
				if (name.EndsWith("Series"))
					name = name.Substring(0, name.Length - "Series".Length);
				for (int i = 0; i < name.Length; i++)
				{
					if (i != 0 && char.IsUpper(name[i]))
						sb.Append(' ');
					sb.Append(name[i]);
				}
				return sb.ToString();
			}

			// Show the list of series types
			public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
			{
				return true;
			}

			public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
			{
				return new StandardValuesCollection(names);
			}

			// Convert the display name to the series object.
			// Really annoyingly, WF wants to convert any standard value to string first, then passes
			// that back as a vaue to convert from.
			public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
			{
				if (sourceType == typeof(string)) return true;
				return base.CanConvertFrom(context, sourceType);
			}

			public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
			{
				if (value == null) return null;
				try
				{
					return map[(string)value];
				}
				catch
				{
					// ignored
				}
				return base.ConvertFrom(context, culture, value);
			}

			// Here we make sure that the value displayed in the list is the display name we want,
			// otherwise it'll just use the full type name.
			public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
			{
				if (destinationType == typeof(string) && value is Series) return ToString(value.GetType());
				return base.ConvertTo(context, culture, value, destinationType);
			}
		}

		class SeriesDataConverter : ExpandableObjectConverter
		{
			public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
			{
				return true;
			}

			public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
			{
				var list = (SeriesDataList)context.Instance;
				return new StandardValuesCollection(list.List.ToArray());
			}

			public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
			{
				if (sourceType == typeof (string)) return true;
				return base.CanConvertFrom(context, sourceType);
			}

			public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
			{
				if (value == null) return null;
				try
				{
					return ((SeriesDataList) context.Instance).List[int.Parse((string) value)];
				}
				catch
				{
					// Ignored
				}
				return base.ConvertFrom(context, culture, value);
			}

			public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
			{
				var list = (SeriesDataList)context.Instance;
				if (destinationType == typeof (string)) return list.List.IndexOf((SeriesData) value).ToString();
				return base.ConvertTo(context, culture, value, destinationType);
			}
		}

		private static readonly Dictionary<Type, Type> itemTypes = new Dictionary<Type, Type>();

		static Type GetItemType(Type seriesType)
		{
			Type ret;
			if(!itemTypes.TryGetValue(seriesType, out ret))
				itemTypes.Add(seriesType, ret = GetItemTypeInternal(seriesType));
			return ret;
		}

		static Type GetItemTypeInternal(Type seriesType)
		{
			while (seriesType != typeof (Series))
			{
				// ReSharper disable once PossibleNullReferenceException
				if (seriesType.Name.EndsWith("VolumeSeries"))
					return typeof(OhlcvItem);
				var nameWithoutSeries = seriesType.Name.Replace("Series", "");
				var nameWithPoint = seriesType.Name.Replace("Series", "Point");
				var nameWithItem = seriesType.Name.Replace("Series", "Item");
				var nameWithSlice = seriesType.Name.Replace("Series", "Slice");
				var it = seriesType.Assembly.GetExportedTypes().FirstOrDefault(t =>
					t.Name == nameWithItem ||
					t.Name == nameWithPoint ||
					t.Name == nameWithoutSeries ||
					t.Name == nameWithSlice
					);
				if (it != null) return it;
				seriesType = seriesType.BaseType;
			}
			return null;
		}

		protected class SeriesDataList
		{
			[DisplayName("Series count")]
			public int Count
			{
				get { return List.Count; }
				set
				{
					while (value > List.Count)
						List.Add(new SeriesData());
					while (value < List.Count)
						List.RemoveAt(List.Count - 1);
				}
			}

			[Browsable(false)]
			public List<SeriesData> List { get; } = new List<SeriesData>();

			[DisplayName("Selected series")]
			[TypeConverter(typeof(SeriesDataConverter))]
			public SeriesData SelectedSeries { get; set; }

			[TypeConverter(typeof(ExpandableObjectConverter))]
			public PlotModel PlotModel { get; set; }


			public SeriesDataList()
			{
				Count = 1;
				SelectedSeries = List[0];
			}
		}

		protected class SeriesData
		{
			private Series series;

			[DisplayName("Graph type"), TypeConverter(typeof (SeriesTypeConverter))]
			public Series Series
			{
				get { return series; }
				set
				{
					series = value;
					TypeDescriptor.Refresh(typeof(SeriesData).Assembly);
				}
			}

			[TypeConverter(typeof(ColumnConverter))]
			public Dictionary<string, string> Columns { get; set; } = new Dictionary<string, string>();

			static object CreateInstance(Type t)
			{
				var ctor = t.GetConstructors()[0];
				return ctor.Invoke(ctor.GetParameters().Select(p =>
				{
					var v = p.DefaultValue;
					return v is DBNull ? null : v;
				}).ToArray());
			}

			object RowToItem(Type itemType, DataColumnCollection columns, DataRow row)
			{
				var ret = CreateInstance(itemType);
				var items = row.ItemArray;
				if (items.Length == 0 || items[0] == null || items[0].Equals("")) return null;
				foreach (var pair in Columns)
				{
					if (pair.Key == "" || string.IsNullOrEmpty(pair.Value)) continue;
					try
					{
						var prop = itemType.GetProperty(pair.Key);
						var idx = columns.IndexOf(pair.Value);
						if (prop == null || idx < 0) continue;
						var item = items[idx];
						if (!prop.PropertyType.IsInstanceOfType(item))
						{
							if (item is IConvertible && typeof (IConvertible).IsAssignableFrom(prop.PropertyType))
							{
								item = ((IConvertible) item).ToType(prop.PropertyType, CultureInfo.InvariantCulture);
							}
							else
							{
								var parseMethod = prop.PropertyType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public);
								item = parseMethod.Invoke(null, new[] {item});
							}
						}
						prop.SetValue(ret, item, null);
					}
					catch
					{
						// ignored
					}
				}
				return ret;
			}

			object RowToDataPoint(DataColumnCollection columns, DataRow row)
			{
				try
				{
					var rowX = row[columns.IndexOf(Columns["X"])];
					var rowY = row[columns.IndexOf(Columns["Y"])];
					var x = rowX as double? ?? double.Parse(rowX.ToString());
					var y = rowY as double? ?? double.Parse(rowY.ToString());
					return new DataPoint(x, y); 
				}
				catch
				{
					return null;
				}
			}

			public void AddDataToSeries(DataTable dataTable)
			{
				var itemSeries = Series as ItemsSeries;
				if (itemSeries != null)
				{
					var type = GetItemType(itemSeries.GetType());
					if (type != null)
					{
						// DataPoint is a special snowflake, and doesn't define writable properties
						if (type == typeof (DataPoint))
						{
							var points = ((DataPointSeries) itemSeries).Points;
							points.Clear();
							points.AddRange(dataTable.Rows.Cast<DataRow>()
								.Select(r => RowToDataPoint(dataTable.Columns, r)).OfType<DataPoint>());
						}
						else if (type == typeof (PieSlice))
						{
							((PieSeries) series).Slices = dataTable.Rows.Cast<DataRow>()
								.Select(r => RowToItem(type, dataTable.Columns, r))
								.Where(o => o != null)
								.Cast<PieSlice>().ToList();
						}
						else
						{
							var items = Series.GetType().GetProperty("Items")?.GetValue(Series, null) as IList;
							if (items != null)
							{
								items.Clear();
								foreach (var o in dataTable.Rows.Cast<DataRow>()
									.Select(r => RowToItem(type, dataTable.Columns, r))
									.Where(o => o != null))
								{
									items.Add(o);
								}
							}
							else
							{
								itemSeries.ItemsSource = dataTable.Rows.Cast<DataRow>()
									.Select(r => RowToItem(type, dataTable.Columns, r))
									.Where(o => o != null).ToArray();
							}
						}
						return;
					}
				}
				// TODO: Add heat/contour map data
			}
		}

		class ColumnConverter : TypeConverter
		{
			public static string[] ColumnNames { get; set; }

			static readonly ColumnSelector converter = new ColumnSelector();

			class ColumnProperty : PropertyDescriptor
			{
				public ColumnProperty(string name) : base(name, null)
				{
				}

				public override bool CanResetValue(object component)
				{
					return false;
				}

				public override object GetValue(object component)
				{
					string ret;
					((Dictionary<string, string>) component).TryGetValue(Name, out ret);
					return ret;
				}

				public override void ResetValue(object component)
				{
					throw new NotSupportedException();
				}

				public override void SetValue(object component, object value)
				{
					((Dictionary<string, string>) component)[Name] = (string) value;
				}

				public override bool ShouldSerializeValue(object component)
				{
					return false;
				}

				public override Type ComponentType => typeof (Dictionary<string, string>);
				public override bool IsReadOnly => false;
				public override Type PropertyType => typeof (string);
				public override TypeConverter Converter => converter;
			}

			class ColumnSelector : TypeConverter
			{
				public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
				{
					return true;
				}

				public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
				{
					return new StandardValuesCollection(ColumnNames ?? new string[0]);
				}
			}

			public override bool GetPropertiesSupported(ITypeDescriptorContext context)
			{
				return true;
			}

			public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
			{
				var data = (SeriesData) context.Instance;
				var itemType = data.Series == null ? null : GetItemType(data.Series.GetType());
				if (itemType == null) return null;
				if(itemType == typeof(DataPoint))
					return new PropertyDescriptorCollection(
						new PropertyDescriptor[]{new ColumnProperty("X"), new ColumnProperty("Y")});
				return new PropertyDescriptorCollection(
					itemType.GetProperties()
					.Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
					.Select(p => (PropertyDescriptor)new ColumnProperty(p.Name))
					.ToArray()
					);
			}

			public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
			{
				if (destinationType == typeof (string)) return "";
				return base.ConvertTo(context, culture, value, destinationType);
			}
		}

		

		void AddTypeDescriptors()
		{
			var dtd = DynamicTypeDescriptor.Install(typeof(OxyColor));
			dtd.SetConverter(new OxyColorConverter());
			TypeDescriptor.Refresh(typeof(OxyColor));
		}

		static void CheckedAction(Action a)
		{
			try
			{
				a();
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message, e.GetType().Name);
			}
		}

		void LoadData()
		{
			if (openFileDialog.ShowDialog(this) != DialogResult.OK)
				return;
			var data = File.ReadAllLines(openFileDialog.FileName)
				.Select(l => (" " + l).Split(',')
					.Select(s => s.Trim())
					.ToArray())
				.ToArray();
			dataTable.Clear();
			dataTable.Columns.Clear();
			ColumnConverter.ColumnNames = data[0];
			dataTable.Columns.AddRange(data[0].Select(n => new DataColumn(n)).ToArray());
			for (int i = 1; i < data.Length; i++)
			{
				while (dataTable.Columns.Count < data[i].Length)
					dataTable.Columns.Add(new DataColumn());
				// ReSharper disable once CoVariantArrayConversion
				dataTable.Rows.Add(data[i]);
			}
		}

		private void dataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
		{
			foreach (var data in dataList.List)
			{
				data.AddDataToSeries(dataTable);
			}
			plotView.InvalidatePlot(true);
		}

		private void loadToolStripMenuItem_Click(object sender, EventArgs e)
		{
			CheckedAction(LoadData);
		}

		private void RefreshGraph()
		{
			plotModel.Series.Clear();
			plotModel.Axes.Clear();
			foreach (var data in dataList.List)
			{
				if (data.Series != null)
				{
					if(data.Series is TornadoBarSeries || data.Series is IntervalBarSeries)
						if(!plotModel.Axes.Any(a => a is CategoryAxis && (a.Position == AxisPosition.Left || a.Position == AxisPosition.Right)))
							plotModel.Axes.Add(new CategoryAxis {Position = AxisPosition.Left});
					plotModel.Series.Add(data.Series);
					data.AddDataToSeries(dataTable);
				}
			}
			plotView.InvalidatePlot(true);
		}

		private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
		{
			if (e.ChangedItem.PropertyDescriptor != null && e.ChangedItem.PropertyDescriptor.Name == "Series")
			{
				RefreshGraph();
			}
		}

		private static readonly Dictionary<string, IExporter> exporters =
			new Dictionary<string, IExporter>
		{
			{".png", new PngExporter()},
			{".pdf", new PdfExporter {Width = 700, Height = 400} },
			{".svg", new SvgExporter()},
		};

		void Export()
		{
			if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
				return;
			var path = saveFileDialog.FileName;
			var exporter = exporters[(Path.GetExtension(path) ?? "").ToLowerInvariant()];
			var dialog = new ExportOptions {SelectedObject = exporter};
			if (dialog.ShowDialog(this) != DialogResult.OK)
				return;
			using (var fs = File.OpenWrite(path))
			{
				exporter.Export(plotModel, fs);
			}
		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			CheckedAction(Export);
		}
	}
}
