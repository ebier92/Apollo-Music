// <copyright file="CurrentQueueFragmentPresenter.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Media.Session;
using Android.Views;
using AndroidX.RecyclerView.Widget;

namespace Apollo
{
    /// <summary>
    /// Presenter class for the <see cref="CurrentQueueFragment"/>.
    /// </summary>
    internal class CurrentQueueFragmentPresenter
    {
        private readonly MainActivity mainActivity;
        private readonly ICurrentQueueFragment view;
        private readonly CurrentQueueFragmentModel model;
        private readonly MusicBrowser musicBrowser;
        private bool scrollToTop;

        /// <summary>
        /// Initializes a new instance of the <see cref="CurrentQueueFragmentPresenter"/> class.
        /// </summary>
        /// <param name="mainActivity">A reference to the <see cref="MainActivity"/>.</param>
        /// <param name="view">Instance of a <see cref="CurrentQueueFragment"/>.</param>
        /// <param name="musicBrowser">A connected instance of a <see cref="MusicBrowser"/>.</param>
        public CurrentQueueFragmentPresenter(MainActivity mainActivity, CurrentQueueFragment view, MusicBrowser musicBrowser)
        {
            this.mainActivity = mainActivity;
            this.view = view;
            this.musicBrowser = musicBrowser;
            model = new CurrentQueueFragmentModel(musicBrowser);
            scrollToTop = true;
        }

        /// <summary>
        /// Initializes the fragment on creation.
        /// </summary>
        public void InitializeOnCreate()
        {
            // Perform initializations and updates
            InitializeRecyclerViewComponents();
            UpdatePlaylistName();
            UpdateShuffleState();
            UpdateGeneratedPlaylistNotifications();
            UpdateComponentVisibility();
            InitializeActiveQueueItem();
        }

        /// <summary>
        /// Reinitializes the fragment if it is coming back from a hidden state.
        /// </summary>
        /// <param name="hidden">True if the fragment is being hidden.</param>
        public void InitializeAfterUnhidden(bool hidden)
        {
            if (!hidden)
            {
                // Perform initializations and updates
                UpdatePlaylistName();
                UpdateShuffleState();
                UpdateGeneratedPlaylistNotifications();
                UpdateQueueItems();
                UpdateComponentVisibility();
                InitializeActiveQueueItem();
                view.UpdateSearchViewQuery("");
            }
        }

        /// <summary>
        /// Updates the fragment for possible changes after a playback state change.
        /// </summary>
        /// <param name="adapterItemCount">The number of items displayed by the <see cref="CurrentQueueAdapter"/>.</param>
        public void UpdateAfterPlaybackStateChange(int adapterItemCount)
        {
            // Update the current queue fragment is the number of items displayed is different than the number of queue items for the media session and no filter is enabled (indicating an outdated queue) or if the user has just selected an item from a filtered list.
            if ((musicBrowser.Queue != null && adapterItemCount != musicBrowser.Queue.Count && !view.FilterEnabled) || view.FilteredItemSelected)
            {
                // Set the filtered item selected flag back to false in case it was true
                if (musicBrowser.PlaybackState.State == PlaybackStateCode.Buffering)
                    view.FilteredItemSelected = false;

                UpdatePlaylistName();
                UpdateShuffleState();
                UpdateGeneratedPlaylistNotifications();
                UpdateQueueItems();
                UpdateComponentVisibility();
                InitializeActiveQueueItem();
            }

            // Check if the playlist is getting towards the end and more tracks need to be requested
            if ((musicBrowser.UsingGeneratedPlaylist || musicBrowser.UsingGeneratedRecommendedPlaylist) && !musicBrowser.PlaylistGenerating)
                UpdateContinueGeneratePlaylist();
        }

