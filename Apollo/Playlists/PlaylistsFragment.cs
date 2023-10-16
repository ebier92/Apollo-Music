// <copyright file="PlaylistsFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Android.App;
using Android.Media.Browse;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace Apollo
{
    /// <summary>
    /// <see cref="Fragment"/> class to show playlists saved by the user.
    /// </summary>
    internal class PlaylistsFragment : Fragment, IPlaylistsFragment
    {
        private MainActivity mainActivity;
        private PlaylistsFragmentPresenter presenter;
        private MusicBrowser musicBrowser;
        private TextView emptyPlaylistsMessage;
        private ImageView playlistsBackground;
        private RecyclerView playlistsRecyclerView;
        private RecyclerView.LayoutManager layoutManager;
        private PlaylistsAdapter adapter;

        /// <summary>
        /// Inflates the <see cref="PlaylistsFragment"/>, sets up a <see cref="RecyclerView"/>, and retrieves a list of playlists.
        /// </summary>
        /// <param name="inflater">A <see cref="LayoutInflater"/> to inflate the view.</param>
        /// <param name="container">A containing <see cref="ViewGroup"/>.</param>
        /// <param name="savedInstanceState">A <see cref="Bundle"/> representing the previousy saved state.</param>
        /// <returns>Inflated <see cref="View"/>.</returns>
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Initialize objects
            mainActivity = (MainActivity)Activity;
            musicBrowser = mainActivity.MusicBrowser;
            presenter = new PlaylistsFragmentPresenter(this, musicBrowser);

            // Create playlists fragment
            var view = inflater.Inflate(Resource.Layout.fragment_playlists, container, false);

            // Find fragment UI elements
            emptyPlaylistsMessage = view.FindViewById<TextView>(Resource.Id.txt_empty_playlists_message);
            playlistsBackground = view.FindViewById<ImageView>(Resource.Id.img_playlists_background);
            playlistsRecyclerView = view.FindViewById<RecyclerView>(Resource.Id.recycler_view_playlists);

            // Set up a linear layout manager
            layoutManager = new LinearLayoutManager(Application.Context);
            playlistsRecyclerView.SetLayoutManager(layoutManager);

            return view;
        }

        /// <summary>
        /// Loads the playlist items on start.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            // Retrieve and populate playlist items from saved data
            presenter.Subscribe();
        }

        /// <summary>
        /// Reinitialize the fragment if it is coming back from a hidden state.
        /// </summary>
        /// <param name="hidden">True if the fragment is being hidden.</param>
        public override void OnHiddenChanged(bool hidden)
        {
            // Retrieve and populate playlist items from saved data if not being hidden
            if (!hidden)
                presenter.Subscribe();

            base.OnHiddenChanged(hidden);
        }

        /// <summary>
        /// Updates the visibility state of the elements to show if there are no playlists to display.
        /// </summary>
        /// <param name="viewState">The visibility state to set to the empty page elements.</param>
        public void UpdateEmptyPageElementsVisibility(ViewStates viewState)
        {
            emptyPlaylistsMessage.Visibility = viewState;
            playlistsBackground.Visibility = viewState;
        }

        /// <summary>
        /// Updates the list of playlists to display on the fragment.
        /// </summary>
        /// <param name="playlistItems">List of playlist items.</param>
        public void UpdatePlaylistItems(IList<MediaBrowser.MediaItem> playlistItems)
        {
            // Set the adapter for the recycler view
            adapter = new PlaylistsAdapter(playlistItems);
            adapter.ItemClick += OnItemClick;
            playlistsRecyclerView.SetAdapter(adapter);
        }

        /// <summary>
        /// Updates the visibility state of the <see cref="RecyclerView"/>.
        /// </summary>
        /// <param name="viewState">The visibility state to set to the <see cref="RecyclerView"/>.</param>
        public void UpdateRecyclerViewVisibility(ViewStates viewState)
        {
            playlistsRecyclerView.Visibility = viewState;
        }

        /// <summary>
        /// Triggered when a specific item is clicked.
        /// </summary>
        /// <param name="sender">The sender <see cref="object"/>.</param>
        /// <param name="playlistName">Name of the playlist that was clicked.</param>
        private void OnItemClick(object sender, string playlistName)
        {
            // Disable shuffle mode if enabled
            if (musicBrowser.ShuffleMode)
                musicBrowser.ToggleShuffleMode();

            // Use the media controller to load in tracks for the selected playlist into the current MusicService queue
            musicBrowser.PlayFromMediaId(MusicBrowser.CreatePlaylistMediaId(playlistName));

            // Request that the main activity inflate the current queue fragment, so the selected playlist can be displayed
            mainActivity.OpenCurrentQueueFragment();
        }

        /// <summary>
        /// View holder class for the playlist <see cref="RecyclerView"/>.
        /// </summary>
        public class PlaylistsViewHolder : RecyclerView.ViewHolder
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PlaylistsViewHolder"/> class.
            /// </summary>
            /// <param name="view">Layout to use for each item.</param>
            /// <param name="viewHolderListener">Click listener.</param>
            public PlaylistsViewHolder(View view, Action<string> viewHolderListener)
                : base(view)
            {
                PlaylistThumbnail = view.FindViewById<ImageView>(Resource.Id.img_playlist_thumbnail);
                PlaylistTitle = view.FindViewById<TextView>(Resource.Id.txt_playlist_title);
                PlaylistTrackCount = view.FindViewById<TextView>(Resource.Id.txt_playlist_track_count);
                view.Click += (sender, e) => viewHolderListener(PlaylistTitle.Text);
            }

            public ImageView PlaylistThumbnail { get; private set; }

            public TextView PlaylistTitle { get; private set; }

            public TextView PlaylistTrackCount { get; private set; }
        }
    }
}