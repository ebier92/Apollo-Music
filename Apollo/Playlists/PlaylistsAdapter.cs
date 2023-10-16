// <copyright file="PlaylistsAdapter.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Android.Media.Browse;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using FFImageLoading;

namespace Apollo
{
    /// <summary>
    /// Adapter class for the playlist <see cref="RecyclerView"/>.
    /// </summary>
    internal class PlaylistsAdapter : RecyclerView.Adapter
    {
        private readonly IList<MediaBrowser.MediaItem> playlistItems;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistsAdapter"/> class.
        /// </summary>
        /// <param name="playlistItems">List of playlist items.</param>
        public PlaylistsAdapter(IList<MediaBrowser.MediaItem> playlistItems)
        {
            this.playlistItems = playlistItems;
        }

        public EventHandler<string> ItemClick { get; set; }

        public override int ItemCount
        {
            get { return playlistItems != null ? playlistItems.Count : 0; }
        }

        /// <summary>
        /// Initializes the instance of the <see cref="PlaylistsViewHolder"/> and layout.
        /// </summary>
        /// <param name="parent">The parent <see cref="ViewGroup"/>.</param>
        /// <param name="viewType">The integer view type.</param>
        /// <returns>Instance of the <see cref="PlaylistsViewHolder"/>.</returns>
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            View view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.playlist_item, parent, false);
            PlaylistsFragment.PlaylistsViewHolder viewHolder = new PlaylistsFragment.PlaylistsViewHolder(view, OnClick);

            return viewHolder;
        }

        /// <summary>
        /// Populates text information for each displayed item in the <see cref="RecyclerView"/>.
        /// </summary>
        /// <param name="holder">Instance of a <see cref="PlaylistsViewHolder"/>.</param>
        /// <param name="position">Item position.</param>
        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            PlaylistsFragment.PlaylistsViewHolder viewHolder = holder as PlaylistsFragment.PlaylistsViewHolder;

            viewHolder.PlaylistTitle.Text = playlistItems[position].Description.Title;
            viewHolder.PlaylistTrackCount.Text = playlistItems[position].Description.Subtitle;
            ImageService.Instance.LoadUrl(playlistItems[position].Description.IconUri.ToString()).Into(viewHolder.PlaylistThumbnail);
        }

        /// <summary>
        /// Click event handler to invoke the custom <see cref="OnItemClick(object, string)"/> method.
        /// </summary>
        /// <param name="playlistName">Name of the playlist that was clicked.</param>
        private void OnClick(string playlistName)
        {
            ItemClick?.Invoke(this, playlistName);
        }
    }
}