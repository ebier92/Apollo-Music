// <copyright file="HomeFragmentPresenter.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;

namespace Apollo
{
    /// <summary>
    /// Presenter class for the <see cref="HomeFragment"/>.
    /// </summary>
    internal class HomeFragmentPresenter
    {
        private readonly MainActivity mainActivity;
        private readonly IHomeFragment view;
        private readonly MusicBrowser musicBrowser;
        private readonly string startSection;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeFragmentPresenter"/> class.
        /// </summary>
        /// <param name="mainActivity">A reference to the <see cref="MainActivity"/>.</param>
        /// <param name="view">Instance of a <see cref="HomeFragment"/>.</param>
        /// <param name="musicBrowser">A connected instance of a <see cref="MusicBrowser"/>.</param>
        public HomeFragmentPresenter(MainActivity mainActivity, HomeFragment view, MusicBrowser musicBrowser)
        {
            this.mainActivity = mainActivity;
            this.view = view;
            this.musicBrowser = musicBrowser;
            startSection = "Today's Biggest Hits";
        }

        /// <summary>
        /// Loads items for the home fragment.
        /// </summary>
        public async void LoadHomeContentItems()
        {
            // Show error message and button to retry home content item loading if there is no network connectivity
            if (!NetworkStatus.IsConnected)
            {
                view.UpdateRetryHomeContentItemsLoadItemsVisibility(ViewStates.Visible);
                return;
            } // Hide error message and button to retry if network is connected
            else
            {
                view.UpdateRetryHomeContentItemsLoadItemsVisibility(ViewStates.Gone);
            }

            // Get cancellation token
            var cancellationToken = view.CancellationToken;

            // Show the loading icon while getting the home content items
            view.UpdateHomeContentItemsLoadingIconVisibility(ViewStates.Visible);

            // Get content items for the home fragment
            List<YouTube.ContentItem> homeContentItems = new List<YouTube.ContentItem>();

            try
            {
                homeContentItems = await YouTube.GetHomePageContentItems(cancellationToken);
            }
            catch
            {
            }

            // Attempt to only add tracks that occur after the starting section to filter out content not focused on popular music or best of genre
            var startSectionFound = false;

            foreach (var homeContentItem in homeContentItems)
            {
                // Set flag when start section is found and start adding content items
                if (homeContentItem.Data != null && homeContentItem.Data["sectionHeader"] == startSection)
                    startSectionFound = true;

                if (startSectionFound)
                    view.AddHomeContentItem(homeContentItem);
            }

            // If the start section was not found (possibly due to a change on YouTube's part) just add all home content items to avoid an empty fragment
            if (!startSectionFound)
            {
                foreach (var homeContentItem in homeContentItems)
                {
                    view.AddHomeContentItem(homeContentItem);
                }
            }

            // Hide the loading icon
            view.UpdateHomeContentItemsLoadingIconVisibility(ViewStates.Invisible);

            // Show the error message and retry button if no home content items were retrieved, hide them otherwise
            if (homeContentItems.Count == 0)
                view.UpdateRetryHomeContentItemsLoadItemsVisibility(ViewStates.Visible);
            else
                view.UpdateRetryHomeContentItemsLoadItemsVisibility(ViewStates.Gone);
        }

        /// <summary>
        /// Displays the app settings prompt.
        /// </summary>
        public void DisplaySettingsPrompt()
        {
            view.DisplaySettingsPrompt();
        }

        /// <summary>
        /// Prompts the user to confirm they want to clear search history.
        /// </summary>
        public void PromptClearSearchHistory()
        {
            static void ClearSearchHistory()
            {
                SettingsManager.ClearSearchHistory();
            }

            view.DisplayConfirmationPrompt(Resource.String.confirm_clear_search_history, ClearSearchHistory);
        }

        /// <summary>
        /// Prompts the user to confirm they want to clear recommendation data.
        /// </summary>
        public void PromptClearRecommendationData()
        {
            static void ClearRecommendationData()
            {
                SettingsManager.ClearRecommendations();
            }

            view.DisplayConfirmationPrompt(Resource.String.confirm_clear_recommendation_data, ClearRecommendationData);
        }

        /// <summary>
        /// Prompts the user to confirm that want to clear all saved playlists.
        /// </summary>
        public void PromptClearPlaylists()
        {
            static void ClearPlaylists()
            {
                SettingsManager.ClearPlaylists();
            }

            view.DisplayConfirmationPrompt(Resource.String.confirm_clear_playlists, ClearPlaylists);
        }

        /// <summary>
        /// Launches a file picker dialog for the user to select a location on their device to export app data.
        /// </summary>
        public void PromptExportAppData()
        {
            // Create intent to launch file picker
            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/json");
            intent.PutExtra(Intent.ExtraTitle, SettingsManager.DefaultAppDataFileName);

            // Start picker from intent
            mainActivity.StartActivityForResult(intent, SettingsManager.ExportAppDataFileRequestCode);
        }

