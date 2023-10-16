// <copyright file="BitmapLoader.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using Android.Graphics;
using FFImageLoading;

namespace Apollo
{
    /// <summary>
    /// Synchronusly loads and returns a bitmap from a URL.
    /// </summary>
    internal class BitmapLoader
    {
        private Bitmap bitmap;

        /// <summary>
        /// Synchronous wrapper to synchronously load and then return a <see cref="Bitmap"/> object.
        /// </summary>
        /// <param name="bitmapUrl">The URL of the target <see cref="Bitmap"/> to load.</param>
        /// <returns><see cref="Bitmap"/> downloaded from the URL.</returns>
        public Bitmap LoadBitmapFromUrl(string bitmapUrl)
        {
            LoadBitmapFromImageService(bitmapUrl);

            return bitmap;
        }

        /// <summary>
        /// Asynchronously loads the <see cref="Bitmap"/>.
        /// </summary>
        /// <param name="bitmapUrl">The URL of the target <see cref="Bitmap"/> to load.</param>
        private async void LoadBitmapFromImageService(string bitmapUrl)
        {
            bitmap = null;
            var bitmapDrawable = await ImageService.Instance.LoadUrl(bitmapUrl).AsBitmapDrawableAsync();
            bitmap = bitmapDrawable.Bitmap;
        }
    }
}