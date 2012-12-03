﻿using Latest_Chatty_8.Common;
using Latest_Chatty_8.DataModel;
using Latest_Chatty_8.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.ApplicationSettings;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Split App template is documented at http://go.microsoft.com/fwlink/?LinkId=234228

namespace Latest_Chatty_8
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	sealed partial class App : Application
	{
		Popup settingsPopup;
		Rect windowBounds;
		
		/// <summary>
		/// Initializes the singleton Application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			this.InitializeComponent();
			TileUpdateManager.CreateTileUpdaterForApplication().EnableNotificationQueue(true);
			this.Suspending += OnSuspending;
			SuspensionManager.KnownTypes.Add(typeof(NewsStory));
			SuspensionManager.KnownTypes.Add(typeof(List<NewsStory>));
			SuspensionManager.KnownTypes.Add(typeof(Comment));
			SuspensionManager.KnownTypes.Add(typeof(List<Comment>));
			SuspensionManager.KnownTypes.Add(typeof(int));
		}

		protected override void OnActivated(IActivatedEventArgs args)
		{
			base.OnActivated(args);
		}

		/// <summary>
		/// Invoked when the application is launched normally by the end user.  Other entry points
		/// will be used when the application is launched to open a specific file, to display
		/// search results, and so forth.
		/// </summary>
		/// <param name="args">Details about the launch request and process.</param>
		protected override async void OnLaunched(LaunchActivatedEventArgs args)
		{
			Window.Current.SizeChanged += OnWindowSizeChanged;
			OnWindowSizeChanged(null, null);
			LatestChattySettings.Instance.CreateInstance();
			await CoreServices.Instance.Initialize();
			CoreServices.Instance.Resume();
		

			SettingsPane.GetForCurrentView().CommandsRequested += SettingsRequested;

			Frame rootFrame = Window.Current.Content as Frame;

			// Do not repeat app initialization when the Window already has content,
			// just ensure that the window is active

			if (rootFrame == null)
			{
				// Create a Frame to act as the navigation context and navigate to the first page
				rootFrame = new Frame();
				//Associate the frame with a SuspensionManager key                                
				SuspensionManager.RegisterFrame(rootFrame, "AppFrame");

				if (args.PreviousExecutionState == ApplicationExecutionState.Terminated)
				{
					// Restore the saved session state only when appropriate
					try
					{
						await SuspensionManager.RestoreAsync();
					}
					catch (SuspensionManagerException)
					{
						//Something went wrong restoring state.
						//Assume there is no state and continue
					}
				}

				// Place the frame in the current Window
				Window.Current.Content = rootFrame;
			}
			if (rootFrame.Content == null)
			{
				// When the navigation stack isn't restored navigate to the first page,
				// configuring the new page by passing required information as a navigation
				// parameter
				if (!rootFrame.Navigate(typeof(MainPage), "AllGroups"))
				{
					throw new Exception("Failed to create initial page");
				}
			}
			// Ensure the current window is active
			Window.Current.Activate();
		}

		private void SettingsRequested(SettingsPane sender, SettingsPaneCommandsRequestedEventArgs args)
		{
			args.Request.ApplicationCommands.Add(new SettingsCommand("MainSettings", "Settings", (x) =>
			{
				settingsPopup = new Popup();
				settingsPopup.Closed += popup_Closed;
				Window.Current.Activated += OnWindowActivated;
				settingsPopup.IsLightDismissEnabled = true;
				settingsPopup.Width = 346;
				//TODO: Respond to tilting.
				settingsPopup.Height = this.windowBounds.Height;

				settingsPopup.ChildTransitions = new TransitionCollection();
				settingsPopup.ChildTransitions.Add(new PaneThemeTransition()
				{
					Edge = (SettingsPane.Edge == SettingsEdgeLocation.Right) ?
							 EdgeTransitionLocation.Right :
							 EdgeTransitionLocation.Left
				});

				var settingsControl = new Latest_Chatty_8.Settings.MainSettings(LatestChattySettings.Instance);
				settingsControl.Width = settingsPopup.Width;
				settingsControl.Height = windowBounds.Height;
				settingsPopup.SetValue(Canvas.LeftProperty, windowBounds.Width - settingsPopup.Width);
				settingsPopup.SetValue(Canvas.TopProperty, 0);
				settingsPopup.Child = settingsControl;
				settingsPopup.IsOpen = true;
				settingsControl.Initialize();
			}));

			args.Request.ApplicationCommands.Add(new SettingsCommand("PrivacySettings", "Privacy and Sync", (x) =>
			{
				settingsPopup = new Popup();
				settingsPopup.Closed += popup_Closed;
				Window.Current.Activated += OnWindowActivated;
				settingsPopup.IsLightDismissEnabled = true;
				settingsPopup.Width = 346;
				//TODO: Respond to tilting.
				settingsPopup.Height = this.windowBounds.Height;

				settingsPopup.ChildTransitions = new TransitionCollection();
				settingsPopup.ChildTransitions.Add(new PaneThemeTransition()
				{
					Edge = (SettingsPane.Edge == SettingsEdgeLocation.Right) ?
							 EdgeTransitionLocation.Right :
							 EdgeTransitionLocation.Left
				});

				var settingsControl = new Latest_Chatty_8.Settings.PrivacySettings(LatestChattySettings.Instance);
				settingsControl.Width = settingsPopup.Width;
				settingsControl.Height = windowBounds.Height;
				settingsPopup.SetValue(Canvas.LeftProperty, windowBounds.Width - settingsPopup.Width);
				settingsPopup.SetValue(Canvas.TopProperty, 0);
				settingsPopup.Child = settingsControl;
				settingsPopup.IsOpen = true;
				settingsControl.Initialize();
			}));
		}

		void popup_Closed(object sender, object e)
		{
			Window.Current.Activated -= OnWindowActivated;
		}

		void OnWindowActivated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
		{
			if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
			{
				this.settingsPopup.IsOpen = false;
			}
		}

		void OnWindowSizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
		{
			this.windowBounds = Window.Current.Bounds;
		}

		/// <summary>
		/// Invoked when application execution is being suspended.  Application state is saved
		/// without knowing whether the application will be terminated or resumed with the contents
		/// of memory still intact.
		/// </summary>
		/// <param name="sender">The source of the suspend request.</param>
		/// <param name="e">Details about the suspend request.</param>
		private async void OnSuspending(object sender, SuspendingEventArgs e)
		{
			var deferral = e.SuspendingOperation.GetDeferral();
			try
			{
				await SuspensionManager.SaveAsync();
			}
			catch { }
			try
			{
				CoreServices.Instance.Suspend();
			}
			catch (Exception){
				System.Diagnostics.Debug.WriteLine("blah");
			}
			deferral.Complete();
		}

		/// <summary>
		/// Invoked when the application is activated to display search results.
		/// </summary>
		/// <param name="args">Details about the activation request.</param>
		protected async override void OnSearchActivated(Windows.ApplicationModel.Activation.SearchActivatedEventArgs args)
		{
			// TODO: Register the Windows.ApplicationModel.Search.SearchPane.GetForCurrentView().QuerySubmitted
			// event in OnWindowCreated to speed up searches once the application is already running

			// If the Window isn't already using Frame navigation, insert our own Frame
			var previousContent = Window.Current.Content;
			var frame = previousContent as Frame;

			// If the app does not contain a top-level frame, it is possible that this 
			// is the initial launch of the app. Typically this method and OnLaunched 
			// in App.xaml.cs can call a common method.
			if (frame == null)
			{
				// Create a Frame to act as the navigation context and associate it with
				// a SuspensionManager key
				frame = new Frame();
				Latest_Chatty_8.Common.SuspensionManager.RegisterFrame(frame, "AppFrame");

				if (args.PreviousExecutionState == ApplicationExecutionState.Terminated)
				{
					// Restore the saved session state only when appropriate
					try
					{
						await Latest_Chatty_8.Common.SuspensionManager.RestoreAsync();
					}
					catch (Latest_Chatty_8.Common.SuspensionManagerException)
					{
						//Something went wrong restoring state.
						//Assume there is no state and continue
					}
				}
			}

			frame.Navigate(typeof(Search), args.QueryText);
			Window.Current.Content = frame;

			// Ensure the current window is active
			Window.Current.Activate();
		}
	}
}
