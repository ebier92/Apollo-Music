// <copyright file="MusicBrowser.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Browse;
using Android.Media.Session;
using Android.OS;
using Java.Lang;

namespace Apollo
{
    /// <summary>
    /// Class to manage the browsing of music content from the <see cref="MusicService"/>. Also provides the ability to modify the music queue being used by the <see cref="MusicService="/>.
    /// </summary>
    internal class MusicBrowser : ResultReceiver
    {
        private readonly MediaBrowser mediaBrowser;
        private readonly ConnectionCallback connectionCallback;
        private readonly List<IShuffleObserver> shuffleObservers;
        private readonly List<ISaveCompleteObserver> saveCompleteObservers;
        private readonly List<IGeneratePlaylistCompleteObserver> generatePlaylistCompleteObservers;
        private MediaController mediaController;
        private MediaControllerCallback mediaControllerCallback;
        private List<IShuffleObserver> shuffleObserversToRemove;
        private List<ISaveCompleteObserver> saveCompleteObserversToRemove;
        private List<IGeneratePlaylistCompleteObserver> generatePlaylistCompleteObserversToRemove;
        private bool isNotifying;
        private bool callbackRegistered;

        /// <summary>
        /// Initializes a new instance of the <see cref="MusicBrowser"/> class.
        /// </summary>
        public MusicBrowser()
            : base(null)
        {
            // Initialize callbacks and media browser
            connectionCallback = new ConnectionCallback();
            mediaBrowser = new MediaBrowser(
                Application.Context,
                new ComponentName(Application.Context, Class.FromType(typeof(MusicService))),
                connectionCallback,
                null);

            // Initialize observer lists
            shuffleObservers = new List<IShuffleObserver>();
            saveCompleteObservers = new List<ISaveCompleteObserver>();
            generatePlaylistCompleteObservers = new List<IGeneratePlaylistCompleteObserver>();
            shuffleObserversToRemove = new List<IShuffleObserver>();
            saveCompleteObserversToRemove = new List<ISaveCompleteObserver>();
            generatePlaylistCompleteObserversToRemove = new List<IGeneratePlaylistCompleteObserver>();

            // Initialize callback registered flag
            callbackRegistered = false;

            // When connected, set up the content root and initialize the media controller
            connectionCallback.OnConnectedAction = () =>
            {
                OnConnected();
            };

            // Show a toast message if the connection fails
            connectionCallback.OnConnectionFailedAction = () =>
            {
                OnConnectionFailed();
            };

            // Clear the content root and media controller if the connection is suspended.
            connectionCallback.OnConnectionSuspendedAction = () =>
            {
                OnConnectionSuspended();
            };
        }

        /// <summary>
        /// Interface to notify an observer of a change in shuffle mode.
        /// </summary>
        public interface IShuffleObserver
        {
            void NotifyShuffle();
        }

        /// <summary>
        /// Interface to notify an observer of when a queue item save operation is completed.
        /// </summary>
        public interface ISaveCompleteObserver
        {
            void NotifySaveComplete();
        }

        /// <summary>
        /// Interface to notify an observer when a generated playlist load has completed.
        /// </summary>
        public interface IGeneratePlaylistCompleteObserver
        {
            void NotifyGeneratePlaylistComplete(bool initialPlaylist);
        }

        /// <summary>
        /// Gets content root.
        /// </summary>
        public string Root { get; private set; }

        /// <summary>
        /// Gets the current session queue.
        /// </summary>
        public IList<MediaSession.QueueItem> Queue
        {
            get { return mediaController.Queue; }
        }

        /// <summary>
        /// Gets the title for the current session queue.
        /// </summary>
        public string QueueTitle
        {
            get { return mediaController.QueueTitle; }
        }

        /// <summary>
        /// Gets current <see cref="PlaybackState"/>.
        /// </summary>
        public PlaybackState PlaybackState
        {
            get { return mediaController.PlaybackState; }
        }

        /// <summary>
        /// Gets current session <see cref="Metadata"/>.
        /// </summary>
        public MediaMetadata Metadata
        {
            get { return mediaController.Metadata; }
        }

