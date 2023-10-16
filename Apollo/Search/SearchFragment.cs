// <copyright file="SearchFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Fragment = AndroidX.Fragment.App.Fragment;
using PopupMenu = AndroidX.AppCompat.Widget.PopupMenu;
using SearchView = Android.Widget.SearchView;

namespace Apollo
{
    /// <summary>
    /// <see cref="Fragment"/> class to search for music and display matching results.
    /// </summary>
    internal class SearchFragment : Fragment, ISearchFragment, MainActivity.INetworkLostObserver
    {
        private MainActivity mainActivity;
        private SearchFragmentPresenter presenter;
        private SearchView searchInput;
        private Button searchSongsButton;
        private Button searchAlbumsButton;
        private Button searchFeaturedPlaylistsButton;
        private Button searchCommunityPlaylistsButton;
        private Button searchGeneralButton;
        private ImageView searchBackground;
        private RecyclerView searchResultsRecyclerView;
        private RecyclerView.LayoutManager layoutManager;
        private ProgressBar initialSearchResultsBuffering;
        private ProgressBar additionalSearchResultsBuffering;
        private SearchAdapter searchAdapter;
        private CancellationTokenSource cancellationTokenSource;
        private string query;

        public string ContinuationToken { get; set; }

        public CancellationToken CancellationToken
        {
            get
            {
                // Return token from cancellation token source if available, otherwise return an empty token
                if (cancellationTokenSource != null)
                    return cancellationTokenSource.Token;
                else
                    return CancellationToken.None;
            }
        }

        /// <summary>
        /// Inflates and initializes the <see cref="SearchFragment"/>.
        /// </summary>
        /// <param name="inflater">A <see cref="LayoutInflater"/> to inflate the view.</param>
        /// <param name="container">A containing <see cref="ViewGroup"/>.</param>
        /// <param name="savedInstanceState">A <see cref="Bundle"/> representing the previousy saved state.</param>
        /// <returns>Inflated <see cref="View"/>.</returns>
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Initialize objects
            mainActivity = (MainActivity)Activity;
            presenter = new SearchFragmentPresenter(mainActivity, this, mainActivity.MusicBrowser);
            cancellationTokenSource = new CancellationTokenSource();

            // Create Search fragment
            var view = inflater.Inflate(Resource.Layout.fragment_search, container, false);

            // Find fragment UI elements
            searchInput = view.FindViewById<SearchView>(Resource.Id.search_view_tracks);
            searchSongsButton = view.FindViewById<Button>(Resource.Id.btn_filter_songs);
            searchAlbumsButton = view.FindViewById<Button>(Resource.Id.btn_filter_albums);
            searchFeaturedPlaylistsButton = view.FindViewById<Button>(Resource.Id.btn_filter_featured_playlists);
            searchCommunityPlaylistsButton = view.FindViewById<Button>(Resource.Id.btn_filter_community_playlists);
            searchGeneralButton = view.FindViewById<Button>(Resource.Id.btn_filter_general);
            searchBackground = view.FindViewById<ImageView>(Resource.Id.img_search_background);
            searchResultsRecyclerView = view.FindViewById<RecyclerView>(Resource.Id.recycler_view_track_search_results);
            initialSearchResultsBuffering = view.FindViewById<ProgressBar>(Resource.Id.progress_bar_initial_results_loading);
            additionalSearchResultsBuffering = view.FindViewById<ProgressBar>(Resource.Id.progress_bar_additional_results_loading);

            // Songs filter button
            searchSongsButton.Click += (sender, e) =>
            {
                presenter.SetSearchOption(1, searchInput.Query);
            };

            // Search albums button
            searchAlbumsButton.Click += (sender, e) =>
            {
                presenter.SetSearchOption(2, searchInput.Query);
            };

            // Search featured playlists button
            searchFeaturedPlaylistsButton.Click += (sender, e) =>
            {
                presenter.SetSearchOption(3, searchInput.Query);
            };

            // Search community playlists button
            searchCommunityPlaylistsButton.Click += (sender, e) =>
            {
                presenter.SetSearchOption(4, searchInput.Query);
            };

            // Search general button
            searchGeneralButton.Click += (sender, e) =>
            {
                presenter.SetSearchOption(0, searchInput.Query);
            };

