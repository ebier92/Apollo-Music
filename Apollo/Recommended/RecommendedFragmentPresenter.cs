// <copyright file="RecommendedFragmentPresenter.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Android.App;
using Android.Views;
using Android.Widget;

namespace Apollo
{
    /// <summary>
    /// Presenter class for the <see cref="RecommendedFragment"/>.
    /// </summary>
    internal class RecommendedFragmentPresenter
    {
        private readonly MainActivity mainActivity;
        private readonly IRecommendedFragment view;
        private readonly MusicBrowser musicBrowser;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecommendedFragmentPresenter"/> class.
        /// </summary>
        /// <param name="mainActivity">A reference to the <see cref="MainActivity"/>.</param>
        /// <param name="view">Instance of a <see cref="RecommendedFragment"/>.</param>
        /// <param name="musicBrowser">A connected instance of a <see cref="MusicBrowser"/>.</param>
        public RecommendedFragmentPresenter(MainActivity mainActivity, RecommendedFragment view, MusicBrowser musicBrowser)
        {
            this.mainActivity = mainActivity;
            this.view = view;
            this.musicBrowser = musicBrowser;
        }

        /// <summary>
        /// Reinitializes the fragment if it is coming back from a hidden state and reloads recommendations if the queue has changed since the last refresh.
        /// </summary>
        /// <param name="hidden">True if the fragment is being hidden.</param>
        /// <param name="historicalTrackCount">The previously recorded number of historical tracks listened to by the user.</param>
        public void InitializeAfterUnhidden(bool hidden, int historicalTrackCount)
        {
            // Check if the fragment is being unhidden
            if (!hidden)
            {
                // Reload content if queues are not equivalent
                if (!(historicalTrackCount == RecommendationsManager.HistoricalTrackCount))
                    LoadRecommendedContentItems();
            }
        }

        /// <summary>
        /// Restarts the app if the music browser is disconnected.
        /// </summary>
        public void RestartIfMusicBrowserDisconnected()
        {
            if (!musicBrowser.IsConnected)
                mainActivity.RestartApp();
        }

        /// <summary>
        /// Gets the number of tracks previously listened to by the user.
        /// </summary>
        /// <returns>The number of historical tracks.</returns>
        public int GetHistoricalTrackCount()
        {
            return RecommendationsManager.HistoricalTrackCount;
        }

        /// <summary>
        /// Updates the number of tracks previously listened to by the user if the fragment is being unhidden.
        /// </summary>
        /// <param name="hidden">True if the fragment is being hidden.</param>
        /// <param name="historicalTrackCount">The current historical track count.</param>
        /// <returns>The number of historical tracks.</returns>
        public int UpdateHistoricalTrackCount(bool hidden, int historicalTrackCount)
        {
            if (!hidden)
                return RecommendationsManager.HistoricalTrackCount;
            else
                return historicalTrackCount;
        }

        /// <summary>
        /// Loads items for the recommended fragment.
        /// </summary>
        public async void LoadRecommendedContentItems()
        {
            // Show error message if there is no network connectivity
            if (!NetworkStatus.IsConnected)
            {
                view.UpdateContentLoadingIconVisibility(ViewStates.Gone);
                view.UpdateRecyclerViewAndRefreshButtonVisibility(ViewStates.Gone);
                view.UpdateContentLoadErrorElementsVisibility(ViewStates.Visible);
                view.UpdateEmptyPageElementsVisibility(ViewStates.Gone);

                return;
            } // Hide error message if network is connected
            else
            {
                view.UpdateContentLoadErrorElementsVisibility(ViewStates.Gone);
            }

            // Get cancellation token
            var cancellationToken = view.CancellationToken;

            // Show the loading icon while getting the recommended content items, hide the recycler view and empty recommendations elements
            view.UpdateContentLoadingIconVisibility(ViewStates.Visible);
            view.UpdateRecyclerViewAndRefreshButtonVisibility(ViewStates.Gone);
            view.UpdateEmptyPageElementsVisibility(ViewStates.Gone);

            // Clear any existing items
            view.ClearRecommendedContentItems();

            // Get content items for the recommended fragment
            List<YouTube.ContentItem> recommendedContentItems = new List<YouTube.ContentItem>();

            try
            {
                recommendedContentItems = await RecommendationsManager.GetRecommendations(cancellationToken);
            }
            catch
            {
            }

            // Set the empty queue message and exit if a null is returned, indicating no historical track data
            if (recommendedContentItems == null)
            {
                view.UpdateContentLoadingIconVisibility(ViewStates.Gone);
                view.UpdateRecyclerViewAndRefreshButtonVisibility(ViewStates.Gone);
                view.UpdateEmptyPageElementsVisibility(ViewStates.Visible);

                return;
            }

            // Add items to the fragment
            foreach (var recommendedContentItem in recommendedContentItems)
            {
                view.AddRecommendedContentItem(recommendedContentItem);
            }

            // Hide the loading icon and show the recycler view
            view.UpdateContentLoadingIconVisibility(ViewStates.Gone);

            // Show the error message if no recommended content items were retrieved, hide otherwise
            if (recommendedContentItems.Count == 0)
                view.UpdateContentLoadErrorElementsVisibility(ViewStates.Visible);
            else
                view.UpdateRecyclerViewAndRefreshButtonVisibility(ViewStates.Visible);

            // Scroll to the top after refresh
            view.ScrollToPosition(0);
        }

