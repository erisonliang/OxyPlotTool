using System;
using System.ComponentModel;
using System.Globalization;
using OxyPlot;

namespace OxyPlotTool
{
	class OxyColorConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof (string)) return true;
			return base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			try
			{
				return OxyColor.Parse((string) value);
			}
			catch
			{
				// ignored
			}
			return base.ConvertFrom(context, culture, value);
		}
	}
}
