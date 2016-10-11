using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
// ReSharper disable InconsistentNaming

namespace DynamicTypeDescriptor
{
	internal class PropertyValueUIService : IPropertyValueUIService
	{
		private EventHandler m_NotifyHandler;
		private PropertyValueUIHandler m_ValueUIHandler;

		/// <summary>
		///     Adds or removes an event handler that will be invoked
		///     when the global list of PropertyValueUIItems is modified.
		/// </summary>
		event EventHandler IPropertyValueUIService.PropertyUIValueItemsChanged
		{
			add
			{
				lock (this)
					m_NotifyHandler += value;
			}
			remove
			{
				lock (this)
					m_NotifyHandler -= value;
			}
		}

		/// <summary>
		///     Tell the IPropertyValueUIService implementation that the global list of PropertyValueUIItems has been modified.
		/// </summary>
		void IPropertyValueUIService.NotifyPropertyValueUIItemsChanged()
		{
			m_NotifyHandler?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		///     Adds a PropertyValueUIHandler to this service.  When GetPropertyUIValueItems is
		///     called, each handler added to this service will be called and given the opportunity
		///     to add an icon to the specified property.
		/// </summary>
		/// <param name="newHandler"></param>
		void IPropertyValueUIService.AddPropertyValueUIHandler(PropertyValueUIHandler newHandler)
		{
			if (newHandler == null)
			{
				throw new ArgumentNullException(nameof(newHandler));
			}
			lock (this)
				m_ValueUIHandler = (PropertyValueUIHandler) Delegate.Combine(m_ValueUIHandler, newHandler);
		}

		/// <summary>
		///     Removes a PropertyValueUIHandler to this service.  When GetPropertyUIValueItems is
		///     called, each handler added to this service will be called and given the opportunity
		///     to add an icon to the specified property.
		/// </summary>
		/// <param name="newHandler"></param>
		void IPropertyValueUIService.RemovePropertyValueUIHandler(PropertyValueUIHandler newHandler)
		{
			if (newHandler == null)
			{
				throw new ArgumentNullException(nameof(newHandler));
			}

			m_ValueUIHandler = (PropertyValueUIHandler) Delegate.Remove(m_ValueUIHandler, newHandler);
		}

		/// <summary>
		///     Gets all the PropertyValueUIItems that should be displayed on the given property.
		///     For each item returned, a glyph icon will be aded to the property.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="propDesc"></param>
		/// <returns></returns>
		PropertyValueUIItem[] IPropertyValueUIService.GetPropertyUIValueItems(ITypeDescriptorContext context,
			PropertyDescriptor propDesc)
		{
			if (propDesc == null)
			{
				throw new ArgumentNullException(nameof(propDesc));
			}

			if (m_ValueUIHandler == null)
			{
				return new PropertyValueUIItem[0];
			}


			lock (this)
			{
				var result = new ArrayList();

				m_ValueUIHandler(context, propDesc, result);

				return (PropertyValueUIItem[]) result.ToArray(typeof (PropertyValueUIItem));
			}
		}
	}


	internal sealed class SimpleSite : ISite
	{
		private Dictionary<Type, object> services;

		public IComponent Component { get; set; }

		IContainer ISite.Container { get; } = new Container();

		public bool DesignMode { get; set; }

		public string Name { get; set; }

		object IServiceProvider.GetService(Type serviceType)
		{
			object service;
			if (services != null && services.TryGetValue(serviceType, out service))
			{
				return service;
			}
			return null;
		}

		public void AddService<T>(T service) where T : class
		{
			if (services == null)
				services = new Dictionary<Type, object>();
			services[typeof (T)] = service;
		}

		public void RemoveService<T>() where T : class
		{
			services?.Remove(typeof (T));
		}
	}
}