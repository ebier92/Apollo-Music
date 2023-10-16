// <copyright file="RecommendedFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Threading;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.ConstraintLayout.Widget;
using AndroidX.RecyclerView.Widget;
using Fragment = AndroidX.Fragment.App.Fragment;
using PopupMenu = AndroidX.AppCompat.Widget.PopupMenu;

namespace Apollo
{
    /// <summary>
    /// <see cref="Fragment"/> class to display recommended and previously selected videos.
    /// </summary>
    internal class RecommendedFragment : Fragment, IRecommendedFragment, MainActivity.INetworkLostObserver
    {
        private MainActivity mainActivity;
        private RecommendedFragmentPresenter presenter;
        private MusicBrowser musicBrowser;
        private ItemTouchHelper itemTouchHelper;
        private RecommendedItemTouch.ItemTouchCallback itemTouchCallback;
        private ImageView recommendedBackground;
        private TextView emptyRecommendationsMessage;
        private TextView recommendedContentItemLoadErrorMessage;
        private Button refreshButton;
        private Button retryButton;
        private ProgressBar recommendedContentLoading;
        private RecyclerView recommendedRecyclerView;
        private RecyclerView.LayoutManager layoutManager;
        private RecommendedAdapter recommendedAdapter;
        private int historicalTrackCount;
        private CancellationTokenSource cancellationTokenSource;

        public CancellationToken CancellationToken
        {
            get
            {
                // Return token from cancellation source if available, otherwise return an empty token
                if (cancellationTokenSource != null)
                    return cancellationTokenSource.Token;
                else
                    return CancellationToken.None;
            }
        }

        /// <summary>
        /// Inflates the <see cref="RecommendedFragment"/>, sets up a <see cref="RecylerView"/>, and retrieves a list of recommended and previously selected videos.
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
            presenter = new RecommendedFragmentPresenter(mainActivity, this, musicBrowser);
            cancellationTokenSource = new CancellationTokenSource();

            // Restart the app if the music browser is disconnected
            presenter.RestartIfMusicBrowserDisconnected();

            // Create recommended fragment
            var view = inflater.Inflate(Resource.Layout.fragment_recommended, container, false);

            // Find fragment UI elements
            recommendedBackground = view.FindViewById<ImageView>(Resource.Id.img_recommended_background);
            emptyRecommendationsMessage = view.FindViewById<TextView>(Resource.Id.txt_empty_recommended_items_message);
            recommendedContentItemLoadErrorMessage = view.FindViewById<TextView>(Resource.Id.txt_no_recommended_items_message);
            refreshButton = view.FindViewById<Button>(Resource.Id.btn_refresh_recommended);
            retryButton = view.FindViewById<Button>(Resource.Id.btn_rety_load_recommended_items);
            recommendedContentLoading = view.FindViewById<ProgressBar>(Resource.Id.progress_bar_recommended_loading);
            recommendedRecyclerView = view.FindViewById<RecyclerView>(Resource.Id.recycler_view_recommended);

            // Settings button
            refreshButton.Click += (sender, e) =>
            {
                presenter.LoadRecommendedContentItems();
            };

            // Retry button
            retryButton.Click += (sender, e) =>
            {
                presenter.LoadRecommendedContentItems();
            };

            // Set up layout manager
            layoutManager = new LinearLayoutManager(Application.Context);
            recommendedRecyclerView.SetLayoutManager(layoutManager);

            // Initialize the adapter
            recommendedAdapter = new RecommendedAdapter();
            recommendedAdapter.ItemClick += OnItemClick;
            recommendedAdapter.ButtonClick += OnButtonClick;
            recommendedAdapter.ItemMenuClick += OnItemMenuClick;
            recommendedRecyclerView.SetAdapter(recommendedAdapter);

            // Initialize item touch
            itemTouchCallback = new RecommendedItemTouch.ItemTouchCallback(recommendedAdapter);
            itemTouchHelper = new ItemTouchHelper(itemTouchCallback);
            itemTouchHelper.AttachToRecyclerView(recommendedRecyclerView);

            // Load items
            presenter.LoadRecommendedContentItems();

            // Save the number of tracks previously listened to by the user for reference
            historicalTrackCount = presenter.GetHistoricalTrackCount();

            return view;
        }

