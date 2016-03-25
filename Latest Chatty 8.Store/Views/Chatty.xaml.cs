﻿using Autofac;
using Latest_Chatty_8.Common;
using Latest_Chatty_8.DataModel;
using Latest_Chatty_8.Settings;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237
namespace Latest_Chatty_8.Views
{
	/// <summary>
	/// A basic page that provides characteristics common to most applications.
	/// </summary>
	public sealed partial class Chatty : ShellView
	{
		private const double SWIPE_THRESHOLD = 110;
		private bool? swipingLeft;
		private bool disableShortcutKeys = false;

		private CoreWindow keyBindWindow;

		public override string ViewTitle
		{
			get
			{
				return "Chatty";
			}
		}

		public override event EventHandler<LinkClickedEventArgs> LinkClicked;

		public override event EventHandler<ShellMessageEventArgs> ShellMessage;

		private IContainer container;

		private LatestChattySettings npcSettings = null;
		private LatestChattySettings Settings
		{
			get { return this.npcSettings; }
			set { this.SetProperty(ref this.npcSettings, value); }
		}

		private INotificationManager notificationManager;

		private CommentThread npcSelectedThread = null;
		public CommentThread SelectedThread
		{
			get { return this.npcSelectedThread; }
			set
			{
				if (this.SetProperty(ref this.npcSelectedThread, value))
				{
					//var t = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
					//{
					//	if (value?.Comments?.Count() > 0) this.commentList.SelectedIndex = 0;
					//});
				}
			}
		}

		private bool npcShowSearch = false;
		private bool ShowSearch
		{
			get { return this.npcShowSearch; }
			set { this.SetProperty(ref this.npcShowSearch, value); }
		}

		private bool npcFilterApplied = false;
		private bool FilterApplied
		{
			get { return this.npcFilterApplied; }
			set
			{
				if (value)
				{
					this.filterButton.Foreground = (SolidColorBrush)App.Current.Resources["ThemeHighlight"];
				}
				else
				{
					this.filterButton.Foreground = this.newRootPostButton.Foreground;
				}
				this.SetProperty(ref this.npcFilterApplied, value);
			}
		}

		public Chatty()
		{
			this.InitializeComponent();
		}

		private ChattyManager chattyManager;
		public ChattyManager ChattyManager
		{
			get { return this.chattyManager; }
			set { this.SetProperty(ref this.chattyManager, value); }
		}
		private ThreadMarkManager markManager;
		private AuthenticationManager authManager;

		#region Thread View

		#endregion

		async private void ChattyListSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.RemovedItems.Count > 0)
			{
				var ct = e.RemovedItems[0] as CommentThread;
				await this.chattyManager.MarkCommentThreadRead(ct);
			}

