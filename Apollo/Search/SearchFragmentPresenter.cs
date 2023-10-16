// <copyright file="SearchFragmentPresenter.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;

namespace Apollo
{
    /// <summary>
    /// Presenter class for the <see cref="SearchFragment"/>.
    /// </summary>
    internal class SearchFragmentPresenter
    {
        private readonly MainActivity mainActivity;
        private readonly ISearchFragment view;
        private readonly MusicBrowser musicBrowser;
        private bool executingInitialQuery;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchFragmentPresenter"/> class.
        /// </summary>
        /// <param name="mainActivity">A reference to the <see cref="MainActivity"/>.</param>
        /// <param name="view">Instance of a <see cref="SearchFragment"/>.</param>
        /// <param name="musicBrowser">A connected instance of a <see cref="MusicBrowser"/>.</param>
        public SearchFragmentPresenter(MainActivity mainActivity, SearchFragment view, MusicBrowser musicBrowser)
        {
            this.mainActivity = mainActivity;
            this.view = view;
            this.musicBrowser = musicBrowser;
        }

        /// <summary>
        /// Set the initial search option button states based on the user selected search option.
        /// </summary>
        public void InitializeSearchOptions()
        {
            view.UpdateFilterButtonState((int)SettingsManager.SearchSetting);
        }

        /// <summary>
        /// Reinitialize the fragment if it is coming back from a hidden state.
        /// </summary>
        /// <param name="hidden">True if the fragment is being hidden.</param>
        public void InitializeAfterUnhidden(bool hidden)
        {
            // Focus on the background to prevent selection of the search box and have search suggestions obscure the screen.
            if (!hidden)
                view.RequestBackgroundFocus();
        }

        /// <summary>
        /// Checks if the results view has reached the end of the list and calls the appropriate view event method.
        /// </summary>
        /// <param name="recyclerView">The <see cref="RecyclerView"/> displaying search results.</param>
        /// <param name="newState">An integer representing the next state of the <see cref="RecyclerView"/>.</param>
        public void CheckScrollChange(RecyclerView recyclerView, int newState)
        {
            if (!recyclerView.CanScrollVertically(1) && newState == RecyclerView.ScrollStateIdle && !executingInitialQuery)
                view.OnScrollToBottom();
        }

        /// <summary>
        /// Performs a search query for music tracks.
        /// </summary>
        /// <param name="query">Query string to search by.</param>
        public async void Query(string query)
        {
            // Show an error message and stop attempting to query if there is no network available
            if (!NetworkStatus.IsConnected)
            {
                Toast.MakeText(Application.Context, Resource.String.error_no_network, ToastLength.Short).Show();
                return;
            }

            // Get cancellation token
            var cancellationToken = view.CancellationToken;

            // Set the executing initial query flag to prevent the scroll change from requesting additional results in certain scenarios
            executingInitialQuery = true;

            // Set the search box text and focus on the fragment background
            view.UpdateSearchQueryText(query);
            view.RequestBackgroundFocus();

            // Show the buffering icon for initial search results
            view.UpdateInitialSearchResultsBufferingIconVisibility(ViewStates.Visible);

            // Hide the search background icon
            view.UpdateSearchBackgroundVisibility(ViewStates.Gone);

            // Set search task based on setting selection
            var searchSetting = SettingsManager.SearchSetting;
            IAsyncEnumerable<YouTube.ContentItem> searchTask;

            // Set the search task to use for the query
            if (searchSetting == SettingsManager.SearchSettingOptions.General)
                searchTask = YouTube.SearchYouTube(query, null, cancellationToken);
            else if (searchSetting == SettingsManager.SearchSettingOptions.Songs)
                searchTask = YouTube.SearchYouTubeMusic(query, null, YouTube.MusicSearchFilter.Songs, cancellationToken);
            else if (searchSetting == SettingsManager.SearchSettingOptions.Albums)
                searchTask = YouTube.SearchYouTubeMusic(query, null, YouTube.MusicSearchFilter.Albums, cancellationToken);
            else if (searchSetting == SettingsManager.SearchSettingOptions.FeaturedPlaylists)
                searchTask = YouTube.SearchYouTubeMusic(query, null, YouTube.MusicSearchFilter.FeaturedPlaylists, cancellationToken);
            else
                searchTask = YouTube.SearchYouTubeMusic(query, null, YouTube.MusicSearchFilter.CommunityPlaylists, cancellationToken);

            string continuationToken = null;
            var itemsFound = false;
            var searchAttempts = 1;
            var searchSucessful = false;

            while (!searchSucessful && searchAttempts <= 3)
            {
                // Retrieve the first page of search results
                try
                {
                    await foreach (var searchResultContentItem in searchTask)
                    {
                        // Set items found flag
                        if (!itemsFound)
                            itemsFound = true;

                        view.AddSearchResultContentItem(searchResultContentItem);

                        // Save next continuation token if not yet saved or if it has changed
                        if (!string.IsNullOrEmpty(continuationToken) || continuationToken != searchResultContentItem.ContinuationToken)
                            continuationToken = searchResultContentItem.ContinuationToken;
                    }

                    // Set search successful flag
                    searchSucessful = true;
                } // Reattempt search if a null reference is encountered
                catch (NullReferenceException)
                {
                    await Task.Delay(100);
                    searchAttempts++;
                }
                catch
                {
                    Toast.MakeText(Application.Context, Resource.String.error_search_results, ToastLength.Short).Show();
                }
            }

            // Track the error if the search was not successful after several attempts
            if (!searchSucessful)
            {
                Toast.MakeText(Application.Context, Resource.String.error_search_results, ToastLength.Short).Show();
            }

            // Update continuation token to retrieve the next page in the future
            view.ContinuationToken = continuationToken;

            // Hide the buffering icon
            view.UpdateInitialSearchResultsBufferingIconVisibility(ViewStates.Invisible);

            // Show the background again if no results were found in the search
            if (!itemsFound)
                view.UpdateSearchBackgroundVisibility(ViewStates.Visible);

            // Reset the executing initial query flag
            executingInitialQuery = false;

            // If network dropped during searching, show error message
            if (!NetworkStatus.IsConnected)
                Toast.MakeText(Application.Context, Resource.String.error_no_network, ToastLength.Short).Show();
        }