        /// <summary>
        /// Reinitialize the fragment if it is coming back from a hidden state.
        /// </summary>
        /// <param name="hidden">True if the fragment is being hidden.</param>
        public override void OnHiddenChanged(bool hidden)
        {
            // Reload recommendations if the queue has changed since the last refresh
            presenter.InitializeAfterUnhidden(hidden, historicalTrackCount);

            // Save the number of tracks previously listened to by the user for reference
            historicalTrackCount = presenter.UpdateHistoricalTrackCount(hidden, historicalTrackCount);

            base.OnHiddenChanged(hidden);
        }

        /// <summary>
        /// Updates the visibility state of the recommended content items loading icon.
        /// </summary>
        /// <param name="viewState">The view state to set to the recommended content items loading icon.</param>
        public void UpdateContentLoadingIconVisibility(ViewStates viewState)
        {
            recommendedContentLoading.Visibility = viewState;
        }

        /// <summary>
        /// Updates the visibility state of the recommended content <see cref="RecyclerView"/>.
        /// </summary>
        /// <param name="viewState">The view state to set to the recommended content items <see cref="RecyclerView"/>.</param>
        public void UpdateRecyclerViewAndRefreshButtonVisibility(ViewStates viewState)
        {
            recommendedRecyclerView.Visibility = viewState;
            refreshButton.Visibility = viewState;
        }

        /// <summary>
        /// Updates the visibility state of a message alerting the user that recommended page content could not be loaded.
        /// </summary>
        /// <param name="viewState">The view state to set to the message and retry button.</param>
        public void UpdateContentLoadErrorElementsVisibility(ViewStates viewState)
        {
            recommendedContentItemLoadErrorMessage.Visibility = viewState;
            retryButton.Visibility = viewState;
        }

        /// <summary>
        /// Updates the visibility state of the elements to show if there are no recommendations to display.
        /// </summary>
        /// <param name="viewState">The visibility state to set to the empty page elements.</param>
        public void UpdateEmptyPageElementsVisibility(ViewStates viewState)
        {
            emptyRecommendationsMessage.Visibility = viewState;
            recommendedBackground.Visibility = viewState;
        }

        /// <summary>
        /// Adds a single recommended content item to the adapter results list.
        /// </summary>
        /// <param name="recommendedContentItem">The <see cref="YouTube.ContentItem"/> to be added.</param>
        public void AddRecommendedContentItem(YouTube.ContentItem recommendedContentItem)
        {
            recommendedAdapter.AddRecommendedContentItem(recommendedContentItem);
        }

