// <copyright file="HomeFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Threading;
using Android.App;
using Android.Content;
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
    /// <see cref="Fragment"/> class to display popular playlist and video items to the user when the app starts.
    /// </summary>
    internal class HomeFragment : Fragment, MainActivity.INetworkLostObserver, IHomeFragment
    {
        private MainActivity mainActivity;
        private HomeFragmentPresenter presenter;
        private MusicBrowser musicBrowser;
        private TextView homeContentItemLoadErrorMessage;
        private Button settingsButton;
        private Button retryHomeContentItemLoadButton;
        private ProgressBar homeContentItemsLoading;
        private RecyclerView homeRecyclerView;
        private RecyclerView.LayoutManager layoutManager;
        private HomeAdapter homeAdapter;
        private CancellationTokenSource cancellationTokenSource;

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
        /// Inflates the <see cref="HomeFragment"/>, sets up a <see cref="RecylerView"/>, and retrieves a list of popular playlists and videos.
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
            presenter = new HomeFragmentPresenter(mainActivity, this, musicBrowser);
            cancellationTokenSource = new CancellationTokenSource();

            // Create home fragment
            var view = inflater.Inflate(Resource.Layout.fragment_home, container, false);

            // Find fragment UI elements
            homeContentItemLoadErrorMessage = view.FindViewById<TextView>(Resource.Id.txt_no_home_items_message);
            settingsButton = view.FindViewById<Button>(Resource.Id.btn_settings);
            retryHomeContentItemLoadButton = view.FindViewById<Button>(Resource.Id.btn_rety_load_home_items);
            homeContentItemsLoading = view.FindViewById<ProgressBar>(Resource.Id.progress_bar_home_loading);
            homeRecyclerView = view.FindViewById<RecyclerView>(Resource.Id.recycler_view_home);

            // Settings button
            settingsButton.Click += (sender, e) =>
            {
                presenter.DisplaySettingsPrompt();
            };

            // Retry home content item load button
            retryHomeContentItemLoadButton.Click += (sender, e) =>
            {
                presenter.LoadHomeContentItems();
            };

            // Set up layout manager
            layoutManager = new LinearLayoutManager(Application.Context);
            homeRecyclerView.SetLayoutManager(layoutManager);

            // Initialize the adapter
            homeAdapter = new HomeAdapter();
            homeAdapter.ItemClick += OnItemClick;
            homeAdapter.ItemMenuClick += OnItemMenuClick;
            homeRecyclerView.SetAdapter(homeAdapter);

            // Load items for the home screen
            presenter.LoadHomeContentItems();

            return view;
        }

        /// <summary>
        /// Updates the visibility state of the home content items loading icon.
        /// </summary>
        /// <param name="viewState">The view state to set to the home content items loading icon.</param>
        public void UpdateHomeContentItemsLoadingIconVisibility(ViewStates viewState)
        {
            homeContentItemsLoading.Visibility = viewState;
        }

        /// <summary>
        /// Updates the visibility state of a message alerting the user that home page content could not be loaded along with a button to retry the loading process.
        /// </summary>
        /// <param name="viewState">The view state to set to the message and retry button.</param>
        public void UpdateRetryHomeContentItemsLoadItemsVisibility(ViewStates viewState)
        {
            homeContentItemLoadErrorMessage.Visibility = viewState;
            retryHomeContentItemLoadButton.Visibility = viewState;
        }

        /// <summary>
        /// Adds a single home content item to the adapter results list.
        /// </summary>
        /// <param name="homeContentItem">The <see cref="YouTube.ContentItem"/> to be added.</param>
        public void AddHomeContentItem(YouTube.ContentItem homeContentItem)
        {
            homeAdapter.AddHomeContentItem(homeContentItem);
        }

        /// <summary>
        /// Displays the app settings prompt.
        /// </summary>
        public void DisplaySettingsPrompt()
        {
            // Inflate settings menu layout
            var inflater = (LayoutInflater)Activity.GetSystemService(Context.LayoutInflaterService);
            var view = inflater.Inflate(Resource.Layout.settings_menu, null);

            // Get UI elements
            var clearSearchHistoryButton = view.FindViewById<Button>(Resource.Id.btn_clear_search_history);
            var clearRecommendationDataButton = view.FindViewById<Button>(Resource.Id.btn_clear_recommendation_data);
            var clearPlaylistsButton = view.FindViewById<Button>(Resource.Id.btn_clear_playlists);
            var exportAppDataButton = view.FindViewById<Button>(Resource.Id.btn_export_data);
            var importAppDataButton = view.FindViewById<Button>(Resource.Id.btn_import_data);
            var streamQualitySpinner = view.FindViewById<Spinner>(Resource.Id.spinner_stream_quality);
            var playlistSourceSpinner = view.FindViewById<Spinner>(Resource.Id.spinner_playlist_source);

            // Clear search history button
            clearSearchHistoryButton.Click += (sender, e) =>
            {
                presenter.PromptClearSearchHistory();
            };

            // Clear recomendation data button
            clearRecommendationDataButton.Click += (sender, e) =>
            {
                presenter.PromptClearRecommendationData();
            };

            // Clear playlists button
            clearPlaylistsButton.Click += (sender, e) =>
            {
                presenter.PromptClearPlaylists();
            };

            // Import app data button
            exportAppDataButton.Click += (sender, e) =>
            {
                presenter.PromptExportAppData();
            };

            // Export app data button
            importAppDataButton.Click += (sender, e) =>
            {
                presenter.PromptImportAppData();
            };

            // Build options lists for dropdowns
            var streamQualityOptions = new string[]
            {
                Application.Context.GetString(Resource.String.low),
                Application.Context.GetString(Resource.String.medium),
                Application.Context.GetString(Resource.String.high),
            };

            var playlistSourceOptions = new string[]
            {
                Application.Context.GetString(Resource.String.related_music),
                Application.Context.GetString(Resource.String.related_videos),
            };

            // Map current settings to default selected spinner item
            var selectedStreamQualtity = SettingsManager.StreamQualitySetting switch
            {
                SettingsManager.StreamQualitySettingOptions.Low => 0,
                SettingsManager.StreamQualitySettingOptions.Medium => 1,
                SettingsManager.StreamQualitySettingOptions.High => 2,
                _ => 0,
            };

            var selectedPlaylistSource = SettingsManager.PlaylistSourceSetting switch
            {
                SettingsManager.PlaylistSourceSettingOptions.YouTubeMusic => 0,
                SettingsManager.PlaylistSourceSettingOptions.YouTube => 1,
                _ => 0,
            };

            // Set options dropdowns
            var streamQualityOptionsAdapter = new ArrayAdapter<string>(Activity, Resource.Layout.support_simple_spinner_dropdown_item, streamQualityOptions);
            streamQualityOptionsAdapter.SetDropDownViewResource(Resource.Layout.support_simple_spinner_dropdown_item);
            streamQualitySpinner.Adapter = streamQualityOptionsAdapter;
            streamQualitySpinner.SetSelection(selectedStreamQualtity);
            streamQualitySpinner.ItemSelected += (sender, e) => { presenter.SetStreamQuality(e.Position); };

            var playlistSourceOptionsAdapter = new ArrayAdapter<string>(Activity, Resource.Layout.support_simple_spinner_dropdown_item, playlistSourceOptions);
            playlistSourceOptionsAdapter.SetDropDownViewResource(Resource.Layout.support_simple_spinner_dropdown_item);
            playlistSourceSpinner.Adapter = playlistSourceOptionsAdapter;
            playlistSourceSpinner.SetSelection(selectedPlaylistSource);
            playlistSourceSpinner.ItemSelected += (sender, e) => { presenter.SetPlaylistSource(e.Position); };

            // Create settings menu button
            var settingsMenu = new AlertDialog.Builder(Activity);

            // Build and show popup
            settingsMenu.SetView(view);

            settingsMenu.Show();
        }

        /// <summary>
        /// Displays a confirmation message with the specified message and calls the specified event handler when the positive button is clicked.
        /// </summary>
        /// <param name="messageStringId">The resource ID of the message string to display to the user.</param>
        /// <param name="positiveButtonAction">The action to perform when the positive button is clicked.</param>
        public void DisplayConfirmationPrompt(int messageStringId, Action positiveButtonAction)
        {
            // Create prompt
            var prompt = new AlertDialog.Builder(Activity);

            // Build and show popup
            prompt.SetTitle(Application.Context.GetString(Resource.String.confirm));
            prompt.SetMessage(Application.Context.GetString(messageStringId));
            prompt.SetNegativeButton(Application.Context.GetString(Resource.String.no), (sender, e) => { return; });
            prompt.SetPositiveButton(Application.Context.GetString(Resource.String.yes), (sender, e) => { positiveButtonAction(); });

            prompt.Show();
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
        /// Home content item click event.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="homeContentItem">The search result item that was clicked.</param>
        private void OnItemClick(object sender, YouTube.ContentItem homeContentItem)
        {
            presenter.ItemClicked(homeContentItem);
        }

        /// <summary>
        /// Called when the options menu is clicked for a home content item video.
        /// </summary>
        /// <param name="item">The menu item that was selected.</param>
        /// <param name="homeContentItem">The home content item where the options menu was selected.</param>
        private void OnItemMenuClick(IMenuItem item, YouTube.ContentItem homeContentItem)
        {
            presenter.ItemMenuClicked(item.ItemId, homeContentItem);
        }

        /// <summary>
        /// View holder class for the home <see cref="RecyclerView"/>.
        /// </summary>
        public class HomeViewHolder : RecyclerView.ViewHolder, View.IOnClickListener, PopupMenu.IOnMenuItemClickListener
        {
            private readonly Action<IMenuItem, YouTube.ContentItem> itemMenuClickListener;

            /// <summary>
            /// Initializes a new instance of the <see cref="HomeViewHolder"/> class.
            /// </summary>
            /// <param name="view">Layout to use for each item.</param>
            /// <param name="itemClickListener">Item click listener.</param>
            /// <param name="itemMenuClickListener">Item options menu click listener.</param>
            public HomeViewHolder(View view, Action<YouTube.ContentItem> itemClickListener, Action<IMenuItem, YouTube.ContentItem> itemMenuClickListener)
                : base(view)
            {
                HomeContentItemSectionHeaderContainer = view.FindViewById<LinearLayout>(Resource.Id.home_item_section_header_container);
                HomeContentItemSectionHeader = view.FindViewById<TextView>(Resource.Id.home_item_section_header);
                HomeContentItemContentContainer = view.FindViewById<ConstraintLayout>(Resource.Id.home_item_content_container);
                HomeContentItemThumbnail = view.FindViewById<ImageView>(Resource.Id.img_home_item_thumbnail);
                HomeContentItemPlaylistIcon = view.FindViewById<ImageView>(Resource.Id.img_home_item_playlist_icon);
                HomeContentItemDuration = view.FindViewById<TextView>(Resource.Id.txt_home_item_duration);
                HomeContentItemTitle = view.FindViewById<TextView>(Resource.Id.txt_home_item_title);
                HomeContentItemDescription = view.FindViewById<TextView>(Resource.Id.txt_home_item_description);
                HomeContentItemPopupButton = view.FindViewById<ImageButton>(Resource.Id.btn_home_item_popup);
                view.Click += (sender, e) => itemClickListener(HomeContentItem);
                HomeContentItemPopupButton.SetOnClickListener(this);
                this.itemMenuClickListener = itemMenuClickListener;
            }

            public LinearLayout HomeContentItemSectionHeaderContainer { get; private set; }

            public TextView HomeContentItemSectionHeader { get; private set; }

            public ConstraintLayout HomeContentItemContentContainer { get; private set; }

            public ImageView HomeContentItemThumbnail { get; private set; }

            public ImageView HomeContentItemPlaylistIcon { get; private set; }

            public TextView HomeContentItemDuration { get; private set; }

            public TextView HomeContentItemTitle { get; private set; }

            public TextView HomeContentItemDescription { get; private set; }

            public ImageButton HomeContentItemPopupButton { get; private set; }

            public YouTube.ContentItem HomeContentItem { get; set; }

            /// <summary>
            /// Opens the options menu for a specific home content item when the options button is clicked.
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
                itemMenuClickListener(item, HomeContentItem);

                return true;
            }
        }
    }
}