            // Set on scroll listener
            var onScrollListener = new RecyclerViewOnScrollListener
            {
                OnScrollChangedAction = (recyclerView, newState) =>
                {
                    presenter.CheckScrollChange(recyclerView, newState);
                },
            };

            searchResultsRecyclerView.AddOnScrollListener(onScrollListener);

            // Set up a linear layout manager
            layoutManager = new LinearLayoutManager(Application.Context);
            searchResultsRecyclerView.SetLayoutManager(layoutManager);

            // Set up the search manager
            var searchManager = (SearchManager)Context.GetSystemService(Context.SearchService);

            // Set up search view
            searchInput.SetSearchableInfo(searchManager.GetSearchableInfo(Activity.ComponentName));
            searchInput.SetIconifiedByDefault(false);
            searchInput.SubmitButtonEnabled = true;
            searchInput.QueryRefinementEnabled = true;

            return view;
        }

        /// <summary>
        /// Initialize search option buttons on start.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            presenter.InitializeSearchOptions();
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
        /// Triggered when a new query is entered by the user.
        /// </summary>
        /// <param name="query">Query string to search by.</param>
        public void OnQuery(string query)
        {
            // Set query text and initialize the key and continuation token
            this.query = query;

            // Initialize a new search adapter to display incoming results
            searchAdapter = new SearchAdapter();
            searchAdapter.ItemClick += OnItemClick;
            searchAdapter.ItemMenuClick += OnItemMenuClick;
            searchResultsRecyclerView.SetAdapter(searchAdapter);

            // Execute the search
            presenter.Query(query);
        }

        /// <summary>
        /// Triggered when the search results reaches the bottom of the last page.
        /// </summary>
        public void OnScrollToBottom()
        {
            presenter.QueryAdditionalResultsPage(query, ContinuationToken);
        }

        /// <summary>
        /// Updates the visibility state for the additional search results buffering icon at the bottom of the page.
        /// </summary>
        /// <param name="viewState">The view state to set to the buffering icon.</param>
        public void UpdateAdditionalSearchResultsBufferingIconVisibility(ViewStates viewState)
        {
            additionalSearchResultsBuffering.Visibility = viewState;
        }

        /// <summary>
        /// Updates the visibility state for the initial search results buffering icon in the middle of the page.
        /// </summary>
        /// <param name="viewState">The view state to set to the buffering icon.</param>
        public void UpdateInitialSearchResultsBufferingIconVisibility(ViewStates viewState)
        {
            initialSearchResultsBuffering.Visibility = viewState;
        }

        /// <summary>
        /// Updates the text written in the search box.
        /// </summary>
        /// <param name="query">The query text to set to the search box.</param>
        public void UpdateSearchQueryText(string query)
        {
            if (searchInput != null)
                searchInput.SetQuery(query, false);
        }

        /// <summary>
        /// Sets a specific filter control button to an active state and all other buttons to the inactive state.
        /// </summary>
        /// <param name="searchOption">The user selected search option (0 for "songs", 1 for "albums", 2 for "featured playlists", 3 for "community playlists", 4 for "all").</param>
        public void UpdateFilterButtonState(int searchOption)
        {
            searchGeneralButton.SetBackgroundResource(searchOption == 0 ? Resource.Drawable.ic_standard_button_activated : Resource.Drawable.ic_standard_button_default);
            searchSongsButton.SetBackgroundResource(searchOption == 1 ? Resource.Drawable.ic_standard_button_activated : Resource.Drawable.ic_standard_button_default);
            searchAlbumsButton.SetBackgroundResource(searchOption == 2 ? Resource.Drawable.ic_standard_button_activated : Resource.Drawable.ic_standard_button_default);
            searchFeaturedPlaylistsButton.SetBackgroundResource(searchOption == 3 ? Resource.Drawable.ic_standard_button_activated : Resource.Drawable.ic_standard_button_default);
            searchCommunityPlaylistsButton.SetBackgroundResource(searchOption == 4 ? Resource.Drawable.ic_standard_button_activated : Resource.Drawable.ic_standard_button_default);
        }

        /// <summary>
        /// Updates the visibility state for the search fragment background icon.
        /// </summary>
        /// <param name="viewState">The view state to set to the search background icon.</param>
        public void UpdateSearchBackgroundVisibility(ViewStates viewState)
        {
            searchBackground.Visibility = viewState;
        }