        /// <summary>
        /// Retrieves and displays the next page of query results.
        /// </summary>
        /// <param name="query">Query string to search by.</param>
        /// <param name="continuationToken">A YouTube API token to retrieve the next page of search results.</param>
        public async void QueryAdditionalResultsPage(string query, string continuationToken)
        {
            // Exit if the contiuation token is null
            if (string.IsNullOrEmpty(continuationToken))
                return;

            // Show an error message and stop attempting to load items if there is no network available
            if (!NetworkStatus.IsConnected)
            {
                Toast.MakeText(Application.Context, Resource.String.error_no_network, ToastLength.Short).Show();
                return;
            }

            // Get cancellation token
            var cancellationToken = view.CancellationToken;

            // Show the buffering icon for additional search results
            view.UpdateAdditionalSearchResultsBufferingIconVisibility(ViewStates.Visible);

            // Set search task based on setting selection
            var searchSetting = SettingsManager.SearchSetting;
            IAsyncEnumerable<YouTube.ContentItem> searchTask;

            // Set the search task to use for the query
            if (searchSetting == SettingsManager.SearchSettingOptions.General)
                searchTask = YouTube.SearchYouTube(query, continuationToken, cancellationToken);
            else if (searchSetting == SettingsManager.SearchSettingOptions.Songs)
                searchTask = YouTube.SearchYouTubeMusic(query, continuationToken, YouTube.MusicSearchFilter.Songs, cancellationToken);
            else if (searchSetting == SettingsManager.SearchSettingOptions.Albums)
                searchTask = YouTube.SearchYouTubeMusic(query, continuationToken, YouTube.MusicSearchFilter.Albums, cancellationToken);
            else if (searchSetting == SettingsManager.SearchSettingOptions.FeaturedPlaylists)
                searchTask = YouTube.SearchYouTubeMusic(query, continuationToken, YouTube.MusicSearchFilter.FeaturedPlaylists, cancellationToken);
            else
                searchTask = YouTube.SearchYouTubeMusic(query, continuationToken, YouTube.MusicSearchFilter.CommunityPlaylists, cancellationToken);

            string nextContinuationToken = null;
            var searchAttempts = 1;
            var searchSuccessful = false;

            while (!searchSuccessful && searchAttempts < 3)
            {
                // Retrieve the next page of search results
                try
                {
                    await foreach (var searchResultContentItem in searchTask)
                    {
                        view.AddSearchResultContentItem(searchResultContentItem);

                        // Save next continuation token if not yet saved or if it has changed
                        if (nextContinuationToken == null || nextContinuationToken != searchResultContentItem.ContinuationToken)
                            nextContinuationToken = searchResultContentItem.ContinuationToken;
                    }

                    // Set search successful flag
                    searchSuccessful = true;
                } // Reattempt search if a null reference is encountered
                catch (NullReferenceException)
                {
                    await Task.Delay(100);
                    searchAttempts++;
                }
                catch
                {
                    Toast.MakeText(Application.Context, Resource.String.error_additional_search_results, ToastLength.Short).Show();
                }
            }

            // Update API continuation token to retrieve the next page in the future
            view.ContinuationToken = nextContinuationToken;

            // Hide the buffering icon
            view.UpdateAdditionalSearchResultsBufferingIconVisibility(ViewStates.Gone);

            // If network dropped during loading of additional results, show error message
            if (!NetworkStatus.IsConnected)
                Toast.MakeText(Application.Context, Resource.String.error_no_network, ToastLength.Short).Show();
        }

