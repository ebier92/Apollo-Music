// <copyright file="RecommendedAdapter.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using FFImageLoading;

namespace Apollo
{
    internal class RecommendedAdapter : RecyclerView.Adapter, RecommendedItemTouch.IItemTouchAdapter
    {
        private readonly List<YouTube.ContentItem> recommendedContentItems;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecommendedAdapter"/> class.
        /// </summary>
        public RecommendedAdapter()
        {
            recommendedContentItems = new List<YouTube.ContentItem>();
        }

        public EventHandler<YouTube.ContentItem> ItemClick { get; set; }

        public EventHandler<string> ButtonClick { get; set; }

        public Action<IMenuItem, YouTube.ContentItem> ItemMenuClick { get; set; }

        public override int ItemCount
        {
            get { return recommendedContentItems != null ? recommendedContentItems.Count : 0; }
        }

        /// <summary>
        /// Initializes an instance of the <see cref="RecommendedFragment.RecommendedViewHolder"/>.
        /// </summary>
        /// <param name="parent">The parent <see cref="ViewGroup"/>.</param>
        /// <param name="viewType">Integer representing the view type.</param>
        /// <returns>Returns an instance of the <see cref="RecommendedFragment.RecommendedViewHolder"/>.</returns>
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.recommended_item, parent, false);
            RecommendedFragment.RecommendedViewHolder viewHolder = new RecommendedFragment.RecommendedViewHolder(view, OnClick, OnButtonClick, OnMenuClick);

            return viewHolder;
        }

        /// <summary>
        /// Populates information and sets visuals for each displayed item in the <see cref="RecyclerView"/>.
        /// </summary>
        /// <param name="holder">Instance of a <see cref="RecommendedFragment.RecommendedViewHolder"/>.</param>
        /// <param name="position">Item position.</param>
        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            RecommendedFragment.RecommendedViewHolder viewHolder = holder as RecommendedFragment.RecommendedViewHolder;

            var recommendedContentItem = recommendedContentItems[position];

            // Initialize all as hidden
            viewHolder.RecommendedContentItemSectionHeaderContainer.Visibility = ViewStates.Gone;
            viewHolder.RecommendedContentItemContentContainer.Visibility = ViewStates.Gone;

            if (recommendedContentItem.Content == null && recommendedContentItem.Data != null)
            {
                // Set the section header text
                viewHolder.RecommendedContentItemSectionHeader.Text = recommendedContentItem.Data["sectionHeader"];

                // Show section header elements, hide content elements
                viewHolder.RecommendedContentItemSectionHeaderContainer.Visibility = ViewStates.Visible;
                viewHolder.RecommendedContentItemContentContainer.Visibility = ViewStates.Gone;
            }
            else if (recommendedContentItem.Content is YouTube.Video video)
            {
                // Convert duration from milliseconds to minutes and seconds
                var duration = video.Duration.TotalMilliseconds;
                var minutes = (int)Math.Floor((decimal)(duration / 60000));
                var seconds = (int)(duration % 60000) / 1000;

                // Set text values
                viewHolder.RecommendedContentItemDuration.Text = $"{minutes}:{seconds.ToString().PadLeft(2, '0')}";
                viewHolder.RecommendedContentItemTitle.Text = video.Title;
                viewHolder.RecommendedContentItemDescription.Text = video.Author;

                // Show content elements, hide section header elements
                viewHolder.RecommendedContentItemSectionHeaderContainer.Visibility = ViewStates.Gone;
                viewHolder.RecommendedContentItemContentContainer.Visibility = ViewStates.Visible;

                // Remove the playlist icon for videos
                viewHolder.RecommendedContentItemPlaylistIcon.Visibility = ViewStates.Invisible;

                // Show the options menu for videos
                viewHolder.RecommendedContentItemPopupButton.Visibility = ViewStates.Visible;

                // Populate the thumbnail
                ImageService.Instance.LoadFile(null)
                    .Into(viewHolder.RecommendedContentItemThumbnail);
                ImageService.Instance.LoadUrl(video.Thumbnails.MediumResUrl)
                    .ErrorPlaceholder("Resources/drawable/image_load_error.png")
                    .Into(viewHolder.RecommendedContentItemThumbnail);
            }

            // Set the search result item
            viewHolder.RecommendedContentItem = recommendedContentItem;
        }

        /// <summary>
        /// Called when an item is swiped and dismissed.
        /// </summary>
        /// <param name="position">The integer position an item was removed from.</param>
        public void OnItemDismiss(int position)
        {
            // Remove the track from historical recommendations data
            var url = ((YouTube.Video)recommendedContentItems[position].Content).Url;
            RecommendationsManager.RemoveTrack(url);

            // Remove the swiped item
            recommendedContentItems.RemoveAt(position);

            // Notify the recycler view that an item has been dismissed.
            NotifyItemRemoved(position);
        }

        /// <summary>
        /// Adds a recommended content item to the end of the item list.
        /// </summary>
        /// <param name="recommendedContentItem">The recommended content item to add.</param>
        public void AddRecommendedContentItem(YouTube.ContentItem recommendedContentItem)
        {
            recommendedContentItems.Add(recommendedContentItem);
            NotifyItemInserted(recommendedContentItems.Count - 1);
        }

        /// <summary>
        /// Clears all recommended content items from the adapter.
        /// </summary>
        public void ClearRecommendedContentItems()
        {
            recommendedContentItems.Clear();
            NotifyDataSetChanged();
        }

        /// <summary>
        /// Click event handler to invoke the custom <see cref="ItemClick"/> event.
        /// </summary>
        /// <param name="recommendedContentItemVideo">The <see cref="YouTube.ContentItem"/> representing the recommended content item that was clicked.</param>
        private void OnClick(YouTube.ContentItem recommendedContentItemVideo)
        {
            ItemClick?.Invoke(this, recommendedContentItemVideo);
        }

        /// <summary>
        /// Button click event handler to invoke the custom <see cref="ButtonClick"/> event.
        /// </summary>
        /// <param name="sectionHeaderText">The section hander text of the section where the button was clicked.</param>
        private void OnButtonClick(string sectionHeaderText)
        {
            ButtonClick?.Invoke(this, sectionHeaderText);
        }

        /// <summary>
        /// Click event handler to invoke the custom <see cref="OnMenuClick(IMenuItem, Video)"/> action.
        /// </summary>
        /// <param name="item">The menu item that was selected.</param>
        /// <param name="recommendedContentItemVideo">The <see cref="YouTube.ContentItem"/> of the item where the menu was clicked.</param>
        private void OnMenuClick(IMenuItem item, YouTube.ContentItem recommendedContentItemVideo)
        {
            ItemMenuClick?.Invoke(item, recommendedContentItemVideo);
        }
    }
}