        /// <summary>
        /// Adds a single search result content item to the adapter results list.
        /// </summary>
        /// <param name="searchResultContentItem">The item to be added.</param>
        public void AddSearchResultContentItem(YouTube.ContentItem searchResultContentItem)
        {
            searchAdapter.AddSearchResultContentItem(searchResultContentItem);
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
        /// Focuses off the search box and on to the background.
        /// </summary>
        public void RequestBackgroundFocus()
        {
            searchResultsRecyclerView.RequestFocus();
        }

        /// <summary>
        /// Focuses to the search box and opens the keyboard for input.
        /// </summary>
        public void RequestSearchViewFocus()
        {
            // Exit if the music search has not yet been initialzed
            if (searchInput == null)
                return;

            // Focus to music search
            searchInput.RequestFocus();

            // Open keyboard
            var inputMethodManager = Context.GetSystemService(Context.InputMethodService) as InputMethodManager;
            inputMethodManager.ShowSoftInput(searchInput, ShowFlags.Forced);
        }

        /// <summary>
        /// Search result content item click event.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="searchResultContentItem">The search result content item that was clicked.</param>
        private void OnItemClick(object sender, YouTube.ContentItem searchResultContentItem)
        {
            presenter.ItemClicked(searchResultContentItem);
        }

        /// <summary>
        /// Called when the options menu is clicked for a search result content item video.
        /// </summary>
        /// <param name="item">The menu item that was selected.</param>
        /// <param name="searchResultContentItem">The search result content item where the options menu was selected.</param>
        private void OnItemMenuClick(IMenuItem item, YouTube.ContentItem searchResultContentItem)
        {
            presenter.ItemMenuClicked(item.ItemId, searchResultContentItem);
        }

        /// <summary>
        /// View holder class for the current queue <see cref="RecyclerView"/>.
        /// </summary>
        public class SearchViewHolder : RecyclerView.ViewHolder, View.IOnClickListener, PopupMenu.IOnMenuItemClickListener
        {
            private readonly Action<IMenuItem, YouTube.ContentItem> itemMenuClickListener;

            /// <summary>
            /// Initializes a new instance of the <see cref="SearchViewHolder"/> class.
            /// </summary>
            /// <param name="view">Layout to use for each item.</param>
            /// <param name="itemClickListener">Item click listener.</param>
            /// <param name="itemMenuClickListener">Item options menu click listener.</param>
            public SearchViewHolder(View view, Action<YouTube.ContentItem> itemClickListener, Action<IMenuItem, YouTube.ContentItem> itemMenuClickListener)
                : base(view)
            {
                SearchResultContentItemThumbnail = view.FindViewById<ImageView>(Resource.Id.img_search_result_item_thumbnail);
                SearchResultContentItemPlaylistIcon = view.FindViewById<ImageView>(Resource.Id.img_search_result_playlist_icon);
                SearchResultContentItemDuration = view.FindViewById<TextView>(Resource.Id.txt_search_result_item_duration);
                SearchResultContentItemTitle = view.FindViewById<TextView>(Resource.Id.txt_search_result_item_title);
                SearchResultContentItemChannel = view.FindViewById<TextView>(Resource.Id.txt_search_result_item_channel);
                SearchResultContentItemPopupButton = view.FindViewById<ImageButton>(Resource.Id.btn_search_result_item_popup);
                view.Click += (sender, e) => itemClickListener(SearchResultContentItem);
                SearchResultContentItemPopupButton.SetOnClickListener(this);
                this.itemMenuClickListener = itemMenuClickListener;
            }

            public ImageView SearchResultContentItemThumbnail { get; private set; }

            public ImageView SearchResultContentItemPlaylistIcon { get; private set; }

            public TextView SearchResultContentItemDuration { get; private set; }

            public TextView SearchResultContentItemTitle { get; private set; }

            public TextView SearchResultContentItemChannel { get; private set; }

            public ImageButton SearchResultContentItemPopupButton { get; private set; }

            public YouTube.ContentItem SearchResultContentItem { get; set; }

            /// <summary>
            /// Opens the options menu for a specific search results item when the options button is clicked.
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
                itemMenuClickListener(item, SearchResultContentItem);

                return true;
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