        /// <summary>
        /// Add the selected video item to the end of the queue or load the selected playlist.
        /// </summary>
        /// <param name="recommendedContentItem">The <see cref="YouTube.ContentItem"/> that was clicked.</param>
        public void ItemClicked(YouTube.ContentItem recommendedContentItem)
        {
            // Item is video, add it to the end of the current queue
            if (recommendedContentItem.Content is YouTube.Video recommendedContentItemVideo)
            {
                musicBrowser.QueueVideoLast(recommendedContentItemVideo, true);
                Toast.MakeText(Application.Context, Resource.String.track_added_end_of_queue, ToastLength.Short).Show();
            }
        }

        /// <summary>
        /// Generates a recommended or historical playlist when the user clicks the buttons next to the related section header.
        /// </summary>
        /// <param name="sectionHeaderText">The section header text to determine which type of playlist to generate.</param>
        public void ButtonClicked(string sectionHeaderText)
        {
            // Select the appropriate type of playlist to generate based on the section where the button was clicked
            if (sectionHeaderText == Application.Context.GetString(Resource.String.recommended_section_header))
                musicBrowser.GenerateRecommendedPlaylist();
            else if (sectionHeaderText == Application.Context.GetString(Resource.String.listen_again_section_header))
                musicBrowser.GenerateHistoricalPlaylist();

            // Open the current queue fragment to display the playlist
            mainActivity.OpenCurrentQueueFragment();
        }

        /// <summary>
        /// Determines which <see cref="MusicBrowser"/> command should be called based on the menu option the user selected.
        /// </summary>
        /// <param name="itemId">The menu item ID that was clicked.</param>
        /// <param name="recommendedContentItem">The <see cref="YouTube.ContentItem"/> where the menu item was clicked.</param>
        public void ItemMenuClicked(int itemId, YouTube.ContentItem recommendedContentItem)
        {
            if (recommendedContentItem.Content is YouTube.Video recommendedContentItemVideo)
            {
                switch (itemId)
                {
                    case Resource.Id.popup_item_queue_to_new_playlist:

                        // Disable shuffle if it is enabled
                        if (musicBrowser.ShuffleMode)
                            musicBrowser.ToggleShuffleMode();

                        // Queue selected item to a new playlist
                        musicBrowser.QueueVideoToNewPlaylist(recommendedContentItemVideo);

                        break;

                    case Resource.Id.popup_item_queue_next:

                        // Queue the video to the next position in the playlist
                        musicBrowser.QueueVideoNext(recommendedContentItemVideo);

                        break;

                    case Resource.Id.popup_item_queue_last:

                        // Queue the video to the last position in the playlist
                        musicBrowser.QueueVideoLast(recommendedContentItemVideo, false);

                        break;

                    case Resource.Id.popup_generate_new_playlist:

                        // Generate a new playlist using the current video as a seed
                        musicBrowser.GenerateNewPlaylist(recommendedContentItemVideo);
                        mainActivity.OpenCurrentQueueFragment();

                        break;

                    case Resource.Id.popup_add_to_saved_playlist:

                        // Display a prompt to add the video to a saved playlist
                        AddToSavedPlaylistManager.DisplayAddToSavedPlaylistPrompt(recommendedContentItemVideo, musicBrowser, mainActivity);

                        break;
                }
            }
        }
    }
}