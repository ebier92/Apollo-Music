// <copyright file="CurrentQueueFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Android.Animation;
using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media.Session;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Fragment = AndroidX.Fragment.App.Fragment;
using SearchView = AndroidX.AppCompat.Widget.SearchView;

namespace Apollo
{
    /// <summary>
    /// Fragment class to show the currently loaded music queue.
    /// </summary>
    internal class CurrentQueueFragment : Fragment, ICurrentQueueFragment, MainActivity.IPlaybackStateChangedObserver, CurrentQueueAdapter.IFilterEnabledObserver,
        CurrentQueueItemTouch.IOnStartDragListener, MusicBrowser.IShuffleObserver, MusicBrowser.ISaveCompleteObserver, MusicBrowser.IGeneratePlaylistCompleteObserver
    {
        private MainActivity mainActivity;
        private CurrentQueueFragmentPresenter presenter;
        private MusicBrowser musicBrowser;
        private ItemTouchHelper itemTouchHelper;
        private CurrentQueueItemTouch.ItemTouchCallback itemTouchCallback;
        private TextView emptyQueueMessage;
        private TextView playlistName;
        private SearchView currentQueueSearchView;
        private Button shuffleButton;
        private Button saveButton;
        private Button deleteButton;
        private ImageView currentQueueBackground;
        private RecyclerView currentQueueRecyclerView;
        private RecyclerView.LayoutManager layoutManager;
        private ProgressBar playlistLoading;
        private ProgressBar playlistGenerating;
        private CurrentQueueAdapter currentQueueAdapter;

        public bool FilterEnabled
        {
            get { return currentQueueAdapter.FilterEnabled; }
        }

        public bool FilteredItemSelected
        {
            get; set;
        }

        public bool AdapterInitialized
        {
            get { return currentQueueAdapter != null; }
        }

        public bool ItemTouchHelperInitialized
        {
            get { return itemTouchHelper != null; }
        }

        /// <summary>
        /// Inflates the <see cref="CurrentQueueFragment"/> and sets up a <see cref="RecyclerView"/>.
        /// </summary>
        /// <param name="inflater">A <see cref="LayoutInflater"/> to inflate the view.</param>
        /// <param name="container">A containing <see cref="ViewGroup"/>.</param>
        /// <param name="savedInstanceState">A <see cref="Bundle"/> representing the previously saved state.</param>
        /// <returns>Inflated <see cref="View"/>.</returns>
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Initialize objects
            mainActivity = (MainActivity)Activity;
            musicBrowser = mainActivity.MusicBrowser;
            presenter = new CurrentQueueFragmentPresenter(mainActivity, this, musicBrowser);

            // Restart the app if the music browser is disconnected
            presenter.RestartIfMusicBrowserDisconnected();

            // Create Current Queue Fragment
            var view = inflater.Inflate(Resource.Layout.fragment_current_queue, container, false);

            // Find fragment UI elements
            emptyQueueMessage = view.FindViewById<TextView>(Resource.Id.txt_empty_queue_message);
            playlistName = view.FindViewById<TextView>(Resource.Id.txt_playlist_name);
            currentQueueSearchView = view.FindViewById<SearchView>(Resource.Id.search_view_current_queue);
            shuffleButton = view.FindViewById<Button>(Resource.Id.btn_shuffle);
            saveButton = view.FindViewById<Button>(Resource.Id.btn_save);
            deleteButton = view.FindViewById<Button>(Resource.Id.btn_delete);
            currentQueueBackground = view.FindViewById<ImageView>(Resource.Id.img_current_queue_background);
            currentQueueRecyclerView = view.FindViewById<RecyclerView>(Resource.Id.recycler_view_current_queue);
            playlistLoading = view.FindViewById<ProgressBar>(Resource.Id.progress_bar_playlist_loading);
            playlistGenerating = view.FindViewById<ProgressBar>(Resource.Id.progress_bar_playlist_generating);

            // Shuffle mode button
            shuffleButton.Click += (sender, e) =>
            {
                presenter.ToggleShuffleMode();
            };

            // Save playlist button
            saveButton.Click += (sender, e) =>
            {
                presenter.PromptToSaveQueueItems(playlistName.Text);
            };

            // Delete playlist button
            deleteButton.Click += (sender, e) =>
            {
                presenter.PromptToDeleteQueueItems(playlistName.Text);
            };

            // Set on scroll listener
            var onScrollListener = new RecyclerViewOnScrollListener
            {
                OnScrollChangedAction = (recyclerView, newState) =>
                {
                    presenter.CheckScrollChange(recyclerView, newState);
                },
            };

            currentQueueRecyclerView.AddOnScrollListener(onScrollListener);

            // Set up a linear layout manager
            layoutManager = new LinearLayoutManager(Application.Context);
            currentQueueRecyclerView.SetLayoutManager(layoutManager);

            // Initialize major components
            presenter.InitializeOnCreate();

            // Invoke a filter when text changes in the search view
            currentQueueSearchView.QueryTextChange += (s, e) =>
            {
                currentQueueAdapter.Filter.InvokeFilter(e.NewText);
            };

            return view;
        }

