﻿using Artemis.Core.DataModelExpansions;
using Stylet;

namespace Artemis.UI.Shared
{
    /// <summary>
    ///     Represents a <see cref="DataModel" /> display view model
    /// </summary>
    /// <typeparam name="T">The type of the data model</typeparam>
    public abstract class DataModelDisplayViewModel<T> : DataModelDisplayViewModel
    {
        private T _displayValue;

        /// <summary>
        ///     Gets or sets value that the view model must display
        /// </summary>
        public T DisplayValue
        {
            get => _displayValue;
            set
            {
                if (!SetAndNotify(ref _displayValue, value)) return;
                OnDisplayValueUpdated();
            }
        }

        internal override object InternalGuard => null;

        /// <inheritdoc />
        public override void UpdateValue(object model)
        {
            DisplayValue = model is T value ? value : default;
        }

        /// <summary>
        ///     Occurs when the display value is updated
        /// </summary>
        protected virtual void OnDisplayValueUpdated()
        {
        }
    }

    /// <summary>
    ///     For internal use only, implement <see cref="DataModelDisplayViewModel{T}" /> instead.
    /// </summary>
    public abstract class DataModelDisplayViewModel : PropertyChangedBase
    {
        private DataModelPropertyAttribute _propertyDescription;

        /// <summary>
        ///     Gets the property description of this value
        /// </summary>
        public DataModelPropertyAttribute PropertyDescription
        {
            get => _propertyDescription;
            internal set => SetAndNotify(ref _propertyDescription, value);
        }

        /// <summary>
        ///     Prevents this type being implemented directly, implement <see cref="DataModelDisplayViewModel{T}" /> instead.
        /// </summary>
        internal abstract object InternalGuard { get; }

        /// <summary>
        ///     Updates the display value
        /// </summary>
        /// <param name="model">The value to set</param>
        public abstract void UpdateValue(object model);
    }
}