        /// <summary>
        /// Gets a value indicating whether the music browser is connected.
        /// </summary>
        public bool IsConnected
        {
            get { return mediaBrowser.IsConnected; }
        }

        /// <summary>
        /// Gets a value indicating whether the current playlist is in shuffle mode.
        /// </summary>
        public bool ShuffleMode { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the current playlist is loading.
        /// </summary>
        public bool PlaylistLoading { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the session is using a generated playlist.
        /// </summary>
        public bool UsingGeneratedPlaylist { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the session is using the recommended playlist.
        /// </summary>
        public bool UsingGeneratedRecommendedPlaylist { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the current playlist is being generated.
        /// </summary>
        public bool PlaylistGenerating { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the initial batch of tracks for a generated playlist are being retrieved.
        /// </summary>
        public bool InitialPlaylistGenerating { get; private set; }

        /// <summary>
        /// Returns a media ID created from a playlist name.
        /// </summary>
        /// <param name="playlistName">The playlist name to create a media ID for.</param>
        /// <returns>Playlist media ID.</returns>
        public static string CreatePlaylistMediaId(string playlistName)
        {
            return MusicService.CreatePlaylistMediaId(playlistName);
        }

        /// <summary>
        /// Determines if a specific media ID hierarchy refers to a playlist and also that the same playlist already exists in the content.
        /// </summary>
        /// <param name="mediaId">Media ID of a specific item.</param>
        /// <returns>True if the media ID hierarchy refers to a playlist and the playlist already exists.</returns>
        public static bool GetIsPlaylistAndExists(string mediaId)
        {
            return MusicService.IsPlaylistAndExists(mediaId);
        }

        /// <summary>
        /// Extracts the playlist name portion of a media ID hierarchy.
        /// </summary>
        /// <param name="mediaId">Media ID of a specific item.</param>
        /// <returns>A string playlist name.</returns>
        public static string GetPlaylistNameFromMediaId(string mediaId)
        {
            return MusicService.GetPlaylistNameFromMediaId(mediaId);
        }

        /// <summary>
        /// Connects the <see cref="MusicBrowser"/> to the current media session.
        /// </summary>
        public void Connect()
        {
            if (!IsConnected)
                mediaBrowser.Connect();
        }

        /// <summary>
        /// Disconnects the <see cref="MusicBrowser"/> from the current media session.
        /// </summary>
        public void Disconnect()
        {
            if (IsConnected)
                mediaBrowser.Disconnect();
        }

        /// <summary>
        /// Subscribes the <see cref="MusicBrowser"/> and returns the hierarchal children objects beneath the media content object specified by the media ID.
        /// </summary>
        /// <param name="mediaId">The media ID of the parent object.</param>
        /// <param name="subscriptionCallback">The <see cref="SubscriptionCallback"/> to receive a callback when children objects are loaded.</param>
        public void Subscribe(string mediaId, SubscriptionCallback subscriptionCallback)
        {
            mediaBrowser.Subscribe(mediaId, subscriptionCallback);
        }

        /// <summary>
        /// Unsubscribes the <see cref="MusicBrowser"/> to the media content object specified by the media ID.
        /// </summary>
        /// <param name="mediaId">The media ID of the parent object subscribed to.</param>
        /// <param name="subscriptionCallback">The <see cref="SubscriptionCallback"/> to remove from receiving callbacks.</param>
        public void Unsubscribe(string mediaId, SubscriptionCallback subscriptionCallback)
        {
            mediaBrowser.Unsubscribe(mediaId, subscriptionCallback);
        }

        /// <summary>
        /// Updates a <see cref="MediaControllerCallback"/> for immediate registration if the media controller exists or for later registration upon initialization.
        /// </summary>
        /// <param name="mediaControllerCallback">The <see cref="MediaControllerCallback"/> to register.</param>
        public void RegisterMediaControllerCallback(MediaControllerCallback mediaControllerCallback)
        {
            this.mediaControllerCallback = mediaControllerCallback;

            // Register immediately if the media controller exists
            if (mediaController != null)
            {
                mediaController.RegisterCallback(mediaControllerCallback);
                callbackRegistered = true;
            }
        }

        /// <summary>
        /// Unregisters the media controller callback if one was registered.
        /// </summary>
        public void UnregisterMediaControllerCallback()
        {
            if (mediaController != null && mediaControllerCallback != null && callbackRegistered)
            {
                mediaController.UnregisterCallback(mediaControllerCallback);
                mediaControllerCallback = null;
                callbackRegistered = false;
            }
        }

        /// <summary>
        /// Skips to the previous track.
        /// </summary>
        public void SkipToPrevious()
        {
            mediaController.GetTransportControls().SkipToPrevious();
        }

        /// <summary>
        /// Stops playback.
        /// </summary>
        public void Stop()
        {
            mediaController.GetTransportControls().Stop();
        }

        /// <summary>
        /// Starts playback.
        /// </summary>
        public void Play()
        {
            mediaController.GetTransportControls().Play();
        }

        /// <summary>
        /// Pauses playback.
        /// </summary>
        public void Pause()
        {
            mediaController.GetTransportControls().Pause();
        }

        /// <summary>
        /// Skips to the next track.
        /// </summary>
        public void SkipToNext()
        {
            mediaController.GetTransportControls().SkipToNext();
        }

        /// <summary>
        /// Plays a track based on the media ID.
        /// </summary>
        /// <param name="mediaId">The media ID of the track to play.</param>
        public void PlayFromMediaId(string mediaId)
        {
            // Unset the playlist flags if the media ID selected if for a saved playlist
            if (MusicService.IsPlaylistAndExists(mediaId))
            {
                UsingGeneratedPlaylist = false;
                UsingGeneratedRecommendedPlaylist = false;
            }

            mediaController.GetTransportControls().PlayFromMediaId(mediaId, null);
        }

        /// <summary>
        /// Seeks to a specific track position during playback.
        /// </summary>
        /// <param name="position">The position to seek to.</param>
        public void SeekTo(long position)
        {
            mediaController.GetTransportControls().SeekTo((long)position);
        }

        /// <summary>
        /// Toggles shuffle mode on or off.
        /// </summary>
        public void ToggleShuffleMode()
        {
            if (!ShuffleMode)
            {
                mediaController.SendCommand(MusicService.Commands.CmdEnableShuffleMode, null, this);
                ShuffleMode = true;
            }
            else
            {
                mediaController.SendCommand(MusicService.Commands.CmdDisableShuffleMode, null, this);
                ShuffleMode = false;
            }
        }

        /// <summary>
        /// Saves all items in the current queue under the specified playlist name.
        /// </summary>
        /// <param name="parentMediaId">The playlist parent media ID to save the queue items as.</param>
        public void SaveQueueItems(string parentMediaId)
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Create command argument bundle
            var args = new Bundle();
            args.PutString("parentMediaId", parentMediaId);

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdSaveQueueItems, args, this);
        }

        /// <summary>
        /// Stops playback and clears all items from the current queue.
        /// </summary>
        public void ClearQueueItems()
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Disable shuffle mode if enabled
            if (ShuffleMode)
                ToggleShuffleMode();

            mediaController.SendCommand(MusicService.Commands.CmdClearQueueItems, null, null);

            // Unset the using playlists flags
            UsingGeneratedPlaylist = false;
            UsingGeneratedRecommendedPlaylist = false;
        }

        /// <summary>
        /// Deletes the associated playlist from the content file if it exists.
        /// </summary>
        /// <param name="parentMediaId">The parent media ID of the playlist to delete.</param>
        public void DeleteMediaItems(string parentMediaId)
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Disable shuffle mode if enabled
            if (ShuffleMode)
                ToggleShuffleMode();

            // Create command argument bundle
            var args = new Bundle();
            args.PutString("parentMediaId", parentMediaId);

            mediaController.SendCommand(MusicService.Commands.CmdDeleteMediaItems, args, null);
        }

        /// <summary>
        /// Moves an item in the music queue to a specified position by media ID.
        /// </summary>
        /// <param name="mediaId">The media ID of the queue item to move.</param>
        /// <param name="toPosition">The integer position in the queue to move the item to.</param>
        public void MoveQueueItem(string mediaId, int toPosition)
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Create command argument bundle
            var args = new Bundle();
            args.PutString("mediaId", mediaId);
            args.PutInt("toPosition", toPosition);

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdMoveQueueItem, args, null);
        }

