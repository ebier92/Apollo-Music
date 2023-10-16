// <copyright file="ObjectConversions.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;

namespace Apollo
{
    /// <summary>
    /// Class to convert to and from Java and .NET objects.
    /// </summary>
    internal static class ObjectConversions
    {
        /// <summary>
        /// Converts a .NET object to a Java object.
        /// </summary>
        /// <typeparam name="TObject">The object type to return.</typeparam>
        /// <param name="value">The <see cref="Java.Lang.Object"/> value to be converted.</param>
        /// <returns>A .NET object.</returns>
        public static TObject ToNetObject<TObject>(this Java.Lang.Object value)
        {
            if (value == null)
                return default;

            if (!(value is JavaHolder))
                throw new InvalidOperationException("Unable to convert to .NET object. Only Java.Lang.Object created with .ToJavaObject() can be converted.");

            TObject returnVal;
            try
            {
                returnVal = (TObject)((JavaHolder)value).Instance;
            }
            finally
            {
                value.Dispose();
            }

            return returnVal;
        }

        /// <summary>
        /// Converts a Java object to a .NET object.
        /// </summary>
        /// <typeparam name="TObject">The object type to return.</typeparam>
        /// <param name="value">The .NET object to be converted.</param>
        /// <returns>A <see cref="Java.Lang.Object"/>.</returns>
        public static Java.Lang.Object ToJavaObject<TObject>(this TObject value)
        {
            if (Equals(value, default(TObject)) && !typeof(TObject).IsValueType)
                return null;

            var holder = new JavaHolder(value);

            return holder;
        }

        /// <summary>
        /// Holds the value of a Java object for conversion.
        /// </summary>
        public class JavaHolder : Java.Lang.Object
        {
            public JavaHolder(object instance)
            {
                Instance = instance;
            }

            public object Instance { get; private set; }
        }
    }
}