        /// <summary>
        /// Reinitialize the fragment if it is coming back from a hidden state.
        /// </summary>
        /// <param name="hidden">True if the fragment is being hidden.</param>
        public override void OnHiddenChanged(bool hidden)
        {
            presenter.InitializeAfterUnhidden(hidden);

            base.OnHiddenChanged(hidden);
        }

        /// <summary>
        /// Detaches the fragment as an observer of current queue adapter changes.
        /// </summary>
        public override void OnDestroy()
        {
            currentQueueAdapter.Detach(this);

            base.OnDestroy();
        }

        /// <summary>
        /// Event triggered when a drag action is started.
        /// </summary>
        /// <param name="viewHolder">The holder of the view being dragged.</param>
        public void OnStartDrag(RecyclerView.ViewHolder viewHolder)
        {
            itemTouchHelper.StartDrag(viewHolder);
        }

        /// <summary>
        /// Notification of when the playback state changes and updates the queue and active queue item if it has changed.
        /// </summary>
        /// <param name="playbackState">The new <see cref="PlaybackState"/>.</param>
        public void NotifyPlaybackStateChange(PlaybackState playbackState)
        {
            presenter.UpdateAfterPlaybackStateChange(currentQueueAdapter.ItemCount);
            presenter.UpdateActiveQueueItemAfterPlaybackStateChange(currentQueueAdapter.ActiveItemMediaId, playbackState);
        }

        /// <summary>
        /// Notification of when the state of the filter enabled value of the adapter changes.
        /// </summary>
        public void NotifyFilterEnabledChange()
        {
            // Disable touch if filter is enabled and vice versa
            itemTouchCallback.EnableTouch = !currentQueueAdapter.FilterEnabled;
        }

        /// <summary>
        /// Notification of when a shuffle state change occurs.
        /// </summary>
        public void NotifyShuffle()
        {
            presenter.UpdateItemsAfterShuffleChange();
        }

        /// <summary>
        /// Notification of when a queue item save operation has completed.
        /// </summary>
        public void NotifySaveComplete()
        {
            presenter.UpdateItemsAfterSaveComplete();
        }

        /// <summary>
        /// Notification of when playlist generation has completed.
        /// </summary>
        /// <param name="initialPlaylist">True if the initial playlist was generated.</param>
        public void NotifyGeneratePlaylistComplete(bool initialPlaylist)
        {
            presenter.UpdateItemsAfterGeneratePlaylistComplete(initialPlaylist);
        }

        /// <summary>
        /// Initializes the <see cref="CurrentQueueAdapter"/>.
        /// </summary>
        /// <param name="queueItems"><see cref="MediaSession.QueueItem"/> IList to load.</param>
        public void InitializeCurrentQueueAdapter(IList<MediaSession.QueueItem> queueItems)
        {
            // Set up the adapter for the recycler view
            currentQueueAdapter = new CurrentQueueAdapter(queueItems, musicBrowser, Activity as MainActivity);
            currentQueueAdapter.ItemClick += OnItemClick;
            currentQueueRecyclerView.SetAdapter(currentQueueAdapter);

            // Attach the view as an observer to filter enabled change events on the adapter
            currentQueueAdapter.Attach(this);
        }

