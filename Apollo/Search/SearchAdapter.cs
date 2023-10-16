// <copyright file="SearchAdapter.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Android.App;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using FFImageLoading;

namespace Apollo
{
    /// <summary>
    /// Adapter class for the search results <see cref="RecyclerView"/>.
    /// </summary>
    internal class SearchAdapter : RecyclerView.Adapter
    {
        private readonly List<YouTube.ContentItem> searchResultContentItems;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchAdapter"/> class.
        /// </summary>
        public SearchAdapter()
        {
            searchResultContentItems = new List<YouTube.ContentItem>();
        }

        public EventHandler<YouTube.ContentItem> ItemClick { get; set; }

        public Action<IMenuItem, YouTube.ContentItem> ItemMenuClick { get; set; }

        public override int ItemCount
        {
            get { return searchResultContentItems != null ? searchResultContentItems.Count : 0;  }
        }

        /// <summary>
        /// Initializes an instance of the <see cref="SearchFragment.SearchViewHolder"/>.
        /// </summary>
        /// <param name="parent">The parent <see cref="ViewGroup"/>.</param>
        /// <param name="viewType">Integer representing the view type.</param>
        /// <returns>Returns an instance of the <see cref="SearchFragment.SearchViewHolder"/>.</returns>
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.search_result_item, parent, false);
            SearchFragment.SearchViewHolder viewHolder = new SearchFragment.SearchViewHolder(view, OnClick, OnMenuClick);

            return viewHolder;
        }

        /// <summary>
        /// Populates information and sets visuals for each displayed item in the <see cref="RecyclerView"/>.
        /// </summary>
        /// <param name="holder">Instance of a <see cref="SearchFragment.SearchViewHolder"/>.</param>
        /// <param name="position">Item position.</param>
        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            SearchFragment.SearchViewHolder viewHolder = holder as SearchFragment.SearchViewHolder;

            var searchResultContentItem = searchResultContentItems[position];

            if (searchResultContentItem.Content is YouTube.Video video)
            {
                // Convert duration from milliseconds to minutes and seconds
                var duration = video.Duration.TotalMilliseconds;
                var minutes = (int)Math.Floor((decimal)(duration / 60000));
                var seconds = (int)(duration % 60000) / 1000;

                // Set text values
                viewHolder.SearchResultContentItemDuration.Text = $"{minutes}:{seconds.ToString().PadLeft(2, '0')}";
                viewHolder.SearchResultContentItemTitle.Text = video.Title;
                viewHolder.SearchResultContentItemChannel.Text = video.Author;

                // Remove the playlist icon for videos
                viewHolder.SearchResultContentItemPlaylistIcon.Visibility = ViewStates.Invisible;

                // Show the options menu for videos
                viewHolder.SearchResultContentItemPopupButton.Visibility = ViewStates.Visible;

                // Populate the thumbnail
                ImageService.Instance.LoadFile(null)
                    .Into(viewHolder.SearchResultContentItemThumbnail);
                ImageService.Instance.LoadUrl(video.Thumbnails.MediumResUrl)
                    .ErrorPlaceholder("Resources/drawable/image_load_error.png")
                    .Into(viewHolder.SearchResultContentItemThumbnail);
            }
            else if (searchResultContentItem.Content is YouTube.Playlist playlist)
            {
                // Hide the playlist track count text view if the track count is -1, indicating the track count was not available, otherwise set the text
                if (playlist.VideoCount == -1)
                    viewHolder.SearchResultContentItemDuration.Visibility = ViewStates.Invisible;
                else
                    viewHolder.SearchResultContentItemDuration.Text = string.Format(Application.Context.GetString(Resource.String.track_count), playlist.VideoCount);

                // Set text values
                viewHolder.SearchResultContentItemTitle.Text = playlist.Title;
                viewHolder.SearchResultContentItemChannel.Text = playlist.Author;

                // Show the playlist icon for playlists
                viewHolder.SearchResultContentItemPlaylistIcon.Visibility = ViewStates.Visible;

                // Hide the options menu for playlists
                viewHolder.SearchResultContentItemPopupButton.Visibility = ViewStates.Invisible;

                // Populate the thumbnail
                ImageService.Instance.LoadFile(null)
                    .Into(viewHolder.SearchResultContentItemThumbnail);
                ImageService.Instance.LoadUrl(playlist.Thumbnails.MediumResUrl)
                    .ErrorPlaceholder("Resources/drawable/image_load_error.png")
                    .Into(viewHolder.SearchResultContentItemThumbnail);
            }

            // Set the search result content item
            viewHolder.SearchResultContentItem = searchResultContentItem;
        }

        /// <summary>
        /// Adds a video search result content item to the end of the results list.
        /// </summary>
        /// <param name="searchResultContentItem">The search result content item to add.</param>
        public void AddSearchResultContentItem(YouTube.ContentItem searchResultContentItem)
        {
            searchResultContentItems.Add(searchResultContentItem);
            NotifyItemInserted(searchResultContentItems.Count - 1);
        }

        /// <summary>
        /// Click event handler to invoke the custom <see cref="ItemClick"/> event handler.
        /// </summary>
        /// <param name="searchResultContentItemVideo">The <see cref="YouTube.ContentItem"/> representing the search result content item that was clicked.</param>
        private void OnClick(YouTube.ContentItem searchResultContentItemVideo)
        {
            ItemClick?.Invoke(this, searchResultContentItemVideo);
        }

        /// <summary>
        /// Click event handler to invoke the custom <see cref="OnMenuClick(IMenuItem, Video)"/> action.
        /// </summary>
        /// <param name="item">The menu item that was selected.</param>
        /// <param name="searchResultContentItemVideo">The <see cref="YouTube.ContentItem"/> of the search result content item where the menu was clicked.</param>
        private void OnMenuClick(IMenuItem item, YouTube.ContentItem searchResultContentItemVideo)
        {
            ItemMenuClick?.Invoke(item, searchResultContentItemVideo);
        }
    }
}