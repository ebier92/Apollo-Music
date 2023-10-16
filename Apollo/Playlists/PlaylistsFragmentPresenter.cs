// <copyright file="PlaylistsFragmentPresenter.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Android.Media.Browse;
using Android.Views;

namespace Apollo
{
    /// <summary>
    /// Presenter class for the <see cref="PlaylistsFragment"/>.
    /// </summary>
    internal class PlaylistsFragmentPresenter
    {
        private readonly IPlaylistsFragment view;
        private readonly MusicBrowser musicBrowser;
        private readonly PlaylistsFragmentModel model;
        private readonly MusicBrowser.SubscriptionCallback subscriptionCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistsFragmentPresenter"/> class.
        /// </summary>
        /// <param name="view">Instance of a <see cref="PlaylistsFragment"/>.</param>
        /// <param name="musicBrowser">A connected instance of a <see cref="MusicBrowser"/>.</param>
        public PlaylistsFragmentPresenter(PlaylistsFragment view, MusicBrowser musicBrowser)
        {
            this.view = view;
            this.musicBrowser = musicBrowser;
            model = new PlaylistsFragmentModel(this.musicBrowser);
            subscriptionCallback = new MusicBrowser.SubscriptionCallback();

            // Populate playlist items to the view and unsubscribe once playlist items are loaded
            subscriptionCallback.OnChildrenLoadedAction = (parentId, children) =>
            {
                InitializePlaylistsFragment(parentId, children, subscriptionCallback);
            };

            subscriptionCallback.OnErrorAction = (parentId) =>
            {
                Android.Widget.Toast.MakeText(Android.App.Application.Context, Resource.String.error_loading_playlist, Android.Widget.ToastLength.Short).Show();
            };
        }

        /// <summary>
        /// Subscribes the <see cref="PlaylistsFragmentModel"/> to the root of the <see cref="MusicBrowser"/> to return playlist items.
        /// </summary>
        public void Subscribe()
        {
            model.SubscribeMusicBrowser(musicBrowser.Root, subscriptionCallback);
        }

        /// <summary>
        /// Initializes the elements on the fragment and populates playlists.
        /// </summary>
        /// <param name="parentId">The parent ID of the children items that are loaded after subscription to the <see cref="MusicBrowser"/>.</param>
        /// <param name="children">The children items loaded after subscription to the <see cref="MusicBrowser"/>.</param>
        /// <param name="subscriptionCallback">The <see cref="MusicBrowser.SubscriptionCallback"/> to receive callbacks after subscription.</param>
        private void InitializePlaylistsFragment(string parentId, IList<MediaBrowser.MediaItem> children, MusicBrowser.SubscriptionCallback subscriptionCallback)
        {
            // Child playlist items found, hide empty page elements and show the recycler view
            if (children.Count > 0)
            {
                view.UpdateEmptyPageElementsVisibility(ViewStates.Gone);
                view.UpdateRecyclerViewVisibility(ViewStates.Visible);
            } // No child playlist items found, hide recycler view and show empty page elements
            else
            {
                view.UpdateEmptyPageElementsVisibility(ViewStates.Visible);
                view.UpdateRecyclerViewVisibility(ViewStates.Gone);
            }

            // Populate playlist items and unsubscribe
            view.UpdatePlaylistItems(children);
            model.UnsubscribeMusicBrowser(parentId, subscriptionCallback);
        }
    }
}