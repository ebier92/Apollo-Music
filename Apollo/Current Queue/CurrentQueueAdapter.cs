// <copyright file="CurrentQueueAdapter.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Android.Media.Session;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using FFImageLoading;
using Java.Lang;

namespace Apollo
{
    /// <summary>
    /// Adapter class for the current queue <see cref="RecyclerView"/>.
    /// </summary>
    internal class CurrentQueueAdapter : RecyclerView.Adapter, IFilterable, CurrentQueueItemTouch.IItemTouchAdapter
    {
        private readonly MusicBrowser musicBrowser;
        private readonly MainActivity mainActivity;
        private readonly List<IFilterEnabledObserver> filterEnabledObservers;
        private List<MediaSession.QueueItem> queueItems;
        private List<IFilterEnabledObserver> filterEnabledObserversToRemove;
        private bool isNotifying;

        /// <summary>
        /// Initializes a new instance of the <see cref="CurrentQueueAdapter"/> class.
        /// </summary>
        /// <param name="queueItems">List of <see cref="MediaSession.QueueItem"/>.</param>
        /// <param name="musicBrowser">A connected instance of the <see cref="MusicBrowser"/>.</param>
        /// <param name="mainActivity">Reference to the app's <see cref="MainActivity"/>.</param>
        public CurrentQueueAdapter(IList<MediaSession.QueueItem> queueItems, MusicBrowser musicBrowser, MainActivity mainActivity)
        {
            this.musicBrowser = musicBrowser;
            this.mainActivity = mainActivity;
            this.queueItems = queueItems != null ? new List<MediaSession.QueueItem>(queueItems) : new List<MediaSession.QueueItem>();
            filterEnabledObservers = new List<IFilterEnabledObserver>();
            filterEnabledObserversToRemove = new List<IFilterEnabledObserver>();
            Filter = new CurrentQueueFilter(this);
        }

        /// <summary>
        /// Interface to notify an observer of a change in the filter enabled state.
        /// </summary>
        public interface IFilterEnabledObserver
        {
            void NotifyFilterEnabledChange();
        }

        public EventHandler<string> ItemClick { get; set; }

        public Filter Filter { get; private set; }

        public bool FilterEnabled { get; private set; }

        public string ActiveItemMediaId { get; set; }

        public override int ItemCount
        {
            get { return queueItems != null ? queueItems.Count : 0; }
        }

        /// <summary>
        /// Initializes an instance of the <see cref="CurrentQueueFragment.CurrentQueueViewHolder"/>.
        /// </summary>
        /// <param name="parent">The parent <see cref="ViewGroup"/>.</param>
        /// <param name="viewType">Integer representing the view type.</param>
        /// <returns>Returns an instance of the <see cref="CurrentQueueFragment.CurrentQueueViewHolder"/>.</returns>
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.queue_item, parent, false);
            CurrentQueueFragment.CurrentQueueViewHolder viewHolder = new CurrentQueueFragment.CurrentQueueViewHolder(view, musicBrowser, mainActivity, OnClick);

