using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace OxyPlotTool
{
	public class DynamicPropertyDescriptor : PropertyDescriptor
	{
		public DynamicPropertyDescriptor(PropertyInfo property) :
			base(property.Name, property.GetCustomAttributes(false).Cast<Attribute>().ToArray())
		{
			this.property = property;
			isReadOnly = !property.CanWrite;
			displayName = base.DisplayName;
			description = base.Description;
			category = base.Category;
		}

		private readonly PropertyInfo property;
		private bool isBrowsable = true, isReadOnly;
		private string displayName, description, category;
		private TypeConverter converter;

		// Hacky way to get the default value of a component
		private bool hasDefaultValue;
		private object defaultValue; 

		public override bool CanResetValue(object component)
		{
			if (!hasDefaultValue)
			{
				defaultValue = property.GetValue(component, null);
				hasDefaultValue = true;
			}
			return true;
		}

		public override object GetValue(object component)
		{
			return property.GetValue(component, null);
		}

		public override void ResetValue(object component)
		{
			SetValue(component, defaultValue);
		}

		public override void SetValue(object component, object value)
		{
			property.SetValue(component, value, null);
		}

		public override bool ShouldSerializeValue(object component)
		{
			return false;
		}

		public override Type ComponentType => property.DeclaringType;
		public override Type PropertyType => property.PropertyType;

		public override bool IsReadOnly => isReadOnly;
		public void SetIsReadOnly(bool value) => isReadOnly = value;

		public override bool IsBrowsable => isBrowsable;
		public void SetIsBrowsable(bool value) => isBrowsable = value;

		public override string DisplayName => displayName;
		public void SetDisplayName(string value) => displayName = value;

		public override string Description => description;
		public void SetDescription(string value) => description = value;

		public override string Category => category;
		public void SetCategory(string value) => category = value;

		public override TypeConverter Converter => converter ?? TypeDescriptor.GetConverter(property.PropertyType);
		public void SetConverter(TypeConverter value) => converter = value;

		public void AddAttribute(Attribute attr)
		{
			if(attr == null) throw new ArgumentNullException(nameof(attr));
			var attrs = AttributeArray.ToList();
			if (!attrs.Contains(attr))
			{
				attrs.Add(attr);
				AttributeArray = attrs.ToArray();
			}
		}

		public void RemoveAttribute(Attribute attr)
		{
			if (attr == null) throw new ArgumentNullException(nameof(attr));
			var attrs = AttributeArray.ToList();
			if (attrs.Remove(attr))
				AttributeArray = attrs.ToArray();
		}
	}

	public class DropDownConverter : TypeConverter
	{
		public ICollection StandardValues { get; set; }

		public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
		{
			return true;
		}

		public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
		{
			return new StandardValuesCollection(StandardValues);
		}
	}

	public class DynamicTypeDescriptor : CustomTypeDescriptor
	{
		class CustomProvider : TypeDescriptionProvider
		{
			
			private readonly Dictionary<Type, DynamicTypeDescriptor> map = new Dictionary<Type, DynamicTypeDescriptor>(); 
			private readonly Dictionary<object, ICustomTypeDescriptor> perInstanceMap = new Dictionary<object, ICustomTypeDescriptor>();

			public void Install(object instance, ICustomTypeDescriptor typeDescriptor)
			{
				perInstanceMap[instance] = typeDescriptor;
			}

			public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
			{
				if (instance != null)
				{
					ICustomTypeDescriptor perInstance;
					perInstanceMap.TryGetValue(instance, out perInstance);
					if (perInstance != null)
						return perInstance;
				}
				DynamicTypeDescriptor ret;
				if(!map.TryGetValue(objectType, out ret))
					map.Add(objectType, ret = new DynamicTypeDescriptor(objectType, parentProvider.GetTypeDescriptor(objectType)));
				return ret;
			}
		}

		static readonly CustomProvider provider = new CustomProvider();
		// This is the basic reflection-based provider
		private static readonly TypeDescriptionProvider parentProvider;

		static DynamicTypeDescriptor()
		{
			parentProvider = TypeDescriptor.GetProvider(typeof (object));
		}

		public static void Install(object instance, ICustomTypeDescriptor typeDescriptor)
		{
			TypeDescriptor.AddProvider(provider, instance);
			provider.Install(instance, typeDescriptor);
		}

		public static DynamicTypeDescriptor Install(Type type)
		{
			TypeDescriptor.AddProvider(provider, type);
			return (DynamicTypeDescriptor)provider.GetTypeDescriptor(type, null);
		}

		private TypeConverter typeConverter;
		private readonly PropertyDescriptorCollection properties;

		private DynamicTypeDescriptor(Type type, ICustomTypeDescriptor parent) : base(parent)
		{
			// ReSharper disable once CoVariantArrayConversion
			properties = new PropertyDescriptorCollection(type.GetProperties().Select(p => new DynamicPropertyDescriptor(p)).ToArray());
		}

		public DynamicPropertyDescriptor GetProperty(string name)
		{
			return (DynamicPropertyDescriptor)properties[name];
		}

		public override PropertyDescriptorCollection GetProperties()
		{
			return properties;
		}

		public override TypeConverter GetConverter()
		{
			return typeConverter ?? base.GetConverter();
		}

		public void SetConverter(TypeConverter value)
		{
			typeConverter = value;
		}

		public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			//return GetProperties();
			if(attributes == null || attributes.Length == 0) return GetProperties();
			IEnumerable<DynamicPropertyDescriptor> filtered = properties.Cast<DynamicPropertyDescriptor>();
			foreach (var attr in attributes)
			{
				if (attr is BrowsableAttribute)
					filtered = filtered.Where(p => p.IsBrowsable);
				else if (attr is CategoryAttribute)
					filtered = filtered.Where(p => p.Category == ((CategoryAttribute) attr).Category);
				else
					filtered = filtered.Where(p => p.Attributes.Contains(attr));
			}
			// ReSharper disable once CoVariantArrayConversion
			return new PropertyDescriptorCollection(filtered.ToArray());
		}
	}
}
