﻿using Autofac;
using Common;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Werd.Common;
using Werd.Managers;
using Werd.Networking;
using Werd.Settings;
using Werd.Views;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using IContainer = Autofac.IContainer;

namespace Werd
{
	public sealed partial class Shell : INotifyPropertyChanged
	{
		#region NPC
		/// <summary>
		/// Multicast event for property change notifications.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Checks if a property already matches a desired value.  Sets the property and
		/// notifies listeners only when necessary.
		/// </summary>
		/// <typeparam name="T">Type of the property.</typeparam>
		/// <param name="storage">Reference to a property with both getter and setter.</param>
		/// <param name="value">Desired value for the property.</param>
		/// <param name="propertyName">Name of the property used to notify listeners.  This
		///     value is optional and can be provided automatically when invoked from compilers that
		///     support CallerMemberName.</param>
		/// <returns>True if the value was changed, false if the existing value matched the
		/// desired value.</returns>
		private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(storage, value)) return;

			storage = value;
			OnPropertyChanged(propertyName);
		}

		/// <summary>
		/// Notifies listeners that a property value has changed.
		/// </summary>
		/// <param name="propertyName">Name of the property used to notify listeners.  This
		/// value is optional and can be provided automatically when invoked from compilers
		/// that support <see cref="CallerMemberNameAttribute"/>.</param>
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var eventHandler = PropertyChanged;
			if (eventHandler != null)
			{
				eventHandler(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		#endregion

		private const int LINK_POPUP_TIMEOUT = 10000;

		#region Private Variables

		readonly IContainer _container;
		Uri _embeddedBrowserLink;
		ShellView _currentlyDisplayedView;
		CoreWindow _keyBindingWindow;
		WebView _embeddedBrowser;
		MediaElement _embeddedMediaPlayer;
		readonly DispatcherTimer _popupTimer = new DispatcherTimer();
		DateTime _linkPopupExpireTime;

		#endregion

		private string npcCurrentViewName = "";
		public string CurrentViewName
		{
			get => npcCurrentViewName;
			set => SetProperty(ref npcCurrentViewName, value);
		}


		private ChattyManager npcChattyManager;
		public ChattyManager ChattyManager
		{
			get => npcChattyManager;
			set => SetProperty(ref npcChattyManager, value);
		}

		private MessageManager npcMessageManager;
		public MessageManager MessageManager
		{
			get => npcMessageManager;
			set => SetProperty(ref npcMessageManager, value);
		}

		private AuthenticationManager npcAuthManager;
		public AuthenticationManager AuthManager
		{
			get => npcAuthManager;
			set => SetProperty(ref npcAuthManager, value);
		}

		private LatestChattySettings npcSettings;
		public LatestChattySettings Settings
		{
			get => npcSettings;
			set => SetProperty(ref npcSettings, value);
		}

		private NetworkConnectionStatus npcConnectionStatus;
		public NetworkConnectionStatus ConnectionStatus
		{
			get => npcConnectionStatus;
			set => SetProperty(ref npcConnectionStatus, value);
		}

		#region Constructor
		public Shell(string initialNavigation, IContainer container)
		{
			InitializeComponent();

			ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(400, 400));
			_container = container;
			MessageManager = _container.Resolve<MessageManager>();
			AuthManager = _container.Resolve<AuthenticationManager>();
			Settings = _container.Resolve<LatestChattySettings>();
			ChattyManager = _container.Resolve<ChattyManager>();
			ConnectionStatus = _container.Resolve<NetworkConnectionStatus>();
			ConnectionStatus.PropertyChanged += ConnectionStatus_PropertyChanged;
			Settings.PropertyChanged += Settings_PropertyChanged;
			App.Current.UnhandledException += UnhandledAppException;

			SetThemeColor();

			Window.Current.Activated += WindowActivated;
			SystemNavigationManager.GetForCurrentView().BackRequested += (
				async (o, a) =>
				{
					await AppGlobal.DebugLog.AddMessage("Shell-HardwareBackButtonPressed").ConfigureAwait(true);
					a.Handled = await NavigateBack().ConfigureAwait(true);
				});
			CoreWindow.GetForCurrentThread().PointerPressed += async (sender, args) =>
			{
				if (args.CurrentPoint.Properties.IsXButton1Pressed) args.Handled = await NavigateBack().ConfigureAwait(true);
			};

			NavigateToTag(initialNavigation);
		}

		//private async void FocusManager_LosingFocus(object sender, LosingFocusEventArgs e)
		//{
		//	await AppGlobal.DebugLog.AddMessage($"LostFocus: CorId [{e.CorrelationId}] - NewElement [{e.NewFocusedElement?.GetType().Name}] LastElement [{e.OldFocusedElement?.GetType().Name}] State [{e.FocusState}] InputDevice [{e.InputDevice}]").ConfigureAwait(true);

		//}

		private void UnhandledAppException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
		{
			//Tooltips are throwing exceptions when the control they're bound to goes away.
			// This isn't detrimental to the application functionality so... ignore them.
			var stackTrace = e.Exception.StackTrace;
			if (!e.Message.StartsWith("The text associated with this error code could not be found.", StringComparison.InvariantCulture))
			{
				Sv_ShellMessage(this,
					new ShellMessageEventArgs("Uh oh. Things may not work right from this point forward. We don't know what happened."
					+ Environment.NewLine + "Restarting the application may help."
					+ Environment.NewLine + "Message: " + e.Message,
					ShellMessageType.Error));
			}
			Task.Run(() => AppGlobal.DebugLog.AddMessage($"UNHANDLED EXCEPTION: {e.Message + Environment.NewLine + stackTrace}"));
			e.Handled = true;
		}

		private async Task<bool> NavigateBack()
		{
			var handled = false;
			if (_embeddedBrowserLink != null)
			{
				if (EmbeddedViewer.Visibility == Visibility.Visible)
				{
					if (_embeddedBrowser.CanGoBack)
					{
						_embeddedBrowser.GoBack();
					}
					else
					{
						await CloseEmbeddedBrowser().ConfigureAwait(true);
					}
					handled = true;
				}
			}
			if (!handled)
			{
				handled = GoBack();
			}

			return handled;
		}

		private void ConnectionStatus_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var status = sender as NetworkConnectionStatus;
			if (status == null) return;
			if (!status.IsConnected)
			{
				Sv_ShellMessage(this, new ShellMessageEventArgs(status.MessageDetails, ShellMessageType.Error));
			}
		}
		#endregion

		private async void WindowActivated(object sender, WindowActivatedEventArgs e)
		{
			await ShowChattyClipboardLinkOpen(e).ConfigureAwait(true);
		}

		private async Task ShowChattyClipboardLinkOpen(WindowActivatedEventArgs e)
		{
			if (e.WindowActivationState == CoreWindowActivationState.Deactivated) { return; }

			try
			{
				DataPackageView dataPackageView = Clipboard.GetContent();
				if (dataPackageView.Contains(StandardDataFormats.Text))
				{
					string text = await dataPackageView.GetTextAsync();
					if (ChattyHelper.TryGetThreadIdFromUrl(text, out var threadId))
					{
						if (threadId != Settings.LastClipboardPostId)
						{
							await AppGlobal.DebugLog.AddMessage($"Parsed threadId {threadId} from clipboard.").ConfigureAwait(true);
							Settings.LastClipboardPostId = threadId;
							LinkPopup.IsOpen = true;
							_popupTimer.Stop();
							_linkPopupExpireTime = DateTime.Now.AddMilliseconds(LINK_POPUP_TIMEOUT);
							_popupTimer.Interval = TimeSpan.FromMilliseconds(30);
							LinkPopupTimer.Value = 100;
							_popupTimer.Tick += (_, __) =>
							{
								var remaining = _linkPopupExpireTime.Subtract(DateTime.Now).TotalMilliseconds;
								if (remaining <= 0)
								{
									LinkPopup.IsOpen = false;
									_popupTimer.Stop();
								}
								else
								{
									LinkPopupTimer.Value = Math.Max(((double)remaining / LINK_POPUP_TIMEOUT) * 100, 0);
								}
							};
							_popupTimer.Start();
						}
					}
				}
			}
			catch
			{
				// ignored
			} //Had an exception where data in clipboard was invalid. Ultimately if this doesn't work, who cares.
		}

		public void NavigateToPage(Type page, object arguments, bool forceNav = false)
		{
			if (navigationFrame.CurrentSourcePageType != page || forceNav)
			{
				navigationFrame.Navigate(page, arguments);
			}
		}


		private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName.Equals(nameof(LatestChattySettings.ThemeName), StringComparison.InvariantCulture))
			{
				SetThemeColor();
			}
		}
		private void FrameNavigating(object sender, NavigatingCancelEventArgs e)
		{
			if (_currentlyDisplayedView != null)
			{
				_currentlyDisplayedView.LinkClicked -= Sv_LinkClicked;
				_currentlyDisplayedView.ShellMessage -= Sv_ShellMessage;
				_currentlyDisplayedView = null;
			}
		}

		private async void FrameNavigatedTo(object sender, NavigationEventArgs e)
		{
			var sv = e.Content as ShellView;
			if (sv != null)
			{
				_currentlyDisplayedView = sv;
				sv.LinkClicked += Sv_LinkClicked;
				sv.ShellMessage += Sv_ShellMessage;
				SetCaptionFromFrame(sv);
			}

			NavView.IsBackEnabled = CanGoBack;

			await AppGlobal.DebugLog.AddMessage($"Shell navigated to {e.Content.GetType().Name}").ConfigureAwait(true);

			if (e.Content is Chatty || e.Content is InlineChattyFast)
			{
				SelectFromTag("chatty");
			}
			else if (e.Content is PinnedThreadsView)
			{
				SelectFromTag("pinned");
			}
			else if (e.Content is SearchWebView)
			{
				SelectFromTag("search");
			}
			else if (e.Content is TagsWebView)
			{
				SelectFromTag("tag");
			}
			else if (e.Content is SettingsView)
			{
				NavView.SelectedItem = NavView.SettingsItem;
			}
			else if (e.Content is Messages)
			{
				SelectFromTag("message");
			}
			else if (e.Content is Help)
			{
				SelectFromTag("help");
			}
			else if (e.Content is DeveloperView)
			{
				SelectFromTag("devtools");
			}
			else if (e.Content is ModToolsWebView)
			{
				SelectFromTag("modtools");
			}
		}

		private void SelectFromTag(string tag)
		{
			var menuItems = NavView.MenuItems.Select(x => x as NavigationViewItem).Where(x => x != null);

			foreach (var item in menuItems)
			{
				item.IsSelected = item.Tag.ToString().Equals(tag, StringComparison.InvariantCultureIgnoreCase);
				if (item.IsSelected) NavView.SelectedItem = item;
			}
		}

		private async void Sv_ShellMessage(object sender, ShellMessageEventArgs e)
		{
			await CoreApplication.MainView.CoreWindow.Dispatcher.RunOnUiThreadAndWait(CoreDispatcherPriority.Normal, () =>
			{
				FindName("MessageContainer");
			}).ConfigureAwait(true);
			PopupMessage.ShowMessage(e);
		}

		private void Sv_LinkClicked(object sender, LinkClickedEventArgs e)
		{
			ShowEmbeddedLink(e.Link);
		}

		private void ClickedNav(Microsoft.UI.Xaml.Controls.NavigationView _, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
		{
			if (args.IsSettingsInvoked)
			{
				NavigateToPage(typeof(SettingsView), _container);
				return;
			}
			if (args.InvokedItemContainer?.Tag is null) return;
			NavigateToTag(args.InvokedItemContainer.Tag.ToString());
		}

		private void NavigateToTag(string tag)
		{
			switch (tag.ToLowerInvariant())
			{
				default:
				case "chatty":
					NavigateToPage(Settings.UseMainDetail ? typeof(Chatty) : typeof(InlineChattyFast), _container);
					break;
				case "pinned":
					NavigateToPage(typeof(PinnedThreadsView), _container);
					break;
				case "search":
					NavigateToPage(typeof(SearchWebView), new Tuple<IContainer, Uri>(_container, new Uri("https://shacknews.com/search?q=&type=4")), true);
					break;
				case "mypostssearch":
					NavigateToPage(typeof(SearchWebView), new Tuple<IContainer, Uri>(_container, new Uri($"https://www.shacknews.com/search?chatty=1&type=4&chatty_term=&chatty_user={AuthManager.UserName}&chatty_author=&chatty_filter=all&result_sort=postdate_desc")), true);
					break;
				case "repliestomesearch":
					NavigateToPage(typeof(SearchWebView), new Tuple<IContainer, Uri>(_container, new Uri($"https://www.shacknews.com/search?chatty=1&type=4&chatty_term=&chatty_user=&chatty_author={AuthManager.UserName}&chatty_filter=all&result_sort=postdate_desc")), true);
					break;
				case "vanitysearch":
					NavigateToPage(typeof(SearchWebView), new Tuple<IContainer, Uri>(_container, new Uri($"https://www.shacknews.com/search?chatty=1&type=4&chatty_term={AuthManager.UserName}&chatty_user=&chatty_author=&chatty_filter=all&result_sort=postdate_desc")), true);
					break;
				case "tags":
					NavigateToPage(typeof(TagsWebView), new Tuple<IContainer, Uri>(_container, new Uri("https://www.shacknews.com/tags-user")));
					break;
				case "modtools":
					NavigateToPage(typeof(ModToolsWebView), new Tuple<IContainer, Uri>(_container, new Uri("https://www.shacknews.com/moderators/ban-tool")));
					break;
				case "devtools":
					NavigateToPage(typeof(DeveloperView), _container);
					break;
				case "help":
					NavigateToPage(typeof(Help), new Tuple<IContainer, bool>(_container, false));
					break;
				case "changelog":
					NavigateToPage(typeof(Help), new Tuple<IContainer, bool>(_container, true));
					break;
				case "message":
					NavigateToPage(typeof(Messages), new Tuple<IContainer, string>(_container, null));
					break;
			}
		}

		private void SetCaptionFromFrame(ShellView sv)
		{
			CurrentViewName = sv.ViewTitle;
		}

		private void SetThemeColor()
		{
			var titleBar = ApplicationView.GetForCurrentView().TitleBar;
			titleBar.ButtonBackgroundColor = titleBar.BackgroundColor = titleBar.InactiveBackgroundColor = titleBar.ButtonInactiveBackgroundColor = Settings.Theme.WindowTitleBackgroundColor;
			titleBar.ButtonForegroundColor = titleBar.ForegroundColor = Settings.Theme.WindowTitleForegroundColor;
			titleBar.InactiveForegroundColor = titleBar.ButtonInactiveForegroundColor = Settings.Theme.WindowTitleForegroundColorInactive;
		}

		public bool CanGoBack => navigationFrame.Content != null && navigationFrame.CanGoBack;

		public bool GoBack()
		{
			var f = navigationFrame;
			if (f != null && f.CanGoBack)
			{
				f.GoBack();
				return true;
			}

			return false;
		}

		private async void ShowEmbeddedLink(Uri link)
		{
			link = await LaunchExternalAppOrGetEmbeddedUri(link).ConfigureAwait(true);
			if (link == null) //it was handled, no more to do.
			{
				return;
			}

			if (LaunchShackThreadForUriIfNecessary(link))
			{
				return;
			}

			var embeddedHtml = EmbedHelper.GetEmbedHtml(link);

			if (string.IsNullOrWhiteSpace(embeddedHtml) && !Settings.OpenUnknownLinksInEmbeddedBrowser)
			{
				//Don't want to use the embedded browser, ever.
				await Launcher.LaunchUriAsync(link);
				return;
			}

			FindName("EmbeddedViewer");
			await AppGlobal.DebugLog.AddMessage("ShellEmbeddedBrowserShown").ConfigureAwait(true);
			_embeddedBrowser = new WebView(WebViewExecutionMode.SeparateThread);
			EmbeddedBrowserContainer.Children.Add(_embeddedBrowser);
			EmbeddedViewer.Visibility = Visibility.Visible;
			_embeddedBrowserLink = link;
			_keyBindingWindow = CoreWindow.GetForCurrentThread();
			_keyBindingWindow.KeyDown += Shell_KeyDown;
			_embeddedBrowser.NavigationStarting += EmbeddedBrowser_NavigationStarting;
			_embeddedBrowser.NavigationCompleted += EmbeddedBrowser_NavigationCompleted;
			if (!string.IsNullOrWhiteSpace(embeddedHtml))
			{
				_embeddedBrowser.NavigateToString(embeddedHtml);
			}
			else
			{
				_embeddedBrowser.Navigate(link);
			}
		}

		private void EmbeddedBrowser_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
		{
			BrowserLoadingIndicator.Visibility = Visibility.Visible;
			BrowserLoadingIndicator.IsActive = true;
		}

		private void EmbeddedBrowser_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
		{
			BrowserLoadingIndicator.IsActive = false;
			BrowserLoadingIndicator.Visibility = Visibility.Collapsed;
		}

		private async void Shell_KeyDown(CoreWindow sender, KeyEventArgs args)
		{
			switch (args.VirtualKey)
			{
				case VirtualKey.Escape:
					if (EmbeddedViewer.Visibility == Visibility.Visible)
					{
						await CloseEmbeddedBrowser().ConfigureAwait(false);
					}
					break;
			}
		}

		private async Task<Uri> LaunchExternalAppOrGetEmbeddedUri(Uri link)
		{
			var launchUri = AppLaunchHelper.GetAppLaunchUri(Settings, link);
			if (launchUri.uri != null && !launchUri.openInEmbeddedBrowser)
			{
				await Launcher.LaunchUriAsync(launchUri.uri);
				return null;
			}
			return launchUri.uri;
		}

		private bool LaunchShackThreadForUriIfNecessary(Uri link)
		{
			var postId = AppLaunchHelper.GetShackPostId(link);
			if (postId != null)
			{
				NavigateToPage(typeof(SingleThreadView), new Tuple<IContainer, int, int>(_container, postId.Value, postId.Value));
				return true;
			}
			return false;
		}

		private async void EmbeddedCloseClicked(object sender, RoutedEventArgs e)
		{
			await CloseEmbeddedBrowser().ConfigureAwait(false);
		}

		private async Task CloseEmbeddedBrowser()
		{
			await AppGlobal.DebugLog.AddMessage("ShellEmbeddedBrowserClosed").ConfigureAwait(true);
			_keyBindingWindow.KeyDown -= Shell_KeyDown;
			if (_embeddedBrowser != null)
			{
				_embeddedBrowser.NavigationStarting -= EmbeddedBrowser_NavigationStarting;
				_embeddedBrowser.NavigationCompleted -= EmbeddedBrowser_NavigationCompleted;
				_embeddedBrowser.Stop();
				_embeddedBrowser.NavigateToString("");
			}
			if (_embeddedMediaPlayer != null)
			{
				_embeddedMediaPlayer.Stop();
				_embeddedMediaPlayer.Source = null;
				_embeddedMediaPlayer = null;
			}
			EmbeddedViewer.Visibility = Visibility.Collapsed;
			EmbeddedBrowserContainer.Children.Clear();
			_embeddedBrowser = null;
			_embeddedBrowserLink = null;
		}

		private async void EmbeddedBrowserClicked(object sender, RoutedEventArgs e)
		{
			if (_embeddedBrowserLink != null)
			{
				await AppGlobal.DebugLog.AddMessage("ShellEmbeddedBrowserShowFullBrowser").ConfigureAwait(true);
				await Launcher.LaunchUriAsync(_embeddedBrowserLink);
				await CloseEmbeddedBrowser().ConfigureAwait(true);
			}
		}

		private void CloseClipboardLinkPopupButtonClicked(object sender, RoutedEventArgs e)
		{
			LinkPopup.IsOpen = false;
		}

		private void OpenClipboardLinkTapped(object sender, TappedRoutedEventArgs e)
		{
			if (Settings.LastClipboardPostId != 0)
			{
				NavigateToPage(typeof(SingleThreadView), new Tuple<IContainer, int, int>(_container, (int)Settings.LastClipboardPostId, (int)Settings.LastClipboardPostId));
				LinkPopup.IsOpen = false;
			}
		}

		private void NavView_BackRequested(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewBackRequestedEventArgs args)
		{
			NavigateBack().ConfigureAwait(false);
		}
	}
}