        /// <summary>
        /// Removes the music queue item at the specified position.
        /// </summary>
        /// <param name="position">The queue position to remove an item from.</param>
        public void RemoveQueueItem(int position)
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Create command argument bundle
            Bundle args = new Bundle();
            args.PutInt("position", position);

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdRemoveQueueItem, args, null);
        }

        /// <summary>
        /// Creates a new queue item from a video and add it as a single item in a new queue.
        /// </summary>
        /// <param name="video"><see cref="YouTube.Video"/> item.</param>
        public void QueueVideoToNewPlaylist(YouTube.Video video)
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Disable shuffle mode if enabled
            if (ShuffleMode)
                ToggleShuffleMode();

            // Create command argument bundle
            var args = new Bundle();
            args.PutString("title", video.Title);
            args.PutString("artist", video.Author);
            args.PutString("iconUri", video.Thumbnails.MediumResUrl);
            args.PutString("mediaUri", video.Url);
            args.PutLong("duration", (long)video.Duration.TotalMilliseconds);

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdQueueVideoToNewPlaylist, args, null);

            // Unset the using playlists flags
            UsingGeneratedPlaylist = false;
            UsingGeneratedRecommendedPlaylist = false;
        }

        /// <summary>
        /// Adds a video as the next item to play after the current queue item.
        /// </summary>
        /// <param name="video"><see cref="YouTube.Video"/> item.</param>
        public void QueueVideoNext(YouTube.Video video)
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Create command argument bundle
            var args = new Bundle();
            args.PutString("title", video.Title);
            args.PutString("artist", video.Author);
            args.PutString("iconUri", video.Thumbnails.MediumResUrl);
            args.PutString("mediaUri", video.Url);
            args.PutLong("duration", (long)video.Duration.TotalMilliseconds);

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdQueueVideoNext, args, null);
        }

        /// <summary>
        /// Adds a video as the last item to play in the queue.
        /// </summary>
        /// <param name="video"><see cref="YouTube.Video"/> item.</param>
        /// <param name="playIfStopped">Plays the newly added item if no other playback is currently occuring.</param>
        public void QueueVideoLast(YouTube.Video video, bool playIfStopped)
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Create command argument bundle
            var args = new Bundle();
            args.PutString("title", video.Title);
            args.PutString("artist", video.Author);
            args.PutString("iconUri", video.Thumbnails.MediumResUrl);
            args.PutString("mediaUri", video.Url);
            args.PutLong("duration", (long)video.Duration.TotalMilliseconds);
            args.PutBoolean("playIfStopped", playIfStopped);

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdQueueVideoLast, args, null);
        }

        /// <summary>
        /// Saves a video to the end of a saved playlist.
        /// </summary>
        /// <param name="video"><see cref="YouTube.Video"/> item.</param>
        /// <param name="parentMediaId">The parent media ID value of the playlist to save the video to.</param>
        public void SaveVideoToPlaylist(YouTube.Video video, string parentMediaId)
        {
            // Create command argument bundle
            var args = new Bundle();
            args.PutString("title", video.Title);
            args.PutString("artist", video.Author);
            args.PutString("mediaUri", video.Url);
            args.PutString("iconUri", video.Thumbnails.MediumResUrl);
            args.PutLong("duration", (long)video.Duration.TotalMilliseconds);
            args.PutString("parentMediaId", parentMediaId);

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdSaveVideoToPlaylist, args, null);
        }

        /// <summary>
        /// Loads a playlist from YouTube to the current queue.
        /// </summary>
        /// <param name="playlist">The <see cref="YouTube.Playlist"/> to load.</param>
        public void LoadPlaylist(YouTube.Playlist playlist)
        {
            // Disable shuffle mode if enabled
            if (ShuffleMode)
                ToggleShuffleMode();

            // Create command argument bundle
            var args = new Bundle();
            args.PutString("playlistId", playlist.PlaylistId);

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdLoadPlaylist, args, this);

            // Set the playlist load flag
            PlaylistLoading = true;

            // Unset the using playlists flags
            UsingGeneratedPlaylist = false;
            UsingGeneratedRecommendedPlaylist = false;
        }

        /// <summary>
        /// Generates a new playlist based on a seed video.
        /// </summary>
        /// <param name="video">The seed <see cref="YouTube.Video"/> to generate a playlist from.</param>
        public void GenerateNewPlaylist(YouTube.Video video)
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Create command argument bundle
            var args = new Bundle();
            args.PutString("videoId", video.VideoId);

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdGenerateNewPlaylist, args, this);

            // Set the playlist generating flag
            PlaylistGenerating = true;

            // Set the initial playlist generating flag
            InitialPlaylistGenerating = true;

            // Set the using generated playlist flag and unset the using recommended playlist flag
            UsingGeneratedPlaylist = true;
            UsingGeneratedRecommendedPlaylist = false;
        }

        /// <summary>
        /// Generates more tracks for an existing playlist.
        /// </summary>
        public void ContinueGeneratePlaylist()
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdContinueGeneratePlaylist, null, this);

            // Set the playlist generating flag
            PlaylistGenerating = true;
        }

        /// <summary>
        /// Generates a playlist based on user recommended tracks.
        /// </summary>
        public void GenerateRecommendedPlaylist()
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdGenerateRecommendedPlaylist, null, this);

            // Set the playlist generating flag
            PlaylistGenerating = true;

            // Set the initial playlist generating flag
            InitialPlaylistGenerating = true;

            // Set the using generating recommended playlist flag and unset the using generated playlist flag
            UsingGeneratedRecommendedPlaylist = true;
            UsingGeneratedPlaylist = false;
        }

        /// <summary>
        /// Generates more tracks for a recommended playlist.
        /// </summary>
        public void ContinueGenerateRecommendedPlaylist()
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdContinueGenerateRecommendedPlaylist, null, this);

            // Set the playlist generating flag
            PlaylistGenerating = true;
        }

        /// <summary>
        /// Generates a playlist consisting of the tracks that the user has previously listened to.
        /// </summary>
        public void GenerateHistoricalPlaylist()
        {
            // Do not allow command when a playlist is loading or generating
            if (PlaylistLoading || PlaylistGenerating)
                return;

            // Send the command
            mediaController.SendCommand(MusicService.Commands.CmdGenerateHistoricalPlaylist, null, this);

            // Set the playlist generating flag
            PlaylistGenerating = true;
        }

        /// <summary>
        /// Attaches an <see cref="IShuffleObserver"/> for notifications.
        /// </summary>
        /// <param name="shuffleObserver">The <see cref="IShuffleObserver"/> to attach.</param>
        public void Attach(IShuffleObserver shuffleObserver)
        {
            if (!shuffleObservers.Contains(shuffleObserver))
                shuffleObservers.Add(shuffleObserver);
        }

        /// <summary>
        /// Attaches an <see cref="ISaveCompleteObserver"/> for notifications.
        /// </summary>
        /// <param name="saveCompleteObserver">The <see cref="ISaveCompleteObserver"/> to attach.</param>
        public void Attach(ISaveCompleteObserver saveCompleteObserver)
        {
            if (!saveCompleteObservers.Contains(saveCompleteObserver))
                saveCompleteObservers.Add(saveCompleteObserver);
        }

        /// <summary>
        /// Attaches an <see cref="IGeneratePlaylistCompleteObserver"/> for notifications.
        /// </summary>
        /// <param name="generatePlaylistCompleteObserver">The <see cref="IGeneratePlaylistCompleteObserver"/> to attach.</param>
        public void Attach(IGeneratePlaylistCompleteObserver generatePlaylistCompleteObserver)
        {
            if (!generatePlaylistCompleteObservers.Contains(generatePlaylistCompleteObserver))
                generatePlaylistCompleteObservers.Add(generatePlaylistCompleteObserver);
        }

        /// <summary>
        /// Detaches an <see cref="IShuffleObserver"/> for notifications.
        /// </summary>
        /// <param name="shuffleObserver">The <see cref="IShuffleObserver"/> to detach.</param>
        public void Detach(IShuffleObserver shuffleObserver)
        {
            if (!isNotifying)
                shuffleObservers.Remove(shuffleObserver);
            else
                shuffleObserversToRemove.Add(shuffleObserver);
        }

        /// <summary>
        /// Detaches an <see cref="ISaveCompleteObserver"/> for notifications.
        /// </summary>
        /// <param name="saveCompleteObserver">The <see cref="ISaveCompleteObserver"/> to detach.</param>
        public void Detach(ISaveCompleteObserver saveCompleteObserver)
        {
            if (!isNotifying)
                saveCompleteObservers.Remove(saveCompleteObserver);
            else
                saveCompleteObserversToRemove.Add(saveCompleteObserver);
        }

        /// <summary>
        /// Detaches an <see cref="IGeneratePlaylistCompleteObserver"/> for notifications.
        /// </summary>
        /// <param name="generatePlaylistCompleteObserver">The <see cref="IGeneratePlaylistCompleteObserver"/> to detach.</param>
        public void Detach(IGeneratePlaylistCompleteObserver generatePlaylistCompleteObserver)
        {
            if (!isNotifying)
                generatePlaylistCompleteObservers.Remove(generatePlaylistCompleteObserver);
            else
                generatePlaylistCompleteObserversToRemove.Add(generatePlaylistCompleteObserver);
        }

        /// <summary>
        /// Handles the results of commands send to the session.
        /// </summary>
        /// <param name="resultCode">Integer result code of the command result.</param>
        /// <param name="resultData">A <see cref="Bundle"/> containing data from the resulting command.</param>
        protected override void OnReceiveResult(int resultCode, Bundle resultData)
        {
            // Get the command name and result from the result data
            var command = resultData.GetString("command");

            // Handle either shuffle command
            if (command == MusicService.Commands.CmdEnableShuffleMode || command == MusicService.Commands.CmdDisableShuffleMode)
            {
                // Set flag that notifications are happening
                isNotifying = true;

                // Notify each observer of a shuffle
                foreach (var shuffleObserver in shuffleObservers)
                {
                    shuffleObserver.NotifyShuffle();
                }

                // Disable flag that notifications are happening
                isNotifying = false;

                // Remove all observers that requested a detach during the notfications
                foreach (var shuffleObserver in shuffleObserversToRemove)
                {
                    shuffleObservers.Remove(shuffleObserver);
                }

                // Reset shuffle observers to remove list
                shuffleObserversToRemove = new List<IShuffleObserver>();
            }
            else if (command == MusicService.Commands.CmdSaveQueueItems)
            {
                // Set flag that notifications are happening
                isNotifying = true;

                // Notify each observer of a shuffle
                foreach (var saveCompleteObserver in saveCompleteObservers)
                {
                    saveCompleteObserver.NotifySaveComplete();
                }

                // Disable flag that notifications are happening
                isNotifying = false;

                // Remove all observers that requested a detach during the notfications
                foreach (var saveCompleteObserver in saveCompleteObserversToRemove)
                {
                    saveCompleteObservers.Remove(saveCompleteObserver);
                }

                // Reset shuffle observers to remove list
                saveCompleteObserversToRemove = new List<ISaveCompleteObserver>();
            } // Handle load playlist command
            else if (command == MusicService.Commands.CmdLoadPlaylist)
            {
                // Unset the playlist loading flag
                PlaylistLoading = false;

                // If the command was successful, start playback
                if (resultCode == (int)Result.Ok)
                {
                    mediaController.GetTransportControls().Play();
                } // Show error message if result was not successful and network is not connected
                else if (!NetworkStatus.IsConnected)
                {
                    Android.Widget.Toast.MakeText(Application.Context, Resource.String.error_no_network, Android.Widget.ToastLength.Short).Show();
                } // Show generic error message if the result was not successful and the network is connected
                else
                {
                    Android.Widget.Toast.MakeText(Application.Context, Resource.String.error_loading_playlist, Android.Widget.ToastLength.Short).Show();
                }
            } // Handle playlist generation commands
            else if (command == MusicService.Commands.CmdGenerateNewPlaylist || command == MusicService.Commands.CmdContinueGeneratePlaylist
                    || command == MusicService.Commands.CmdGenerateRecommendedPlaylist || command == MusicService.Commands.CmdContinueGenerateRecommendedPlaylist
                    || command == MusicService.Commands.CmdGenerateHistoricalPlaylist)
            {
                // Set flag that notifications are happening
                isNotifying = true;

                // Unset the initial playlist generating flag after saving the value for the notification
                var initialPlaylist = InitialPlaylistGenerating;
                InitialPlaylistGenerating = false;

                // Unset the playlist generating flag
                PlaylistGenerating = false;

                // Notify each observer that playlist generation is complete
                foreach (var generatePlaylistCompleteObserver in generatePlaylistCompleteObservers)
                {
                    generatePlaylistCompleteObserver.NotifyGeneratePlaylistComplete(initialPlaylist);
                }

                // Disable flag that notifications are happening
                isNotifying = false;

                // Remove all observers that requested a detach during the notifications
                foreach (var generatePlaylistCompleteObserver in generatePlaylistCompleteObserversToRemove)
                {
                    generatePlaylistCompleteObservers.Remove(generatePlaylistCompleteObserver);
                }

                // Reset playlist generation complete observers to remove list
                generatePlaylistCompleteObserversToRemove = new List<IGeneratePlaylistCompleteObserver>();
            }
        }

        /// <summary>
        /// Updates up the content root and initializes the media controller once the <see cref="MusicBrowser"/> is connected.
        /// </summary>
        private void OnConnected()
        {
            Root = mediaBrowser.Root;
            mediaController = new MediaController(Application.Context, mediaBrowser.SessionToken);

            // Register the callback if one was set
            if (!callbackRegistered && mediaControllerCallback != null)
            {
                mediaController.RegisterCallback(mediaControllerCallback);
                callbackRegistered = true;
            }
        }

        /// <summary>
        /// Displays an error message if the connection to the <see cref="MusicService"/> fails.
        /// </summary>
        private void OnConnectionFailed()
        {
            Android.Widget.Toast.MakeText(Application.Context, Resource.String.error_could_not_connect, Android.Widget.ToastLength.Short).Show();
        }

        /// <summary>
        /// Clears the content root and disposes the media controller if the connection is suspended.
        /// </summary>
        private void OnConnectionSuspended()
        {
            Root = null;

            if (mediaController != null)
            {
                UnregisterMediaControllerCallback();
                mediaController.Dispose();
                mediaController = null;
            }
        }

        /// <summary>
        /// Receives callbacks for subscription related events.
        /// </summary>
        public class SubscriptionCallback : MediaBrowser.SubscriptionCallback
        {
            public Action<string, IList<MediaBrowser.MediaItem>> OnChildrenLoadedAction { get; set; }

            public Action<string> OnErrorAction { get; set; }

            public override void OnChildrenLoaded(string parentId, IList<MediaBrowser.MediaItem> children)
            {
                OnChildrenLoadedAction(parentId, children);
            }

            public override void OnError(string parentId)
            {
                OnErrorAction(parentId);
            }
        }

        /// <summary>
        /// Receives callbacks for media session related events.
        /// </summary>
        public class MediaControllerCallback : MediaController.Callback
        {
            public Action<PlaybackState> OnPlaybackStateChangedAction { get; set; }

            public Action<IList<MediaSession.QueueItem>> OnQueueChangedAction { get; set; }

            public override void OnPlaybackStateChanged(PlaybackState state)
            {
                OnPlaybackStateChangedAction(state);
            }

            public override void OnQueueChanged(IList<MediaSession.QueueItem> queue)
            {
                OnQueueChangedAction(queue);
            }
        }

        /// <summary>
        /// Receives callbacks for connection related events.
        /// </summary>
        private class ConnectionCallback : MediaBrowser.ConnectionCallback
        {
            public Action OnConnectedAction { get; set; }

            public Action OnConnectionFailedAction { get; set; }

            public Action OnConnectionSuspendedAction { get; set; }

            public override void OnConnected()
            {
                OnConnectedAction();
            }

            public override void OnConnectionFailed()
            {
                OnConnectionFailedAction();
            }

            public override void OnConnectionSuspended()
            {
                OnConnectionSuspendedAction();
            }
        }
    }
}