        public void PromptImportAppData()
        {
            // Create intent to launch file picker
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/json");

            // Start picker from intent
            mainActivity.StartActivityForResult(intent, SettingsManager.ImportAppDataFileRequestCode);
        }

        /// <summary>
        /// Sets the stream quality setting.
        /// </summary>
        /// <param name="streamQualityOption">The index of the selected stream quality setting.</param>
        public void SetStreamQuality(int streamQualityOption)
        {
            // Map selected option with setting value
            var streamQualitySelectedSetting = streamQualityOption switch
            {
                0 => SettingsManager.StreamQualitySettingOptions.Low,
                1 => SettingsManager.StreamQualitySettingOptions.Medium,
                2 => SettingsManager.StreamQualitySettingOptions.High,
                _ => SettingsManager.StreamQualitySettingOptions.Low,
            };

            // Change the setting if it is different
            if (streamQualitySelectedSetting != SettingsManager.StreamQualitySetting)
                SettingsManager.StreamQualitySetting = streamQualitySelectedSetting;
        }

        /// <summary>
        /// Sets the playlist source settings.
        /// </summary>
        /// <param name="playlistSourceOption">The index of the selected playlist source setting.</param>
        public void SetPlaylistSource(int playlistSourceOption)
        {
            // Map selected option with setting value
            var playlistSourceSelectedSetting = playlistSourceOption switch
            {
                0 => SettingsManager.PlaylistSourceSettingOptions.YouTubeMusic,
                1 => SettingsManager.PlaylistSourceSettingOptions.YouTube,
                _ => SettingsManager.PlaylistSourceSettingOptions.YouTubeMusic,
            };

            // Change the setting if it is different
            if (playlistSourceSelectedSetting != SettingsManager.PlaylistSourceSetting)
                SettingsManager.PlaylistSourceSetting = playlistSourceSelectedSetting;
        }

        /// <summary>
        /// Determines which <see cref="MusicBrowser"/> command should be called based on the menu option the user selected.
        /// </summary>
        /// <param name="itemId">The menu item ID that was clicked.</param>
        /// <param name="homeContentItem">The <see cref="YouTube.ContentItem"/> where the menu item was clicked.</param>
        public void ItemMenuClicked(int itemId, YouTube.ContentItem homeContentItem)
        {
            if (homeContentItem.Content is YouTube.Video homeContentItemVideo)
            {
                switch (itemId)
                {
                    case Resource.Id.popup_item_queue_to_new_playlist:

                        // Disable shuffle if it is enabled
                        if (musicBrowser.ShuffleMode)
                            musicBrowser.ToggleShuffleMode();

                        // Queue selected item to a new playlist
                        musicBrowser.QueueVideoToNewPlaylist(homeContentItemVideo);

                        break;

                    case Resource.Id.popup_item_queue_next:

                        // Queue the video to the next position in the playlist
                        musicBrowser.QueueVideoNext(homeContentItemVideo);

                        break;

                    case Resource.Id.popup_item_queue_last:

                        // Queue the video to the last position in the playlist
                        musicBrowser.QueueVideoLast(homeContentItemVideo, false);

                        break;

                    case Resource.Id.popup_generate_new_playlist:

                        // Generate a new playlist using the current video as a seed
                        musicBrowser.GenerateNewPlaylist(homeContentItemVideo);
                        mainActivity.OpenCurrentQueueFragment();

                        break;

                    case Resource.Id.popup_add_to_saved_playlist:

                        // Display a prompt to add the video to a saved playlist
                        AddToSavedPlaylistManager.DisplayAddToSavedPlaylistPrompt(homeContentItemVideo, musicBrowser, mainActivity);

                        break;
                }
            }
        }

        /// <summary>
        /// Add the selected video item to the end of the queue or load the selected playlist.
        /// </summary>
        /// <param name="homeContentItem">The <see cref="YouTube.ContentItem"/> that was clicked.</param>
        public void ItemClicked(YouTube.ContentItem homeContentItem)
        {
            // Item is video, add it to the end of the current queue
            if (homeContentItem.Content is YouTube.Video homeContentItemVideo)
            {
                musicBrowser.QueueVideoLast(homeContentItemVideo, true);
                Toast.MakeText(Application.Context, Resource.String.track_added_end_of_queue, ToastLength.Short).Show();
            } // Item is playlist, load the playlist and open the current queue
            else if (homeContentItem.Content is YouTube.Playlist homeContentItemPlaylist)
            {
                musicBrowser.LoadPlaylist(homeContentItemPlaylist);
                mainActivity.OpenCurrentQueueFragment();
            }
        }
    }
}