			if (e.AddedItems.Count == 1)
			{
				var ct = e.AddedItems[0] as CommentThread;
				if (this.visualState.CurrentState == VisualStatePhone)
				{
					this.singleThreadControl.DataContext = null;
					await this.singleThreadControl.Close();
					this.Frame.Navigate(typeof(SingleThreadView), new Tuple<IContainer, CommentThread>(this.container, ct));
				}
				else
				{
					this.singleThreadControl.Initialize(this.container);
					this.singleThreadControl.DataContext = ct;
				}
				this.threadList.ScrollIntoView(ct);
			}
		}

		#region Events

		async private void MarkAllRead(object sender, RoutedEventArgs e)
		{
			await this.chattyManager.MarkAllVisibleCommentsRead();
			(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-MarkReadClicked");
		}

		async private void PinClicked(object sender, RoutedEventArgs e)
		{
			var flyout = sender as MenuFlyoutItem;
			if (flyout == null) return;
			var commentThread = flyout.DataContext as CommentThread;
			if (commentThread == null) return;
			if (this.markManager.GetMarkType(commentThread.Id) == MarkType.Pinned)
			{
				(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-PinClicked");
				await this.markManager.MarkThread(commentThread.Id, MarkType.Unmarked);
			}
			else
			{
				(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-UnpinClicked");
				await this.markManager.MarkThread(commentThread.Id, MarkType.Pinned);
			}
		}

		async private void CollapseClicked(object sender, RoutedEventArgs e)
		{
			var flyout = sender as MenuFlyoutItem;
			if (flyout == null) return;
			var commentThread = flyout.DataContext as CommentThread;
			if (commentThread == null) return;
			if (this.markManager.GetMarkType(commentThread.Id) == MarkType.Collapsed)
			{
				(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-CollapseClicked");
				await this.markManager.MarkThread(commentThread.Id, MarkType.Unmarked);
			}
			else
			{
				(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-UncollapseClicked");
				await this.markManager.MarkThread(commentThread.Id, MarkType.Collapsed);
			}
		}

		async private void MarkThreadReadClicked(object sender, RoutedEventArgs e)
		{
			var flyout = sender as MenuFlyoutItem;
			if (flyout == null) return;
			var commentThread = flyout.DataContext as CommentThread;
			if (commentThread == null) return;
			await this.ChattyManager.MarkCommentThreadRead(commentThread);
		}

		async private void ChattyManager_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName.Equals(nameof(ChattyManager.IsFullUpdateHappening)))
			{
				if (this.ChattyManager.IsFullUpdateHappening)
				{
					this.singleThreadControl.DataContext = null;
					await this.singleThreadControl.Close();
				}
			}
		}
		#endregion


		async private void ReSortClicked(object sender, RoutedEventArgs e)
		{
			(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-ResortClicked");
			await ReSortChatty();
		}

		private async Task ReSortChatty()
		{
			this.SelectedThread = null;
			this.singleThreadControl.DataContext = null;

			if (this.Settings.MarkReadOnSort)
			{
				await this.chattyManager.MarkAllVisibleCommentsRead();
			}
			await this.chattyManager.CleanupChattyList();
			if (this.threadList.Items.Count > 0)
			{
				this.threadList.ScrollIntoView(this.threadList.Items[0]);
			}
			await this.notificationManager.Resume();
		}

		async private void SearchTextChanged(object sender, TextChangedEventArgs e)
		{
			var searchTextBox = sender as TextBox;
			await this.ChattyManager.SearchChatty(searchTextBox.Text);
		}

		private void SearchKeyUp(object sender, KeyRoutedEventArgs e)
		{
			if (e.Key == VirtualKey.Escape)
			{
				foreach (var item in this.filterCombo.Items)
				{
					var i = item as ComboBoxItem;
					if (i.Tag != null && i.Tag.ToString().Equals("all", StringComparison.OrdinalIgnoreCase))
					{
						this.filterCombo.SelectedItem = i;
						break;
					}
				}
			}
		}

		private void NewRootPostButtonClicked(object sender, RoutedEventArgs e)
		{
			if (this.newRootPostButton.IsChecked.HasValue && this.newRootPostButton.IsChecked.Value)
			{
				this.ShowNewRootPost();
			}
			else
			{
				this.CloseNewRootPost();
			}
		}

		private void ThreadListRightHeld(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
		{
			Windows.UI.Xaml.Controls.Primitives.FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
		}
		private void ThreadListRightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
		{
			Windows.UI.Xaml.Controls.Primitives.FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
		}

		async private void FilterChanged(object sender, SelectionChangedEventArgs e)
		{
			if (this.ChattyManager == null) return;
			if (e.AddedItems.Count != 1) return;
			var item = e.AddedItems[0] as ComboBoxItem;
			if (item == null) return;
			ChattyFilterType filter;
			var tagName = item.Tag.ToString();
			(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-Filter-" + tagName);
			switch (tagName)
			{
				case "news":
					filter = ChattyFilterType.News;
					break;
				case "new":
					filter = ChattyFilterType.New;
					break;
				case "has replies":
					filter = ChattyFilterType.HasReplies;
					break;
				case "participated":
					filter = ChattyFilterType.Participated;
					break;
				case "search":
					this.ShowSearch = true;
					this.FilterApplied = true;
					await this.ChattyManager.SearchChatty(this.searchTextBox.Text);
					this.searchTextBox.Focus(FocusState.Programmatic);
					return;
				case "collapsed":
					filter = ChattyFilterType.Collapsed;
					break;
				case "pinned":
					filter = ChattyFilterType.Pinned;
					break;
				default:
					filter = ChattyFilterType.All;
					break;
			}
			this.ShowSearch = false;
			this.FilterApplied = filter != ChattyFilterType.All;
			await this.ChattyManager.FilterChatty(filter);
		}

		async private void SortChanged(object sender, SelectionChangedEventArgs e)
		{
			if (this.ChattyManager == null) return;
			if (e.AddedItems.Count != 1) return;
			var item = e.AddedItems[0] as ComboBoxItem;
			if (item == null) return;
			ChattySortType sort;
			var tagName = item.Tag.ToString();
			(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-Sort-" + tagName);
			switch (tagName)
			{
				case "inf":
					sort = ChattySortType.Inf;
					break;
				case "lol":
					sort = ChattySortType.Lol;
					break;
				case "mostreplies":
					sort = ChattySortType.ReplyCount;
					break;
				case "hasnewreplies":
					sort = ChattySortType.HasNewReplies;
					break;
				case "participated":
					sort = ChattySortType.Participated;
					break;
				default:
					sort = ChattySortType.Default;
					break;
			}
			await this.ChattyManager.SortChatty(sort);
		}

		#region Load and Save State
		protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);
			this.container = e.Parameter as Autofac.IContainer;
			this.authManager = container.Resolve<AuthenticationManager>();
			this.ChattyManager = container.Resolve<ChattyManager>();
			this.markManager = container.Resolve<ThreadMarkManager>();
			this.Settings = container.Resolve<LatestChattySettings>();
			this.notificationManager = container.Resolve<INotificationManager>();
			this.keyBindWindow = CoreWindow.GetForCurrentThread();
			this.keyBindWindow.KeyDown += Chatty_KeyDown;
			this.keyBindWindow.KeyUp += Chatty_KeyUp;
			this.ChattyManager.PropertyChanged += ChattyManager_PropertyChanged;
			if (this.Settings.DisableSplitView)
			{
				VisualStateManager.GoToState(this, "VisualStatePhone", false);
			}
			this.visualState.CurrentStateChanging += VisualState_CurrentStateChanging;
			if (this.visualState.CurrentState == VisualStatePhone)
			{
				this.threadList.SelectedIndex = -1;
			}
		}

		private void VisualState_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
		{
			if (this.Settings.DisableSplitView)
			{
				VisualStateManager.GoToState(e.Control, "VisualStatePhone", false);
			}
		}

		protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
		{
			base.OnNavigatingFrom(e);
			this.ChattyManager.PropertyChanged -= ChattyManager_PropertyChanged;
			this.visualState.CurrentStateChanging -= VisualState_CurrentStateChanging;
			if (this.keyBindWindow != null)
			{
				this.keyBindWindow.KeyDown -= Chatty_KeyDown;
				this.keyBindWindow.KeyUp -= Chatty_KeyUp;
			}
		}

		private bool ctrlDown = false;

		async private void Chatty_KeyDown(CoreWindow sender, KeyEventArgs args)
		{
			try
			{
				if (this.disableShortcutKeys || !this.singleThreadControl.ShortcutKeysEnabled)
				{
					System.Diagnostics.Debug.WriteLine($"{this.GetType().Name} - Suppressed KeyDown event.");
					return;
				}

				switch (args.VirtualKey)
				{
					case VirtualKey.Control:
						ctrlDown = true;
						break;
					case VirtualKey.F5:
						(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-F5Pressed");
						await ReSortChatty();
						break;
					case VirtualKey.J:
						if (this.visualState.CurrentState != VisualStatePhone && !ctrlDown)
						{
							(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-JPressed");
							this.threadList.SelectedIndex = Math.Max(this.threadList.SelectedIndex - 1, 0);
						}
						break;
					case VirtualKey.K:
						if (this.visualState.CurrentState != VisualStatePhone && !ctrlDown)
						{
							(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-KPressed");
							this.threadList.SelectedIndex = Math.Min(this.threadList.SelectedIndex + 1, this.threadList.Items.Count - 1);
						}
						break;
					case VirtualKey.P:
						if (this.visualState.CurrentState != VisualStatePhone && !ctrlDown)
						{
							(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-PPressed");
							if (this.SelectedThread != null)
							{
								await this.markManager.MarkThread(this.SelectedThread.Id, this.markManager.GetMarkType(this.SelectedThread.Id) != MarkType.Pinned ? MarkType.Pinned : MarkType.Unmarked);
							}
						}
						break;
					case VirtualKey.C:
						if (this.visualState.CurrentState != VisualStatePhone && !ctrlDown)
						{
							(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-CPressed");
							if (this.SelectedThread != null)
							{
								await this.markManager.MarkThread(this.SelectedThread.Id, this.markManager.GetMarkType(this.SelectedThread.Id) != MarkType.Collapsed ? MarkType.Collapsed : MarkType.Unmarked);
							}
						}
						break;
					default:
						break;
				}
				System.Diagnostics.Debug.WriteLine($"{this.GetType().Name} - KeyDown event for {args.VirtualKey}");
			}
			catch (Exception e)
			{
				(new Microsoft.ApplicationInsights.TelemetryClient()).TrackException(e, new Dictionary<string, string> { { "keyCode", args.VirtualKey.ToString() } });
			}
		}

		private void Chatty_KeyUp(CoreWindow sender, KeyEventArgs args)
		{
			try
			{
				if (this.disableShortcutKeys || !this.singleThreadControl.ShortcutKeysEnabled)
				{
					System.Diagnostics.Debug.WriteLine($"{this.GetType().Name} - Suppressed KeyUp event.");
					return;
				}

				switch (args.VirtualKey)
				{
					case VirtualKey.Control:
						ctrlDown = false;
						break;
					case VirtualKey.N:
						if (ctrlDown)
						{
							(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-CtrlNPressed");
							this.ShowNewRootPost();
						}
						break;
					default:
						switch ((int)args.VirtualKey)
						{
							case 191:
								(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-SlashPressed");

								if (this.ShowSearch)
								{
									this.searchTextBox.Focus(FocusState.Programmatic);
								}
								else
								{
									foreach (var item in this.filterCombo.Items)
									{
										var i = item as ComboBoxItem;
										if (i.Tag != null && i.Tag.ToString().Equals("search", StringComparison.OrdinalIgnoreCase))
										{
											this.filterCombo.SelectedItem = i;
											break;
										}
									}
								}
								break;
						}
						break;
				}
				System.Diagnostics.Debug.WriteLine($"{this.GetType().Name} - KeyUp event for {args.VirtualKey}");
			}
			catch (Exception e)
			{
				(new Microsoft.ApplicationInsights.TelemetryClient()).TrackException(e, new Dictionary<string, string> { { "keyCode", args.VirtualKey.ToString() } });
			}
		}

		#endregion

		private void ShowNewRootPost()
		{
			this.DisableShortcutKeys();
			this.newRootPostButton.IsChecked = true;
			this.newRootPostControl.SetAuthenticationManager(this.authManager);
			this.newRootPostControl.SetFocus();
			this.newRootPostControl.Closed += NewRootPostControl_Closed;
			this.newRootPostControl.ShellMessage += NewRootPostControl_ShellMessage;
		}

		private void CloseNewRootPost()
		{
			this.newRootPostButton.IsChecked = false;
			this.newRootPostControl.Closed -= NewRootPostControl_Closed;
			this.newRootPostControl.ShellMessage -= NewRootPostControl_ShellMessage;
			this.EnableShortcutKeys();
		}

		private void NewRootPostControl_ShellMessage(object sender, ShellMessageEventArgs e)
		{
			if (this.ShellMessage != null)
			{
				this.ShellMessage(sender, e);
			}
		}

		private void NewRootPostControl_Closed(object sender, EventArgs e)
		{
			this.CloseNewRootPost();
		}

		private void DisableShortcutKeys()
		{
			this.disableShortcutKeys = true;
			this.singleThreadControl.ShortcutKeysEnabled = false;
		}

		private void EnableShortcutKeys()
		{
			this.disableShortcutKeys = false;
			this.singleThreadControl.ShortcutKeysEnabled = true;
		}

		private void SearchTextBoxLostFocus(object sender, RoutedEventArgs e)
		{
			this.EnableShortcutKeys();
		}

		private void SearchTextBoxGotFocus(object sender, RoutedEventArgs e)
		{
			this.DisableShortcutKeys();
		}

		#region Swipe Gestures
		private void ChattyListManipulationStarted(object sender, Windows.UI.Xaml.Input.ManipulationStartedRoutedEventArgs e)
		{
			var grid = sender as Grid;
			if (grid == null) return;

			var container = grid.FindFirstControlNamed<Grid>("previewContainer");
			if (container == null) return;

			var swipeContainer = grid.FindName("swipeContainer") as Grid;
			swipeContainer.Visibility = Visibility.Visible;

			container.Background = (Brush)this.Resources["ApplicationPageBackgroundThemeBrush"];
			this.swipingLeft = null;
		}

		async private void ChattyListManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
		{
			var grid = sender as Grid;
			if (grid == null) return;

			var container = grid.FindFirstControlNamed<Grid>("previewContainer");
			if (container == null) return;

			var swipeContainer = grid.FindName("swipeContainer") as Grid;
			swipeContainer.Visibility = Visibility.Collapsed;

			var ct = container.DataContext as CommentThread;
			if (ct == null) return;
			var currentMark = this.markManager.GetMarkType(ct.Id);
			e.Handled = false;
			System.Diagnostics.Debug.WriteLine("Completed manipulation {0},{1}", e.Cumulative.Translation.X, e.Cumulative.Translation.Y);

			var completedSwipe = Math.Abs(e.Cumulative.Translation.X) > SWIPE_THRESHOLD;
			var operation = e.Cumulative.Translation.X > 0 ? this.Settings.ChattyRightSwipeAction : this.Settings.ChattyLeftSwipeAction;

			if (completedSwipe)
			{
				switch (operation.Type)
				{
					case ChattySwipeOperationType.Collapse:

						if (currentMark != MarkType.Collapsed)
						{
							await this.markManager.MarkThread(ct.Id, MarkType.Collapsed);
						}
						else if (currentMark == MarkType.Collapsed)
						{
							await this.markManager.MarkThread(ct.Id, MarkType.Unmarked);
						}
						e.Handled = true;
						break;
					case ChattySwipeOperationType.Pin:
						if (currentMark != MarkType.Pinned)
						{
							await this.markManager.MarkThread(ct.Id, MarkType.Pinned);
						}
						else if (currentMark == MarkType.Pinned)
						{
							await this.markManager.MarkThread(ct.Id, MarkType.Unmarked);
						}
						e.Handled = true;
						break;
					case ChattySwipeOperationType.MarkRead:
						await this.ChattyManager.MarkCommentThreadRead(ct);
						e.Handled = true;
						break;
				}
			}

			var transform = container.Transform3D as Windows.UI.Xaml.Media.Media3D.CompositeTransform3D;
			transform.TranslateX = 0;
			container.Background = new SolidColorBrush(Windows.UI.Colors.Transparent);
			this.swipingLeft = null;
		}

		private void ChattyListManipulationDelta(object sender, Windows.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs e)
		{
			var grid = sender as Grid;
			if (grid == null) return;

			var container = grid.FindFirstControlNamed<Grid>("previewContainer");
			if (container == null) return;

			var swipeContainer = grid.FindFirstControlNamed<StackPanel>("swipeTextContainer");
			if (swipeContainer == null) return;

			var swipeIconTransform = swipeContainer.Transform3D as Windows.UI.Xaml.Media.Media3D.CompositeTransform3D;

			var transform = container.Transform3D as Windows.UI.Xaml.Media.Media3D.CompositeTransform3D;
			var cumulativeX = e.Cumulative.Translation.X;
			var showRight = (cumulativeX < 0);

			if (!this.swipingLeft.HasValue || this.swipingLeft != showRight)
			{
				var commentThread = grid.DataContext as CommentThread;
				if (commentThread == null) return;

				var swipeIcon = grid.FindFirstControlNamed<TextBlock>("swipeIcon");
				if (swipeIcon == null) return;
				var swipeText = grid.FindFirstControlNamed<TextBlock>("swipeText");
				if (swipeText == null) return;

				var op = showRight ? this.Settings.ChattyLeftSwipeAction : this.Settings.ChattyRightSwipeAction;

				swipeIcon.Text = op.Icon;
				swipeText.Text = op.DisplayName;
				swipeContainer.FlowDirection = showRight ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
				this.swipingLeft = showRight;
			}

			transform.TranslateX = cumulativeX;
			if (Math.Abs(cumulativeX) < SWIPE_THRESHOLD)
			{
				swipeIconTransform.TranslateX = showRight ? -(cumulativeX * .3) : cumulativeX * .3;
			}
			else
			{
				swipeIconTransform.TranslateX = 15;
			}
		}

		#endregion

		private void GoToChattyTopClicked(object sender, RoutedEventArgs e)
		{
			if (this.threadList.Items.Count > 0)
			{
				this.threadList.ScrollIntoView(this.threadList.Items[0]);
			}
		}

		private void InlineControlLinkClicked(object sender, LinkClickedEventArgs e)
		{
			if (this.LinkClicked != null)
			{
				this.LinkClicked(sender, e);
			}
		}

		private void InlineControlShellMessage(object sender, ShellMessageEventArgs e)
		{
			if (this.ShellMessage != null)
			{
				this.ShellMessage(sender, e);
			}
		}
	}
}