        /// <summary>
        /// Initializes the <see cref="ItemTouchHelper"/>.
        /// </summary>
        public void InitializeItemTouchHelper()
        {
            itemTouchCallback = new CurrentQueueItemTouch.ItemTouchCallback(currentQueueAdapter);
            itemTouchHelper = new ItemTouchHelper(itemTouchCallback);
            itemTouchHelper.AttachToRecyclerView(currentQueueRecyclerView);
        }

        /// <summary>
        /// Initializes the active queue item media ID and scrolls to the active item.
        /// </summary>
        /// <param name="activeItemMediaId">The media ID of the active queue item.</param>
        /// <param name="position">The position of in the queue of the active item.</param>
        public void InitializeActiveQueueItem(string activeItemMediaId, int position)
        {
            currentQueueAdapter.ActiveItemMediaId = activeItemMediaId;
            ScrollToPosition(position);
        }

        /// <summary>
        /// Updates the active item media ID when an item is clicked.
        /// </summary>
        /// <param name="newActiveItemMediaId">The media ID of the new active queue item.</param>
        public void UpdateActiveQueueItem(string newActiveItemMediaId)
        {
            currentQueueAdapter.ActiveItemMediaId = newActiveItemMediaId;
            currentQueueAdapter.NotifyDataSetChanged();
        }

        /// <summary>
        /// Updates the visbility of the current queue background icon.
        /// </summary>
        /// <param name="viewState">The visibility state to set to the current queue background icon.</param>
        public void UpdateCurrentQueueBackgroundVisibility(ViewStates viewState)
        {
            currentQueueBackground.Visibility = viewState;
        }

        /// <summary>
        /// Updates the visibility states for the empty queue message.
        /// </summary>
        /// <param name="viewState">The view state to set for the empty queue message.</param>
        public void UpdateEmptyQueueMessageVisibility(ViewStates viewState)
        {
            emptyQueueMessage.Visibility = viewState;
        }

        /// <summary>
        /// Updates the visibility states for the main fragment elements.
        /// </summary>
        /// <param name="viewState">The view state to set for the main fragment elements.</param>
        public void UpdateMainComponentVisibility(ViewStates viewState)
        {
            // Set visibility state for main elements
            playlistName.Visibility = viewState;
            currentQueueSearchView.Visibility = viewState;
            shuffleButton.Visibility = viewState;
            saveButton.Visibility = viewState;
            deleteButton.Visibility = viewState;
            currentQueueRecyclerView.Visibility = viewState;
        }

        /// <summary>
        /// Updates the visibility state for the playlist loading icon in the middle of the page.
        /// </summary>
        /// <param name="viewState">The view state to set to the loading icon.</param>
        public void UpdatePlaylistLoadingProgressBarVisibility(ViewStates viewState)
        {
            playlistLoading.Visibility = viewState;
        }

        /// <summary>
        /// Updates the visibility state for the playlist generating icon in the middle of the page.
        /// </summary>
        /// <param name="viewState">The view state to set to the loading icon.</param>
        public void UpdatePlaylistGeneratingProgressBarVisibility(ViewStates viewState)
        {
            playlistGenerating.Visibility = viewState;
        }

        /// <summary>
        /// Updates the playlist name label text.
        /// </summary>
        /// <param name="playlistName">The text to set for the playlist name label.</param>
        public void UpdatePlaylistName(string playlistName)
        {
            this.playlistName.Text = playlistName;
        }

        /// <summary>
        /// Updates the <see cref="MediaSession.QueueItem"/>s shown on the fragment.
        /// </summary>
        /// <param name="queueItems">List of current of <see cref="MediaSession.QueueItem"/> in the queue.</param>
        public void UpdateQueueItems(IList<MediaSession.QueueItem> queueItems)
        {
            currentQueueAdapter.UpdateQueueItems(queueItems);
        }