            return viewHolder;
        }

        /// <summary>
        /// Populates information and sets visuals for each displayed item in the <see cref="RecyclerView"/>.
        /// </summary>
        /// <param name="holder">Instance of a <see cref="CurrentQueueFragment.CurrentQueueViewHolder"/>.</param>
        /// <param name="position">Item position.</param>
        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            CurrentQueueFragment.CurrentQueueViewHolder viewHolder = holder as CurrentQueueFragment.CurrentQueueViewHolder;

            // Retrieve the duration from the extras bundle and convert from milliseconds to minutes and seconds
            var description = queueItems[position].Description;
            var duration = description.Extras.GetLong("Duration", 0);
            var minutes = (int)System.Math.Floor((decimal)(duration / 60000));
            var seconds = (int)(duration % 60000) / 1000;

            // Set text values
            viewHolder.QueueItemDuration.Text = $"{minutes}:{seconds.ToString().PadLeft(2, '0')}";
            viewHolder.QueueItemTitle.Text = description.Title;
            viewHolder.QueueItemChannel.Text = description.Subtitle;

            // Populate the thumbnail
            ImageService.Instance.LoadFile(null)
                .Into(viewHolder.QueueItemThumbnail);
            ImageService.Instance.LoadUrl(description.IconUri.ToString())
                .ErrorPlaceholder("Resources/drawable/image_load_error.png")
                .Into(viewHolder.QueueItemThumbnail);

            // Set the item media ID
            viewHolder.MediaId = description.MediaId;

            // Set the background to a lighter color if the queue item is currently playing
            var themeManager = mainActivity.ThemeManager;
            var backgroundColor = viewHolder.MediaId == ActiveItemMediaId ? themeManager.ColorForeground : themeManager.ColorBackground;
            viewHolder.QueueItemContainer.SetBackgroundColor(backgroundColor);
        }

        /// <summary>
        /// Called when an item is dragged far enough to trigger a move event.
        /// </summary>
        /// <param name="fromPosition">The integer position an item was moved from.</param>
        /// <param name="toPosition">The integer position an item was moved to.</param>
        /// <returns>True if the item was moved to a new position.</returns>
        public bool OnItemMove(int fromPosition, int toPosition)
        {
            // Save both items in variables for the swap
            var firstItem = queueItems[fromPosition];
            var secondItem = queueItems[toPosition];

            // Swap the items in the queue items list
            queueItems[fromPosition] = secondItem;
            queueItems[toPosition] = firstItem;

            // Notify the recycler view that an item move has occured.
            NotifyItemMoved(fromPosition, toPosition);

            return true;
        }

        /// <summary>
        /// Called when an item is swiped and dismissed.
        /// </summary>
        /// <param name="position">The integer position an item was removed from.</param>
        public void OnItemDismiss(int position)
        {
            // Delete the displayed queue item from the list by index
            queueItems.RemoveAt(position);

            // Notify the recycler view that an item has been dismissed.
            NotifyItemRemoved(position);

            // Remove the queue item from the music service's music queue
            musicBrowser.RemoveQueueItem(position);
        }

        /// <summary>
        /// Updates the queue items any time the queue changes.
        /// </summary>
        /// <param name="queueItems">List of <see cref="MediaSession.QueueItem"/>.</param>
        public void UpdateQueueItems(IList<MediaSession.QueueItem> queueItems)
        {
            this.queueItems = new List<MediaSession.QueueItem>(queueItems);
            (Filter as CurrentQueueFilter).UpdateQueueItems(queueItems);
            NotifyDataSetChanged();
        }

        /// <summary>
        /// Attaches an <see cref="IFilterEnabledObserver"/> for notifications.
        /// </summary>
        /// <param name="filterEnabledChangeObserver">The <see cref="IFilterEnabledObserver"/> to attach.</param>
        public void Attach(IFilterEnabledObserver filterEnabledChangeObserver)
        {
            filterEnabledObservers.Add(filterEnabledChangeObserver);
        }

        /// <summary>
        /// Detaches an <see cref="IFilterEnabledObserver"/> for notifications.
        /// </summary>
        /// <param name="filterEnabledChangeObserver">The <see cref="IFilterEnabledObserver"/> to detach.</param>
        public void Detach(IFilterEnabledObserver filterEnabledChangeObserver)
        {
            if (!isNotifying)
                filterEnabledObservers.Remove(filterEnabledChangeObserver);
            else
                filterEnabledObserversToRemove.Add(filterEnabledChangeObserver);
        }

        /// <summary>
        /// Click event handler to invoke the custom <see cref="OnItemClick(object, string)"/> method.
        /// </summary>
        /// <param name="mediaId">The media ID of the queue item that was clicked.</param>
        private void OnClick(string mediaId)
        {
            ItemClick?.Invoke(this, mediaId);
        }

        /// <summary>
        /// Notifies <see cref="IFilterEnabledObserver"/>s when a change to <see cref="FilterEnabled"/> occurs.
        /// </summary>
        private void NotifyFilterEnabledChange()
        {
            // Set flag that notifications are happening
            isNotifying = true;

            // Notify each observer of a filter enabled change
            foreach (var filterEnabledObserver in filterEnabledObservers)
            {
                filterEnabledObserver.NotifyFilterEnabledChange();
            }

            // Disable flag that notifications are happening
            isNotifying = false;

            // Remove all observers that requested a detach during the notfications
            foreach (var filterEnabledObserver in filterEnabledObserversToRemove)
            {
                filterEnabledObservers.Remove(filterEnabledObserver);
            }

            // Reset the filter enabled observers to remove list
            filterEnabledObserversToRemove = new List<IFilterEnabledObserver>();
        }

        /// <summary>
        /// Filter class for the <see cref="CurrentQueueAdapter"/>.
        /// </summary>
        private class CurrentQueueFilter : Filter
        {
            private readonly CurrentQueueAdapter adapter;
            private List<MediaSession.QueueItem> unfilteredQueueItems;

            public CurrentQueueFilter(CurrentQueueAdapter currentQueueAdapter)
            {
                adapter = currentQueueAdapter;
                unfilteredQueueItems = adapter.queueItems;
            }

            /// <summary>
            /// Updates the queue items in the filter.
            /// </summary>
            /// <param name="queueItems">An IList of queue items to be filtered.</param>
            public void UpdateQueueItems(IList<MediaSession.QueueItem> queueItems)
            {
                unfilteredQueueItems = queueItems.ToList();
            }

            /// <summary>
            /// Applies a filter constraint to the queue items.
            /// </summary>
            /// <param name="constraint">Constraint to filter queue items.</param>
            /// <returns>Results of the filter.</returns>
            protected override FilterResults PerformFiltering(ICharSequence constraint)
            {
                var filterResults = new FilterResults();
                var results = new List<MediaSession.QueueItem>();

                // Perform filtering if filter characters are present and the queue contains items
                if (constraint != null && constraint.ToString() != null && unfilteredQueueItems != null && unfilteredQueueItems.Any())
                {
                    var description = unfilteredQueueItems[0].Description;

                    // Create matching filter results
                    results.AddRange(
                        unfilteredQueueItems.Where(
                            item => item.Description.Title.ToLower().Contains(constraint.ToString()) || item.Description.Subtitle.ToLower().Contains(constraint.ToString())));
                }

                // Save the original filter enabled value to compare for changes
                var initialFilterEnabledValue = adapter.FilterEnabled;

                // Set the filter enabled flag based on the length of the constraint
                if (constraint.Length() > 0)
                    adapter.FilterEnabled = true;
                else
                    adapter.FilterEnabled = false;

                // Call the notification method if the filter enabled state has changed
                if (initialFilterEnabledValue != adapter.FilterEnabled)
                    adapter.NotifyFilterEnabledChange();

                // Create filter results
                filterResults.Values = FromArray(results.Select(r => r.ToJavaObject()).ToArray());
                filterResults.Count = results.Count;

                constraint.Dispose();

                return filterResults;
            }

            /// <summary>
            /// Publish filtered items to the queue fragment.
            /// </summary>
            /// <param name="constraint">Constain to filter queue items.</param>
            /// <param name="results">Results of the filter.</param>
            protected override void PublishResults(ICharSequence constraint, FilterResults results)
            {
                // Set filter results to queue and update the data set
                adapter.queueItems = results.Values.ToArray<Java.Lang.Object>()
                    .Select(r => r.ToNetObject<MediaSession.QueueItem>()).ToList();
                adapter.NotifyDataSetChanged();

                constraint.Dispose();
                results.Dispose();
            }
        }
    }
}