        /// <summary>
        /// Updates an active queue item if one is playing and scrolls to that item.
        /// </summary>
        public void InitializeActiveQueueItem()
        {
            // Get the media ID of the active item by queue ID
            var playbackState = musicBrowser.PlaybackState;
            var activeQueueItemMediaId = model.GetQueueItemMediaId(playbackState.ActiveQueueItemId);

            // Get the queue position of the active item
            var position = model.GetQueueItemPosition(playbackState.ActiveQueueItemId);

            // Update the active queue item for a valid playback state and position, otherwise scroll to the first item in the playlist if this operation is allowed
            if (playbackState.State != PlaybackStateCode.Stopped && playbackState.State != PlaybackStateCode.None && position != -1)
                view.InitializeActiveQueueItem(activeQueueItemMediaId, position);
            else if (scrollToTop)
                view.ScrollToPosition(0);

            // Reset scroll to top flag
            scrollToTop = true;
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
        /// Toggles the shuffle mode on or off.
        /// </summary>
        public void ToggleShuffleMode()
        {
            // Toggle shuffle mode and update the color of the shuffle mode button if the queue is not filtered
            if (!view.FilterEnabled)
            {
                musicBrowser.Attach((MusicBrowser.IShuffleObserver)view);
                musicBrowser.ToggleShuffleMode();
                view.UpdateShuffleButton(musicBrowser.ShuffleMode);
            }
        }

        /// <summary>
        /// Opens the prompt to enter a playlist name.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to save.</param>
        public void PromptToSaveQueueItems(string playlistName)
        {
            // If the playlist name has been manually specified (is not equal to the default unsaved playlist name), open the prompt with this title prepopulated
            if (playlistName != Application.Context.GetString(Resource.String.unsaved_playlist))
                view.DisplayPromptToSaveQueueItems(playlistName);
            else
                view.DisplayPromptToSaveQueueItems("");
        }

        /// <summary>
        /// Runs input validation for the playlist save and opens a confirmation to overwrite a playlist if it exists.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to save.</param>
        public void PromptToSaveQueueItemsContinue(string playlistName)
        {
            // Prompt the user with an error message for invalid playlists or playlist names and exit
            if (playlistName == "")
            {
                view.DisplayErrorMessage(Application.Context.GetString(Resource.String.error_prompt_message_no_name));
                return;
            }
            else if (playlistName == Application.Context.GetString(Resource.String.unsaved_playlist))
            {
                view.DisplayErrorMessage(Application.Context.GetString(Resource.String.error_prompt_message_invalid_name));
                return;
            }
            else if (musicBrowser.IsConnected && musicBrowser.Queue.Count == 0)
            {
                view.DisplayErrorMessage(Application.Context.GetString(Resource.String.error_prompt_message_empty_queue));
                return;
            }

            // Check if the playlist already exists in the content
            var playlistMediaId = MusicBrowser.CreatePlaylistMediaId(playlistName);
            var playlistExists = MusicBrowser.GetIsPlaylistAndExists(playlistMediaId);

            // If the playlist exists, confirm overwrite with the user, otherwise save the playlist
            if (playlistExists)
                view.DisplayOverwriteQueueItemsConfirmation(playlistName);
            else
                SaveQueueItems(playlistName);
        }

        /// <summary>
        /// Saves the playlist.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to save.</param>
        public void SaveQueueItems(string playlistName)
        {
            // Attach the view to listen for the completion of the save operation
            musicBrowser.Attach((MusicBrowser.ISaveCompleteObserver)view);

            // Send the save queue items command
            var parentMediaId = MusicBrowser.CreatePlaylistMediaId(playlistName);
            musicBrowser.SaveQueueItems(parentMediaId);

            // Update to the newly saved playlist name
            view.UpdatePlaylistName(playlistName);
        }

        /// <summary>
        /// Prompts the user to confirm playlist deletion.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to delete.</param>
        public void PromptToDeleteQueueItems(string playlistName)
        {
            // Call the playlist 'Unsaved Playlist' if the name is null or is the default
            if (playlistName == "" || playlistName == Application.Context.GetString(Resource.String.unsaved_playlist))
                view.DisplayDeleteQueueItemsConfirmation(Application.Context.GetString(Resource.String.unsaved_playlist_name));
            else
                view.DisplayDeleteQueueItemsConfirmation(playlistName);
        }

        /// <summary>
        /// Deletes a playlist by media ID.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to delete.</param>
        public void DeleteQueueItems(string playlistName)
        {
            // Repopulate with an empty queue
            view.UpdateQueueItems(new List<MediaSession.QueueItem>());

            // Clear the queue.
            musicBrowser.ClearQueueItems();

            // Set the playlist name to an empty value
            view.UpdatePlaylistName("");

            // Delete the playlist if it exists
            var parentMediaId = MusicBrowser.CreatePlaylistMediaId(playlistName);
            musicBrowser.DeleteMediaItems(parentMediaId);
        }

        /// <summary>
        /// Checks if the current queue <see cref="RecylerView"/> has reached the end of the list and calls the appropriate view event method.
        /// </summary>
        /// <param name="recyclerView">The <see cref="RecyclerView"/> displaying search results.</param>
        /// <param name="newState">An integer representing the next state of the <see cref="RecyclerView"/>.</param>
        public void CheckScrollChange(RecyclerView recyclerView, int newState)
        {
            // If the recycler view has reached the end of the list, the current playlist is a generated playlist, and playlist generation is not currently occuring,
            // continue generating more playlist items
            if (!recyclerView.CanScrollVertically(1) && newState == RecyclerView.ScrollStateIdle && musicBrowser.UsingGeneratedPlaylist && !musicBrowser.PlaylistGenerating)
            {
                UpdateGeneratedPlaylistNotifications();
                musicBrowser.ContinueGeneratePlaylist();
                UpdateComponentVisibility();
            } // If the recycler view has reached the end of the list, the current playlist is a generated recommended playlist, playlist generation is not occuring, continue generating more items
            else if (!recyclerView.CanScrollVertically(1) && newState == RecyclerView.ScrollStateIdle && musicBrowser.UsingGeneratedRecommendedPlaylist && !musicBrowser.PlaylistGenerating)
            {
                UpdateGeneratedPlaylistNotifications();
                musicBrowser.ContinueGenerateRecommendedPlaylist();
                UpdateComponentVisibility();
            }
        }

        /// <summary>
        /// Updates the new active queue item media ID based on the new active item media ID from a playback state change.
        /// </summary>
        /// <param name="previousActiveItemMediaId">Media ID of the previous active queue item.</param>
        /// <param name="playbackState">Current <see cref="PlaybackState"/> of the session.</param>
        public void UpdateActiveQueueItemAfterPlaybackStateChange(string previousActiveItemMediaId, PlaybackState playbackState)
        {
            var newActiveItemQueueId = playbackState.ActiveQueueItemId;
            var newActiveItemMediaId = model.GetQueueItemMediaId(newActiveItemQueueId);

            // Update the active queue item if the queue ID has changed and the playback state has changed to playing or buffering
            if (previousActiveItemMediaId != newActiveItemMediaId && (playbackState.State == PlaybackStateCode.Playing || playbackState.State == PlaybackStateCode.Buffering))
                view.UpdateActiveQueueItem(newActiveItemMediaId);
        }

        /// <summary>
        /// Reinitializes the current queue after a shuffle change.
        /// </summary>
        public void UpdateItemsAfterShuffleChange()
        {
            musicBrowser.Detach((MusicBrowser.IShuffleObserver)view);
            view.UpdateQueueItems(musicBrowser.Queue.ToList());
            InitializeActiveQueueItem();
        }

        /// <summary>
        /// Reinitializes the current queue after a save operation is completed.
        /// </summary>
        public void UpdateItemsAfterSaveComplete()
        {
            // Disable scroll to top flag so the current scroll position is maintained after an active queue item update
            scrollToTop = false;

            // Detach save operation listener, update queue items, and initialize the active queue item
            musicBrowser.Detach((MusicBrowser.ISaveCompleteObserver)view);
            view.UpdateQueueItems(musicBrowser.Queue.ToList());
            InitializeActiveQueueItem();
        }

        /// <summary>
        /// Reinitializes the current queue after playlist generation has completed.
        /// </summary>
        /// <param name="initialPlaylist">True if the initial playlist was generated.</param>
        public void UpdateItemsAfterGeneratePlaylistComplete(bool initialPlaylist)
        {
            // Scroll to top if generating initial playlist, otherwise disable the scroll to top flag
            if (initialPlaylist)
                view.ScrollToPosition(0);
            else
                scrollToTop = false;

            // Detach playlist generation listener, update queue items, and initialize active queue item
            musicBrowser.Detach((MusicBrowser.IGeneratePlaylistCompleteObserver)view);

            if (musicBrowser.Queue != null)
                view.UpdateQueueItems(musicBrowser.Queue.ToList());

            UpdateComponentVisibility();
        }

        /// <summary>
        /// Updates the active queue item and resets the text of the search view if it is filled.
        /// </summary>
        /// <param name="previousActiveItemMediaId">Media ID of the previous active queue item.</param>
        /// <param name="newActiveItemMediaId">Media ID of the current active queue item.</param>
        public void UpdateOnItemClick(string previousActiveItemMediaId, string newActiveItemMediaId)
        {
            UpdateActiveQueueItemOnItemClick(previousActiveItemMediaId, newActiveItemMediaId);
            ResetSearchViewFilterIfEnabled();
        }

        /// <summary>
        /// Initializes the adapter and touch components of the recycler view.
        /// </summary>
        private void InitializeRecyclerViewComponents()
        {
            IList<MediaSession.QueueItem> queueItems;

            // Attempt to get the current queue
            if (musicBrowser.Queue != null)
                queueItems = musicBrowser.Queue;
            else
                queueItems = new List<MediaSession.QueueItem>();

            // Initialize recycler view components if needed
            if (!view.AdapterInitialized)
                view.InitializeCurrentQueueAdapter(queueItems);

            if (!view.ItemTouchHelperInitialized)
                view.InitializeItemTouchHelper();
        }

        /// <summary>
        /// Clears the search view, reloads unfiltered queue items, and initializes and scrolls to the active queue item if the filter is enabled.
        /// </summary>
        private void ResetSearchViewFilterIfEnabled()
        {
            if (view.FilterEnabled)
            {
                // Clear search view text
                view.UpdateSearchViewQuery("");

                // Set the flag that an item has been selected from the filtered list
                view.FilteredItemSelected = true;

                // Retrieve and populate tracks from the current queue
                UpdateQueueItems();
            }
        }

        /// <summary>
        /// Updates the new active queue item media ID after an item is clicked.
        /// </summary>
        /// <param name="previousActiveItemMediaId">Media ID of the previous active queue item.</param>
        /// <param name="newActiveItemMediaId">Media ID of the current active queue item.</param>
        private void UpdateActiveQueueItemOnItemClick(string previousActiveItemMediaId, string newActiveItemMediaId)
        {
            // Update the active queue item if the queue ID has changed
            if (previousActiveItemMediaId != newActiveItemMediaId)
                view.UpdateActiveQueueItem(newActiveItemMediaId);
        }

        /// <summary>
        /// Retrieves the name of the currently loaded playlist and sets it as the playlist name display text.
        /// </summary>
        private void UpdatePlaylistName()
        {
            // If there are items within the queue, get the media ID from the first item
            string playlistName = "";

            if (!string.IsNullOrEmpty(musicBrowser.QueueTitle))
            {
                playlistName = musicBrowser.QueueTitle;
            }

            view.UpdatePlaylistName(playlistName);
        }

        /// <summary>
        /// Updates the background of the shuffle button based on the current shuffle mode.
        /// </summary>
        private void UpdateShuffleState()
        {
            view.UpdateShuffleButton(musicBrowser.ShuffleMode);
        }

        /// <summary>
        /// Attaches the fragment for notifications about playlist generation completion events.
        /// </summary>
        private void UpdateGeneratedPlaylistNotifications()
        {
            // Attach the fragment for notifications if the music browser shows that the session is using a generated playlist
            if (musicBrowser.PlaylistGenerating || musicBrowser.UsingGeneratedPlaylist || musicBrowser.UsingGeneratedRecommendedPlaylist)
                musicBrowser.Attach((MusicBrowser.IGeneratePlaylistCompleteObserver)view);
        }

        /// <summary>
        /// Retrieves items from the session queue and populates them to the view.
        /// </summary>
        private void UpdateQueueItems()
        {
            // Populate queue items from the session if not null, otherwise populate an empty list
            if (musicBrowser.Queue != null)
                view.UpdateQueueItems(musicBrowser.Queue);
            else
                view.UpdateQueueItems(new List<MediaSession.QueueItem>());
        }

        /// <summary>
        /// Determines whether to show main elements of empty queue elements depending on the number of queue items and the current playlist load status.
        /// </summary>
        private void UpdateComponentVisibility()
        {
            // Set the queue count if the queue is not null
            int queueCount;

            if (musicBrowser.Queue != null)
                queueCount = musicBrowser.Queue.Count;
            else
                queueCount = 0;

            // Whenever playlist is loading or an initial round of track is generating for a playlist, hide main components and empty queue message, show progress bar
            if (musicBrowser.PlaylistLoading || musicBrowser.InitialPlaylistGenerating)
            {
                view.UpdateMainComponentVisibility(ViewStates.Gone);
                view.UpdateEmptyQueueMessageVisibility(ViewStates.Gone);
                view.UpdateCurrentQueueBackgroundVisibility(ViewStates.Gone);
                view.UpdatePlaylistLoadingProgressBarVisibility(ViewStates.Visible);
                view.UpdatePlaylistGeneratingProgressBarVisibility(ViewStates.Gone);
            } // Whenever playlist is generating additional tracks, show main components and playlist generating progress bar
            else if (musicBrowser.PlaylistGenerating)
            {
                view.UpdateMainComponentVisibility(ViewStates.Visible);
                view.UpdateEmptyQueueMessageVisibility(ViewStates.Gone);
                view.UpdateCurrentQueueBackgroundVisibility(ViewStates.Gone);
                view.UpdatePlaylistLoadingProgressBarVisibility(ViewStates.Gone);
                view.UpdatePlaylistGeneratingProgressBarVisibility(ViewStates.Visible);
            } // Whenever playlist is not loading and queue contains items, show main components, hide empty queue message and progress bars
            else if (queueCount > 0)
            {
                view.UpdateMainComponentVisibility(ViewStates.Visible);
                view.UpdateEmptyQueueMessageVisibility(ViewStates.Gone);
                view.UpdateCurrentQueueBackgroundVisibility(ViewStates.Gone);
                view.UpdatePlaylistLoadingProgressBarVisibility(ViewStates.Gone);
                view.UpdatePlaylistGeneratingProgressBarVisibility(ViewStates.Gone);
            } // Anytime playlist is not loading and queue is empty, hide main components and playlist loading progress bar, show empty queue message
            else
            {
                view.UpdateMainComponentVisibility(ViewStates.Gone);
                view.UpdateEmptyQueueMessageVisibility(ViewStates.Visible);
                view.UpdateCurrentQueueBackgroundVisibility(ViewStates.Visible);
                view.UpdatePlaylistLoadingProgressBarVisibility(ViewStates.Gone);
                view.UpdatePlaylistGeneratingProgressBarVisibility(ViewStates.Gone);
            }
        }

        /// <summary>
        /// Requests additional generated playlist tracks if the current active queue item is one of the last three items in the queue.
        /// </summary>
        private void UpdateContinueGeneratePlaylist()
        {
            if (musicBrowser.Queue != null)
            {
                // Get queue index of active queue item
                var activeQueueItemId = musicBrowser.PlaybackState.ActiveQueueItemId;
                var activeQueueItem = musicBrowser.Queue.Where(queueItem => queueItem.QueueId == activeQueueItemId).FirstOrDefault();
                var queueIndex = musicBrowser.Queue.IndexOf(activeQueueItem);

                // Request more tracks if the currently playing item is one of the last three items in the current list
                if (queueIndex >= musicBrowser.Queue.Count - 3)
                {
                    UpdateGeneratedPlaylistNotifications();

                    if (musicBrowser.UsingGeneratedPlaylist)
                        musicBrowser.ContinueGeneratePlaylist();
                    else if (musicBrowser.UsingGeneratedRecommendedPlaylist)
                        musicBrowser.ContinueGenerateRecommendedPlaylist();

                    UpdateComponentVisibility();
                }
            }
        }
    }
}