        /// <summary>
        /// Sets the search option setting.
        /// </summary>
        /// <param name="searchOption">The index of the selected search option.</param>
        /// <param name="query">The current search query text.</param>
        public void SetSearchOption(int searchOption, string query)
        {
            // Exit if currently executing an initial query
            if (executingInitialQuery)
                return;

            // Update the selected button state
            view.UpdateFilterButtonState(searchOption);

            // Save original search setting
            var originalSearchSetting = SettingsManager.SearchSetting;

            // Set the new search option
            switch (searchOption)
            {
                case 0:
                    SettingsManager.SearchSetting = SettingsManager.SearchSettingOptions.General;
                    break;
                case 1:
                    SettingsManager.SearchSetting = SettingsManager.SearchSettingOptions.Songs;
                    break;
                case 2:
                    SettingsManager.SearchSetting = SettingsManager.SearchSettingOptions.Albums;
                    break;
                case 3:
                    SettingsManager.SearchSetting = SettingsManager.SearchSettingOptions.FeaturedPlaylists;
                    break;
                case 4:
                    SettingsManager.SearchSetting = SettingsManager.SearchSettingOptions.CommunityPlaylists;
                    break;
            }

            // If a search query is specified and the search option selected has changed, run a new query with the updated search option
            if (!string.IsNullOrEmpty(query) && originalSearchSetting != SettingsManager.SearchSetting)
            {
                var intent = new Intent(Intent.ActionSearch, null, Application.Context, typeof(MainActivity));
                intent.SetFlags(ActivityFlags.NewTask);
                intent.PutExtra(SearchManager.Query, query);
                Application.Context.StartActivity(intent);
            }
        }

        /// <summary>
        /// Determines which <see cref="MusicBrowser"/> command should be called based on the menu option the user selected.
        /// </summary>
        /// <param name="itemId">The menu item ID that was clicked.</param>
        /// <param name="searchResultContentItem">The <see cref="YouTube.ContentItem"/> where the menu item was clicked.</param>
        public void ItemMenuClicked(int itemId, YouTube.ContentItem searchResultContentItem)
        {
            if (searchResultContentItem.Content is YouTube.Video searchResultContentItemVideo)
            {
                switch (itemId)
                {
                    case Resource.Id.popup_item_queue_to_new_playlist:

                        // Disable shuffle if it is enabled
                        if (musicBrowser.ShuffleMode)
                            musicBrowser.ToggleShuffleMode();

                        // Queue selected item to a new playlist
                        musicBrowser.QueueVideoToNewPlaylist(searchResultContentItemVideo);

                        break;

                    case Resource.Id.popup_item_queue_next:

                        // Queue the video to the next position in the playlist
                        musicBrowser.QueueVideoNext(searchResultContentItemVideo);

                        break;

                    case Resource.Id.popup_item_queue_last:

                        // Queue the video to the last position in the playlist
                        musicBrowser.QueueVideoLast(searchResultContentItemVideo, false);

                        break;

                    case Resource.Id.popup_generate_new_playlist:

                        // Generate a new playlist using the current video as a seed
                        musicBrowser.GenerateNewPlaylist(searchResultContentItemVideo);
                        mainActivity.OpenCurrentQueueFragment();

                        break;

                    case Resource.Id.popup_add_to_saved_playlist:

                        // Display a prompt to add the video to a saved playlist
                        AddToSavedPlaylistManager.DisplayAddToSavedPlaylistPrompt(searchResultContentItemVideo, musicBrowser, mainActivity);

                        break;
                }
            }
        }

        /// <summary>
        /// Add the selected video item to the end of the queue or load the selected playlist.
        /// </summary>
        /// <param name="searchResultContentItem">The <see cref="YouTube.ContentItem"/> that was clicked.</param>
        public void ItemClicked(YouTube.ContentItem searchResultContentItem)
        {
            // Item is video, add it to the end of the current queue
            if (searchResultContentItem.Content is YouTube.Video searchResultContentItemVideo)
            {
                musicBrowser.QueueVideoLast(searchResultContentItemVideo, true);
                Toast.MakeText(Application.Context, Resource.String.track_added_end_of_queue, ToastLength.Short).Show();
            } // Item is playlist, load the playlist and open the current queue
            else if (searchResultContentItem.Content is YouTube.Playlist searchResultContentItemPlaylist)
            {
                musicBrowser.LoadPlaylist(searchResultContentItemPlaylist);
                mainActivity.OpenCurrentQueueFragment();
            }
        }
    }
}