        /// <summary>
        /// Updates the value of the search view query text.
        /// </summary>
        /// <param name="queryText">The text to set in the search view.</param>
        public void UpdateSearchViewQuery(string queryText)
        {
            currentQueueSearchView.SetQuery(queryText, false);
        }

        /// <summary>
        /// Updates the shuffle button background based on the current shuffle mode.
        /// </summary>
        /// <param name="shuffleMode">True if shuffle mode is enabled.</param>
        public void UpdateShuffleButton(bool shuffleMode)
        {
            var background = shuffleMode ? Resource.Drawable.ic_shuffle_control_pressed : Resource.Drawable.ic_shuffle_control_default;
            shuffleButton.SetBackgroundResource(background);
        }

        /// <summary>
        /// Scrolls the queue to the item in the specified position.
        /// </summary>
        /// <param name="position">The position to scroll to.</param>
        public void ScrollToPosition(int position)
        {
            layoutManager.ScrollToPosition(position);
        }

        /// <summary>
        /// Displays a prompt to save the current playlist.
        /// </summary>
        /// <param name="playlistName">The name of the currently loaded playlist.</param>
        public void DisplayPromptToSaveQueueItems(string playlistName)
        {
            // Create input field and prompt
            var textField = new EditText(Activity);
            var prompt = new AlertDialog.Builder(Activity);

            // Build and show popup
            prompt.SetTitle(Application.Context.GetString(Resource.String.save_prompt_title));
            prompt.SetMessage(Application.Context.GetString(Resource.String.save_prompt_message));
            textField.SetText(playlistName, null);
            prompt.SetView(textField);
            prompt.SetNegativeButton(Application.Context.GetString(Resource.String.cancel), (sender, e) => { return; });
            prompt.SetPositiveButton(Application.Context.GetString(Resource.String.save), (sender, e) => { presenter.PromptToSaveQueueItemsContinue(textField.Text); });

            prompt.Show();
        }

        /// <summary>
        /// Displays a confirmation message if the saving the current playlist will overwrite an existing playlist.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to be overwritten.</param>
        public void DisplayOverwriteQueueItemsConfirmation(string playlistName)
        {
            // Create prompt
            var prompt = new AlertDialog.Builder(Activity);

            // Build and show popup
            prompt.SetTitle(Application.Context.GetString(Resource.String.save_confirm_prompt_title));
            prompt.SetMessage(Application.Context.GetString(Resource.String.save_confirm_prompt_message));
            prompt.SetNegativeButton(Application.Context.GetString(Resource.String.no), (sender, e) => { return; });
            prompt.SetPositiveButton(Application.Context.GetString(Resource.String.yes), (sender, e) => { presenter.SaveQueueItems(playlistName); });

            prompt.Show();
        }

        /// <summary>
        /// Displays a confirmation prompt to delete a playlist.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to delete.</param>
        public void DisplayDeleteQueueItemsConfirmation(string playlistName)
        {
            // Create prompt
            var prompt = new AlertDialog.Builder(Activity);

            // Build and show popup
            prompt.SetTitle(Application.Context.GetString(Resource.String.delete_prompt_title));
            prompt.SetMessage(string.Format(Application.Context.GetString(Resource.String.delete_prompt_message), playlistName));
            prompt.SetNegativeButton(Application.Context.GetString(Resource.String.no), (sender, e) => { return; });
            prompt.SetPositiveButton(Application.Context.GetString(Resource.String.yes), (sender, e) => { presenter.DeleteQueueItems(playlistName); });

            prompt.Show();
        }

        /// <summary>
        /// Displays an error message to the user.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        public void DisplayErrorMessage(string message)
        {
            // Create prompt
            var prompt = new AlertDialog.Builder(Activity);

            // Build and show popup
            prompt.SetTitle(Application.Context.GetString(Resource.String.error_prompt_title));
            prompt.SetMessage(message);
            prompt.SetPositiveButton(Application.Context.GetString(Resource.String.cancel), (sender, e) => { return; });

            prompt.Show();
        }

