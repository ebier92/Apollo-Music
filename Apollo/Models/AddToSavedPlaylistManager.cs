// <copyright file="AddToSavedPlaylistManager.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Media.Browse;
using Android.Views;
using Android.Widget;

namespace Apollo
{
    /// <summary>
    /// Class to display and manage prompts for a user saving a track to a previouly saved playlist.
    /// </summary>
    internal static class AddToSavedPlaylistManager
    {
        /// <summary>
        /// Displays a prompt to the user to add a track to a previously saved playlist.
        /// </summary>
        /// <param name="video">The <see cref="YouTube.Video"/> to add to the playlist.</param>
        /// <param name="musicBrowser">A connected instance of the <see cref="MusicBrowser"/>.</param>
        /// <param name="activity">The app <see cref="Activity"/>.</param>
        public static void DisplayAddToSavedPlaylistPrompt(YouTube.Video video, MusicBrowser musicBrowser, Activity activity)
        {
            if (musicBrowser != null && musicBrowser.IsConnected)
            {
                var subscriptionCallback = new MusicBrowser.SubscriptionCallback
                {
                    // Call the next method to build and display the prompt after playlist names have loaded
                    OnChildrenLoadedAction = (parentId, children) =>
                    {
                        if (children.Count > 0)
                            OnPlaylistsLoaded(video, musicBrowser, activity, children.ToList());
                        else
                            Toast.MakeText(Application.Context, Resource.String.empty_playlists_message, ToastLength.Short).Show();
                    },

                    OnErrorAction = (parentId) =>
                    {
                        Toast.MakeText(Application.Context, Resource.String.error_loading_playlist, ToastLength.Short).Show();
                    },
                };

                musicBrowser.Subscribe(musicBrowser.Root, subscriptionCallback);
            }
        }

        /// <summary>
        /// Builds and displays a dialog to add a track to a previously saved playlists once the list of playlists is available.
        /// </summary>
        /// <param name="video">The <see cref="YouTube.Video"/> to add to the playlist.</param>
        /// <param name="musicBrowser">A connected instance of the <see cref="MusicBrowser"/>.</param>
        /// <param name="activity">The app <see cref="Activity"/>.</param>
        /// <param name="children">A list of saved playlists stored as <see cref="MediaBrowser.MediaItem"/>s.</param>
        private static void OnPlaylistsLoaded(YouTube.Video video, MusicBrowser musicBrowser, Activity activity, List<MediaBrowser.MediaItem> children)
        {
            // Get the name of the currently loaded playlist
            string currentPlaylist = "";

            if (musicBrowser.QueueTitle != null)
                currentPlaylist = musicBrowser.QueueTitle;

            // Get the names of all saved playlists
            var playlistsList = (from mediaItem in children
                                 select mediaItem.Description.Title).ToList();
            playlistsList.Sort();
            var playlists = playlistsList.ToArray();

            // Inflate settings menu layout
            var inflater = (LayoutInflater)activity.GetSystemService(Context.LayoutInflaterService);
            var view = inflater.Inflate(Resource.Layout.add_to_saved_playlist_menu, null);

            // Build spinner for playlist selection
            var spinnerAdapter = new ArrayAdapter<string>(Application.Context, Resource.Layout.support_simple_spinner_dropdown_item, playlists);
            spinnerAdapter.SetDropDownViewResource(Resource.Layout.support_simple_spinner_dropdown_item);
            var spinner = view.FindViewById<Spinner>(Resource.Id.spinner_saved_playlist);
            spinner.Adapter = spinnerAdapter;

            // Modify appearance of the selected item
            spinner.ItemSelected += (sender, e) =>
            {
                ((TextView)e.Parent.GetChildAt(0)).SetTextColor(((MainActivity)activity).ThemeManager.TextColorSecondary);
                ((TextView)e.Parent.GetChildAt(0)).SetTextSize(Android.Util.ComplexUnitType.Sp, 18);
            };

            // Create prompt
            var prompt = new AlertDialog.Builder(activity);

            // Build and show popup
            prompt.SetView(view);
            prompt.SetNegativeButton(Application.Context.GetString(Resource.String.cancel), (sender, e) => { return; });
            prompt.SetPositiveButton(
                Application.Context.GetString(Resource.String.save),
                (sender, e) =>
                {
                    var selectedPlaylistMediaId = MusicService.CreatePlaylistMediaId((string)spinner.SelectedItem);
                    musicBrowser.SaveVideoToPlaylist(video, selectedPlaylistMediaId);
                });

            prompt.Show();
        }
    }
}