        /// <summary>
        /// Clears all recommended content items from the <see cref="RecyclerView"/>.
        /// </summary>
        public void ClearRecommendedContentItems()
        {
            recommendedAdapter.ClearRecommendedContentItems();
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
        /// Notification of when network connectivity has been lost.
        /// </summary>
        public void NotifyNetworkLost()
        {
            // Cancel all network dependent tasks
            cancellationTokenSource.Cancel();

            // Dispose and recreate the cancellation token source for future use
            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Recommended content item click event.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="recommendedContentItem">The search result item that was clicked.</param>
        private void OnItemClick(object sender, YouTube.ContentItem recommendedContentItem)
        {
            presenter.ItemClicked(recommendedContentItem);
        }

        /// <summary>
        /// Recommended content button click event.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="sectionHeaderText">The text of the section header.</param>
        private void OnButtonClick(object sender, string sectionHeaderText)
        {
            presenter.ButtonClicked(sectionHeaderText);
        }

        /// <summary>
        /// Called when the options menu is clicked for a recommended content item video.
        /// </summary>
        /// <param name="item">The menu item that was selected.</param>
        /// <param name="recommendedContentItem">The recommended content item where the options menu was selected.</param>
        private void OnItemMenuClick(IMenuItem item, YouTube.ContentItem recommendedContentItem)
        {
            presenter.ItemMenuClicked(item.ItemId, recommendedContentItem);
        }

        /// <summary>
        /// View holder class for the recommended <see cref="RecyclerView"/>.
        /// </summary>
        public class RecommendedViewHolder : RecyclerView.ViewHolder, View.IOnClickListener, RecommendedItemTouch.IItemTouchViewHolder, PopupMenu.IOnMenuItemClickListener
        {
            private readonly Action<IMenuItem, YouTube.ContentItem> itemMenuClickListener;

            /// <summary>
            /// Initializes a new instance of the <see cref="RecommendedViewHolder"/> class.
            /// </summary>
            /// <param name="view">Layout to use for each item.</param>
            /// <param name="itemClickListener">Item click listener.</param>
            /// <param name="playMixButtonClickListener">Play mix button click listener.</param>
            /// <param name="itemMenuClickListener">Item options menu click listener.</param>
            public RecommendedViewHolder(View view, Action<YouTube.ContentItem> itemClickListener, Action<string> playMixButtonClickListener, Action<IMenuItem, YouTube.ContentItem> itemMenuClickListener)
                : base(view)
            {
                RecommendedContentItemSectionHeaderContainer = view.FindViewById<LinearLayout>(Resource.Id.recommended_item_section_header_container);
                RecommendedContentItemSectionHeader = view.FindViewById<TextView>(Resource.Id.recommended_item_section_header);
                RecommendedContentItemPlayMixButton = view.FindViewById<Button>(Resource.Id.btn_play_mix);
                RecommendedContentItemContentContainer = view.FindViewById<ConstraintLayout>(Resource.Id.recommended_item_content_container);
                RecommendedContentItemThumbnail = view.FindViewById<ImageView>(Resource.Id.img_recommended_item_thumbnail);
                RecommendedContentItemPlaylistIcon = view.FindViewById<ImageView>(Resource.Id.img_recommended_item_playlist_icon);
                RecommendedContentItemDuration = view.FindViewById<TextView>(Resource.Id.txt_recommended_item_duration);
                RecommendedContentItemTitle = view.FindViewById<TextView>(Resource.Id.txt_recommended_item_title);
                RecommendedContentItemDescription = view.FindViewById<TextView>(Resource.Id.txt_recommended_item_description);
                RecommendedContentItemPopupButton = view.FindViewById<ImageButton>(Resource.Id.btn_recommended_item_popup);

                view.Click += (sender, e) => itemClickListener(RecommendedContentItem);
                RecommendedContentItemPlayMixButton.Click += (sender, e) => playMixButtonClickListener(RecommendedContentItemSectionHeader.Text);
                RecommendedContentItemPopupButton.SetOnClickListener(this);
                this.itemMenuClickListener = itemMenuClickListener;
            }

            public LinearLayout RecommendedContentItemSectionHeaderContainer { get; private set; }

            public TextView RecommendedContentItemSectionHeader { get; private set; }

            public Button RecommendedContentItemPlayMixButton { get; private set; }

            public ConstraintLayout RecommendedContentItemContentContainer { get; private set; }

            public ImageView RecommendedContentItemThumbnail { get; private set; }

            public ImageView RecommendedContentItemPlaylistIcon { get; private set; }

            public TextView RecommendedContentItemDuration { get; private set; }

            public TextView RecommendedContentItemTitle { get; private set; }

            public TextView RecommendedContentItemDescription { get; private set; }

            public ImageButton RecommendedContentItemPopupButton { get; private set; }

            public YouTube.ContentItem RecommendedContentItem { get; set; }

            /// <summary>
            /// Opens the options menu for a specific recommended content item when the options button is clicked.
            /// </summary>
            /// <param name="view">The <see cref="View"/> of the search result where the options button was clicked.</param>
            public void OnClick(View view)
            {
                var popupMenu = new PopupMenu(view.Context, view, (int)GravityFlags.Right);
                popupMenu.Inflate(Resource.Menu.content_item_popup);
                popupMenu.SetOnMenuItemClickListener(this);
                popupMenu.Show();
            }

            /// <summary>
            /// Triggers the menu item click listener that a specific menu item option was clicked.
            /// </summary>
            /// <param name="item">The menu item that was clicked.</param>
            /// <returns>True.</returns>
            public bool OnMenuItemClick(IMenuItem item)
            {
                itemMenuClickListener(item, RecommendedContentItem);

                return true;
            }

            /// <summary>
            /// Called when an item touch is initiated.
            /// </summary>
            public void OnItemSelected()
            {
                // Not implemented
            }

            /// <summary>
            /// Called at the end of a drag or swipe action.
            /// </summary>
            public void OnItemClear()
            {
                // Not implemented
            }
        }
    }
}