        /// <summary>
        /// Queue item click event handler.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="mediaId">Media ID of the track that was clicked.</param>
        private void OnItemClick(object sender, string mediaId)
        {
            musicBrowser.PlayFromMediaId(mediaId);
            presenter.UpdateOnItemClick(currentQueueAdapter.ActiveItemMediaId, mediaId);
        }

        /// <summary>
        /// View holder class for the current queue <see cref="RecyclerView"/>.
        /// </summary>
        public class CurrentQueueViewHolder : RecyclerView.ViewHolder, CurrentQueueItemTouch.IItemTouchViewHolder
        {
            private readonly MusicBrowser musicBrowser;
            private readonly MainActivity mainActivity;
            private ValueAnimator itemSelectedAnimator;

            /// <summary>
            /// Initializes a new instance of the <see cref="CurrentQueueViewHolder"/> class.
            /// </summary>
            /// <param name="view">Layout to use for each item.</param>
            /// <param name="musicBrowser">A connected instance of the <see cref="MusicBrowser"/>.</param>
            /// <param name="mainActivity">A reference to the <see cref="MainActivity"/>.</param>
            /// <param name="itemClickListener">Click listener.</param>
            public CurrentQueueViewHolder(View view, MusicBrowser musicBrowser, MainActivity mainActivity, Action<string> itemClickListener)
                : base(view)
            {
                this.musicBrowser = musicBrowser;
                this.mainActivity = mainActivity;

                QueueItemContainer = view.FindViewById<LinearLayout>(Resource.Id.queue_item_container);
                QueueItemThumbnail = view.FindViewById<ImageView>(Resource.Id.img_queue_item_thumbnail);
                QueueItemDuration = view.FindViewById<TextView>(Resource.Id.txt_queue_item_duration);
                QueueItemTitle = view.FindViewById<TextView>(Resource.Id.txt_queue_item_title);
                QueueItemChannel = view.FindViewById<TextView>(Resource.Id.txt_queue_item_channel);

                view.Click += (sender, e) => itemClickListener(MediaId);
            }

            public LinearLayout QueueItemContainer { get; private set; }

            public ImageView QueueItemThumbnail { get; private set; }

            public TextView QueueItemDuration { get; private set; }

            public TextView QueueItemTitle { get; private set; }

            public TextView QueueItemChannel { get; private set; }

            public string MediaId { get; set; }

            /// <summary>
            /// Creates and starts an animator to animate a color change of the item background when selected.
            /// </summary>
            public void OnItemSelected()
            {
                // Set up normal background and activated control colors for animations
                var themeManager = mainActivity.ThemeManager;
                var colorBackground = (ItemView.Background as ColorDrawable).Color.ToArgb();
                var colorActivated = themeManager.ColorControlActivated.ToArgb();

                // Initialize item drag activation animator
                itemSelectedAnimator = ValueAnimator.OfObject(new ArgbEvaluator(), colorBackground, colorActivated);
                itemSelectedAnimator.SetDuration(250);

                itemSelectedAnimator.Update += (object sender, ValueAnimator.AnimatorUpdateEventArgs e) =>
                {
                    ItemView.SetBackgroundColor(new Color((int)e.Animation.AnimatedValue));
                };

                itemSelectedAnimator.Start();
            }

            /// <summary>
            /// Called at the end of a drag action. Moves the selected item to its new position in the music queue and reverses the selection animation.
            /// </summary>
            public void OnItemClear()
            {
                // Move the item to its new adapter position in the music queue
                musicBrowser.MoveQueueItem(MediaId, AbsoluteAdapterPosition);

                // Reverse the item selection animation
                itemSelectedAnimator.Reverse();
            }
        }

        /// <summary>
        /// Listener class for updates on the <see cref="RecyclerView"/> scroll state.
        /// </summary>
        private class RecyclerViewOnScrollListener : RecyclerView.OnScrollListener
        {
            public Action<RecyclerView, int> OnScrollChangedAction { get; set; }

            public override void OnScrollStateChanged(RecyclerView recyclerView, int newState)
            {
                OnScrollChangedAction(recyclerView, newState);
                base.OnScrollStateChanged(recyclerView, newState);
            }
        }
    }
}