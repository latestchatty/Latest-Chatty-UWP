﻿using Autofac;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Werd.Common;
using Werd.DataModel;
using Werd.Managers;
using Werd.Settings;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Werd.Views
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class Messages
	{
		public override string ViewTitle => "Messages";

		public override event EventHandler<LinkClickedEventArgs> LinkClicked = delegate { }; //Unused
		public override event EventHandler<ShellMessageEventArgs> ShellMessage = delegate { }; //Unused
		private CoreWindow _keyBindWindow;

		private int _currentPage = 1;

		private List<Message> npcMessages;
		public List<Message> DisplayMessages
		{
			get => npcMessages;
			set => SetProperty(ref npcMessages, value);
		}

		private bool npcCanGoBack;
		public bool CanGoBack
		{
			get => npcCanGoBack;
			set => SetProperty(ref npcCanGoBack, value);
		}

		private bool npcCanGoForward;
		public bool CanGoForward
		{
			get => npcCanGoForward;
			set => SetProperty(ref npcCanGoForward, value);
		}

		private bool npcLoadingMessages;
		public bool LoadingMessages
		{
			get => npcLoadingMessages;
			set => SetProperty(ref npcLoadingMessages, value);
		}

		private bool npcCanSendNewMessage;
		public bool CanSendNewMessage
		{
			get => npcCanSendNewMessage;
			set => SetProperty(ref npcCanSendNewMessage, value);
		}

		private MessageManager _messageManager;

		public Messages()
		{
			InitializeComponent();
		}

		protected async override void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);
			var p = e.Parameter as Tuple<IContainer, string>;
			var container = p?.Item1;
			_messageManager = container.Resolve<MessageManager>();
			container.Resolve<LatestChattySettings>();
			_keyBindWindow = CoreWindow.GetForCurrentThread();
			_keyBindWindow.KeyDown += ShortcutKeyDown;
			_keyBindWindow.KeyUp += ShortcutKeyUp;
			if (!string.IsNullOrWhiteSpace(p?.Item2))
			{
				NewMessageButton.IsChecked = true;
				ToTextBox.Text = p.Item2;
			}
			await LoadThreads();

		}

		private bool _ctrlDown;
		private bool _disableShortcutKeys;
		private async void ShortcutKeyDown(CoreWindow sender, KeyEventArgs args)
		{
			if (_disableShortcutKeys)
			{
				return;
			}
			switch (args.VirtualKey)
			{
				case VirtualKey.Control:
					_ctrlDown = true;
					break;
				case VirtualKey.F5:
					await LoadThreads();
					break;
				case VirtualKey.J:
					_currentPage--;
					await LoadThreads();
					break;
				case VirtualKey.K:
					_currentPage++;
					await LoadThreads();
					break;
				case VirtualKey.A:
					MessagesList.SelectedIndex = Math.Max(MessagesList.SelectedIndex - 1, 0);
					break;
				case VirtualKey.Z:
					if (MessagesList.Items != null)
					{
						MessagesList.SelectedIndex =
							Math.Min(MessagesList.SelectedIndex + 1, MessagesList.Items.Count - 1);
					}
					else
					{
						MessagesList.SelectedIndex = 0;
					}

					break;
				case VirtualKey.D:
					var msg = MessagesList.SelectedItem as Message;
					if (msg == null) return;
					await AppGlobal.DebugLog.AddMessage("Message-DPressed");
					await DeleteMessage(msg);
					break;
			}
		}

		private void ShortcutKeyUp(CoreWindow sender, KeyEventArgs args)
		{
			if (_disableShortcutKeys)
			{
				return;
			}
			switch (args.VirtualKey)
			{
				case VirtualKey.Control:
					_ctrlDown = false;
					break;
				case VirtualKey.N:
					if (_ctrlDown)
					{
						NewMessageButton.IsChecked = true;
					}
					break;
				case VirtualKey.R:
					ShowReply.IsChecked = true;
					break;
			}
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			_keyBindWindow.KeyDown -= ShortcutKeyDown;
			_keyBindWindow.KeyUp -= ShortcutKeyUp;
		}

		private async void PreviousPageClicked(object sender, RoutedEventArgs e)
		{
			_currentPage--;
			await LoadThreads();
		}

		private async void NextPageClicked(object sender, RoutedEventArgs e)
		{
			_currentPage++;
			await LoadThreads();
		}

		private async void RefreshClicked(object sender, RoutedEventArgs e)
		{
			await LoadThreads();
		}

		private async void MessagesPullRefresh(RefreshContainer sender, RefreshRequestedEventArgs args)
		{
			using (var _ = args.GetDeferral())
			{
				await LoadThreads();
			}
		}

		private async void DeleteMessageClicked(object sender, RoutedEventArgs e)
		{
			var msg = MessagesList.SelectedItem as Message;
			if (msg == null) return;
			await DeleteMessage(msg);
		}

		private async void SubmitPostButtonClicked(object sender, RoutedEventArgs e)
		{
			var msg = MessagesList.SelectedItem as Message;
			if (msg == null) return;
			var btn = (Button)sender;
			try
			{
				btn.IsEnabled = false;
				//If we're replying to a sent message, we want to send to the person we sent it to, not to ourselves.
				var viewingSentMessage = MailboxCombo.SelectedItem != null && ((ComboBoxItem)MailboxCombo.SelectedItem).Tag.ToString().Equals("sent", StringComparison.OrdinalIgnoreCase);
				var success = await _messageManager.SendMessage(viewingSentMessage ? msg.To : msg.From, string.Format("Re: {0}", msg.Subject), ReplyTextBox.Text);
				if (success)
				{
					ShowReply.IsChecked = false;
					_disableShortcutKeys = false;
					Focus(FocusState.Programmatic);
					if (viewingSentMessage)
					{
						await LoadThreads();
					}
				}
				else
				{
					var dlg = new MessageDialog("Failed to send message.");
					await dlg.ShowAsync();
				}
			}
			finally
			{
				btn.IsEnabled = true;
			}
		}

		private void ShowReplyChecked(object sender, RoutedEventArgs e)
		{
			var msg = MessagesList.SelectedItem as Message;
			if (msg == null) return;
			_disableShortcutKeys = true;
			ReplyTextBox.Text = string.Format("{2}{2}On {0} {1} wrote: {2} {3}", msg.Date, msg.From, Environment.NewLine, msg.Body);
			ReplyTextBox.Focus(FocusState.Programmatic);

		}

		private void ShowReplyUnchecked(object sender, RoutedEventArgs e)
		{
			_disableShortcutKeys = false;
		}

		private async void SendNewMessageClicked(object sender, RoutedEventArgs e)
		{
			var btn = (Button)sender;
			try
			{
				btn.IsEnabled = false;
				var success = await _messageManager.SendMessage(ToTextBox.Text, SubjectTextBox.Text, NewMessageTextBox.Text);
				if (success)
				{
					NewMessageButton.IsChecked = false;
					_disableShortcutKeys = false;
					Focus(FocusState.Programmatic);
					if (MailboxCombo.SelectedItem != null && ((ComboBoxItem)MailboxCombo.SelectedItem).Tag.ToString().Equals("sent", StringComparison.OrdinalIgnoreCase))
					{
						await LoadThreads();
					}
				}
				else
				{
					var dlg = new MessageDialog("Failed to send message.");
					await dlg.ShowAsync();
				}
			}
			finally
			{
				btn.IsEnabled = true;
			}
		}

		private void ShowNewMessageButtonChecked(object sender, RoutedEventArgs e)
		{
			_disableShortcutKeys = true;
			ToTextBox.Text = string.Empty;
			SubjectTextBox.Text = string.Empty;
			NewMessageTextBox.Text = string.Empty;
			ToTextBox.Focus(FocusState.Programmatic);
		}

		private void ShowNewMessageButtonUnchecked(object sender, RoutedEventArgs e)
		{
			_disableShortcutKeys = false;
		}

		private void NewMessageTextChanged(object sender, TextChangedEventArgs e)
		{
			CanSendNewMessage = !string.IsNullOrWhiteSpace(ToTextBox.Text) &&
				!string.IsNullOrWhiteSpace(SubjectTextBox.Text) &&
				!string.IsNullOrWhiteSpace(NewMessageTextBox.Text);
		}

		private async Task LoadThreads()
		{
			if (_messageManager == null) return;

			LoadingMessages = true;

			CanGoBack = false;
			CanGoForward = false;

			if (_currentPage <= 1) _currentPage = 1;

			var folder = ((ComboBoxItem)MailboxCombo.SelectedItem)?.Tag.ToString();
			var result = await _messageManager.GetMessages(_currentPage, folder);

			DisplayMessages = result.Item1;

			CanGoBack = _currentPage > 1;
			CanGoForward = _currentPage < result.Item2;

			LoadingMessages = false;
		}

		private async void MessageSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ShowReply.IsChecked = false;
			if (e.AddedItems.Count != 1) return;

			var message = e.AddedItems[0] as Message;
			if (message != null)
			{
				//var embedResult = EmbedHelper.RewriteEmbeds(message.Body);
				//this.messageWebView.LoadPost(WebBrowserHelper.GetPostHtml(embedResult.Item1, embedResult.Item2), this.settings);
				MessageWebView.LoadPost(message.Body, false);
				//Mark read.
				await _messageManager.MarkMessageRead(message);
			}
		}

		private async void FolderSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await LoadThreads();
		}

		private async Task DeleteMessage(Message msg)
		{
			DeleteButton.IsEnabled = false;
			try
			{
				if (await _messageManager.DeleteMessage(msg, ((ComboBoxItem)MailboxCombo.SelectedItem)?.Tag.ToString()))
				{
					await LoadThreads();
				}
			}
			finally
			{
				DeleteButton.IsEnabled = true;
			}
		}

		private void DiscardPostButtonClicked(object sender, RoutedEventArgs e)
		{
			ReplyTextBox.Text = string.Empty;
			ShowReply.IsChecked = false;
		}
	}
}
