// <copyright file="HomeAdapter.cs" company="Erik Bierbrauer">
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
    /// Adapter class for the home fragment <see cref="RecyclerView"/>.
    /// </summary>
    internal class HomeAdapter : RecyclerView.Adapter
    {
        private readonly List<YouTube.ContentItem> homeContentItems;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeAdapter"/> class.
        /// </summary>
        public HomeAdapter()
        {
            homeContentItems = new List<YouTube.ContentItem>();
        }

        public EventHandler<YouTube.ContentItem> ItemClick { get; set; }

        public Action<IMenuItem, YouTube.ContentItem> ItemMenuClick { get; set; }

        public override int ItemCount
        {
            get { return homeContentItems != null ? homeContentItems.Count : 0; }
        }

        /// <summary>
        /// Initializes an instance of the <see cref="HomeFragment.HomeViewHolder"/>.
        /// </summary>
        /// <param name="parent">The parent <see cref="ViewGroup"/>.</param>
        /// <param name="viewType">Integer representing the view type.</param>
        /// <returns>Returns an instance of the <see cref="HomeFragment.HomeViewHolder"/>.</returns>
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.home_item, parent, false);
            HomeFragment.HomeViewHolder viewHolder = new HomeFragment.HomeViewHolder(view, OnClick, OnMenuClick);

            return viewHolder;
        }

        /// <summary>
        /// Populates information and sets visuals for each displayed item in the <see cref="RecyclerView"/>.
        /// </summary>
        /// <param name="holder">Instance of a <see cref="HomeFragment.HomeViewHolder"/>.</param>
        /// <param name="position">Item position.</param>
        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            HomeFragment.HomeViewHolder viewHolder = holder as HomeFragment.HomeViewHolder;

            var homeContentItem = homeContentItems[position];

            // Initialize all as hidden
            viewHolder.HomeContentItemSectionHeaderContainer.Visibility = ViewStates.Gone;
            viewHolder.HomeContentItemContentContainer.Visibility = ViewStates.Gone;

            if (homeContentItem.Content == null && homeContentItem.Data != null)
            {
                // Set the section header text
                viewHolder.HomeContentItemSectionHeader.Text = homeContentItem.Data["sectionHeader"];

                // Show section header elements, hide content elements
                viewHolder.HomeContentItemSectionHeaderContainer.Visibility = ViewStates.Visible;
                viewHolder.HomeContentItemContentContainer.Visibility = ViewStates.Gone;
            }
            else if (homeContentItem.Content is YouTube.Video video)
            {
                // Convert duration from milliseconds to minutes and seconds
                var duration = video.Duration.TotalMilliseconds;
                var minutes = (int)Math.Floor((decimal)(duration / 60000));
                var seconds = (int)(duration % 60000) / 1000;

                // Set text values
                viewHolder.HomeContentItemDuration.Text = $"{minutes}:{seconds.ToString().PadLeft(2, '0')}";
                viewHolder.HomeContentItemTitle.Text = video.Title;
                viewHolder.HomeContentItemDescription.Text = video.Author;

                // Show content elements, hide section header elements
                viewHolder.HomeContentItemSectionHeaderContainer.Visibility = ViewStates.Gone;
                viewHolder.HomeContentItemContentContainer.Visibility = ViewStates.Visible;

                // Remove the playlist icon for videos
                viewHolder.HomeContentItemPlaylistIcon.Visibility = ViewStates.Invisible;

                // Show the options menu for videos
                viewHolder.HomeContentItemPopupButton.Visibility = ViewStates.Visible;

                // Populate the thumbnail
                ImageService.Instance.LoadFile(null)
                    .Into(viewHolder.HomeContentItemThumbnail);
                ImageService.Instance.LoadUrl(video.Thumbnails.MediumResUrl)
                    .ErrorPlaceholder("Resources/drawable/image_load_error.png")
                    .Into(viewHolder.HomeContentItemThumbnail);
            }
            else if (homeContentItem.Content is YouTube.Playlist playlist)
            {
                // Extract video count and playlist description
                var videoCount = playlist.VideoCount;
                var description = playlist.Description;

                // Set text values
                viewHolder.HomeContentItemDuration.Text = string.Format(Application.Context.Resources.GetString(Resource.String.track_count), videoCount);
                viewHolder.HomeContentItemTitle.Text = playlist.Title;
                viewHolder.HomeContentItemDescription.Text = description;

                // Show content elements, hide section header elements
                viewHolder.HomeContentItemSectionHeaderContainer.Visibility = ViewStates.Gone;
                viewHolder.HomeContentItemContentContainer.Visibility = ViewStates.Visible;

                // Show the playlist icon for playlists
                viewHolder.HomeContentItemPlaylistIcon.Visibility = ViewStates.Visible;

                // Hide the options menu for playlists
                viewHolder.HomeContentItemPopupButton.Visibility = ViewStates.Invisible;

                // Populate the thumbnail
                ImageService.Instance.LoadFile(null)
                    .Into(viewHolder.HomeContentItemThumbnail);
                ImageService.Instance.LoadUrl(playlist.Thumbnails.MediumResUrl)
                    .ErrorPlaceholder("Resources/drawable/image_load_error.png")
                    .Into(viewHolder.HomeContentItemThumbnail);
            }

            // Set the search result item
            viewHolder.HomeContentItem = homeContentItem;
        }

        /// <summary>
        /// Adds a home content item to the end of the item list.
        /// </summary>
        /// <param name="homeContentItem">The home content item to add.</param>
        public void AddHomeContentItem(YouTube.ContentItem homeContentItem)
        {
            homeContentItems.Add(homeContentItem);
            NotifyItemInserted(homeContentItems.Count - 1);
        }

        /// <summary>
        /// Click event handler to invoke the custom <see cref="ItemClick"/> event handler.
        /// </summary>
        /// <param name="homeContentItemVideo">The <see cref="YouTube.ContentItem"/> representing the home content item that was clicked.</param>
        private void OnClick(YouTube.ContentItem homeContentItemVideo)
        {
            ItemClick?.Invoke(this, homeContentItemVideo);
        }

        /// <summary>
        /// Click event handler to invoke the custom <see cref="OnMenuClick(IMenuItem, Video)"/> action.
        /// </summary>
        /// <param name="item">The menu item that was selected.</param>
        /// <param name="homeContentItemVideo">The <see cref="YouTube.ContentItem"/> of the item where the menu was clicked.</param>
        private void OnMenuClick(IMenuItem item, YouTube.ContentItem homeContentItemVideo)
        {
            ItemMenuClick?.Invoke(item, homeContentItemVideo);
        }
    }
}