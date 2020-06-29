﻿using Autofac;
using Latest_Chatty_8.Common;
using Latest_Chatty_8.Managers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Latest_Chatty_8.Views
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class DeveloperView
	{
		private IgnoreManager _ignoreManager;
		private IContainer _container;
		public override string ViewTitle => "Developer Stuff - Be careful!";

		public override event EventHandler<LinkClickedEventArgs> LinkClicked = delegate { };

		public override event EventHandler<ShellMessageEventArgs> ShellMessage;


		private ReadOnlyObservableCollection<string> DebugLog { get => Global.DebugLog.Messages; }

		public DeveloperView()
		{
			InitializeComponent();
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			_container = e.Parameter as IContainer;
			_ignoreManager = _container.Resolve<IgnoreManager>();
			Global.DebugLog.ListVisibleInUI = true;
			base.OnNavigatedTo(e);
		}

		protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
		{
			Global.DebugLog.ListVisibleInUI = false;
			base.OnNavigatingFrom(e);
		}

		private void SendTestToast(object sender, RoutedEventArgs e)
		{
			var threadId = ToastThreadId.Text.Replace("http://www.shacknews.com/chatty?id=", "");
			threadId = threadId.Substring(0, threadId.IndexOf("#", StringComparison.Ordinal) > 0 ? threadId.IndexOf("#", StringComparison.Ordinal) : threadId.Length);
			var toastDoc = new XDocument(
				new XElement("toast", new XAttribute("launch", $"goToPost?postId={threadId}"),
					new XElement("visual",
						new XElement("binding", new XAttribute("template", "ToastText02"),
							new XElement("text", new XAttribute("id", "1"), "Test Toast"),
							new XElement("text", new XAttribute("id", "2"), $"This is a test toast for thread id {threadId}.")
						)
					),
					new XElement("actions",
							new XElement("input", new XAttribute("id", "message"),
								new XAttribute("type", "text"),
								new XAttribute("placeHolderContent", "reply")),
							new XElement("action", new XAttribute("activationType", "background"),
								new XAttribute("content", "reply"),
								new XAttribute("arguments", $"reply={threadId}")/*,
								new XAttribute("imageUri", "Assets/success.png"),
								new XAttribute("hint-inputId", "message")*/)
					)
				)
			);

			var doc = new XmlDocument();
			doc.LoadXml(toastDoc.ToString());
			var toast = new ToastNotification(doc);
			toast.Tag = threadId;
			toast.Group = "ReplyToUser";
			var notifier = ToastNotificationManager.CreateToastNotifier();
			notifier.Show(toast);
		}

		private async void ResetIgnoredUsersClicked(object sender, RoutedEventArgs e)
		{
			await _ignoreManager.RemoveAllUsers();
		}

		private async void ResetIgnoredKeywordsClicked(object sender, RoutedEventArgs e)
		{
			await _ignoreManager.RemoveAllKeywords();
		}

		private void LoadThreadById(object sender, RoutedEventArgs e)
		{
			int threadId;
			if (int.TryParse(ToastThreadId.Text, out threadId))
			{
				Frame.Navigate(typeof(SingleThreadView), new Tuple<IContainer, int, int>(_container, threadId, threadId));
			}
		}

		private void ThrowException(object sender, RoutedEventArgs e)
		{
			throw new Exception("Testing exception.");
		}

		private void FastChattyClicked(object sender, RoutedEventArgs e)
		{
			Frame.Navigate(typeof(InlineChattyFast), _container);
		}

		private void CopyDebugLogClicked(object sender, RoutedEventArgs e)
		{
			var dataPackage = new DataPackage();
			var logItems = Global.DebugLog.Messages.ToArray();
			var builder = new StringBuilder();
			foreach (var item in logItems)
			{
				builder.AppendLine(item);
			}
			dataPackage.SetText(builder.ToString());
			Clipboard.SetContent(dataPackage);
			ShellMessage?.Invoke(this, new ShellMessageEventArgs("Log copied to clipboard."));
		}
	}
}
