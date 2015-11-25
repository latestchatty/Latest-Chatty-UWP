﻿using Latest_Chatty_8.Settings;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Latest_Chatty_8.Controls
{
	public sealed partial class RichPostView : UserControl
	{
		private enum RunType
		{
			End,
			None,
			Red,
			Green,
			Blue,
			Yellow,
			Olive,
			Lime,
			Orange,
			Pink,
			Italics,
			Bold,
			Quote,
			Sample,
			Underline,
			Strike,
			Spoiler,
			Code,
			Hyperlink
		}

		private string imageUrlForContextMenu;
		private LatestChattySettings Settings;

		public event EventHandler Resized;

		public RichPostView()
		{
			this.InitializeComponent();
		}

		#region Public Methods
		public void Close()
		{
			this.SizeChanged -= RichPostView_SizeChanged;
		}

		public void LoadPost(string v, LatestChattySettings settings)
		{
			this.Settings = settings;
			this.SizeChanged += RichPostView_SizeChanged;
			this.PopulateBox(v);
		}
		#endregion

		private class TagFind
		{
			public string TagName { get; private set; }
			public RunType Type { get; private set; }

			public TagFind(string tagName, RunType type)
			{
				this.TagName = tagName;
				this.Type = type;
			}
		}

		private TagFind[] FindTags =
		{
			new TagFind("red", RunType.Red),
			new TagFind("green", RunType.Green),
			new TagFind("blue", RunType.Blue),
			new TagFind("yellow", RunType.Yellow),
			new TagFind("olive", RunType.Olive),
			new TagFind("lime", RunType.Lime),
			new TagFind("orange", RunType.Orange),
			new TagFind("pink", RunType.Pink),
			new TagFind("quote", RunType.Quote),
			new TagFind("sample", RunType.Sample),
			new TagFind("strike", RunType.Strike),
			new TagFind("spoiler", RunType.Spoiler),
			new TagFind("code", RunType.Code)
		};

		private void PopulateBox(string body)
		{
			this.postBody.Blocks.Clear();
			var lines = this.ParseLines(body);
			foreach (var line in lines)
			{
				var paragraph = new Windows.UI.Xaml.Documents.Paragraph();
				AddRunsToParagraph(ref paragraph, line);
				this.postBody.Blocks.Add(paragraph);
			}
		}

		private List<string> ParseLines(string body)
		{
			return body.Split(new string[] { "<br />", "<br>" }, StringSplitOptions.None).ToList();
		}

		private void AddRunsToParagraph(ref Paragraph para, string line)
		{
			Run currentRun = null;
			var iCurrentPosition = 0;
			var appliedRunTypes = new Queue<RunType>();

			while (iCurrentPosition < line.Length)
			{
				var result = FindRunTypeAtPosition(line, iCurrentPosition);
				var type = result.Item1;
				var lengthOfTag = result.Item2;
				if (type == RunType.Hyperlink)
				{
					//Handle special.

					//Complete any current run.
					if (!string.IsNullOrEmpty(currentRun?.Text))
					{
						para.Inlines.Add(currentRun);
						currentRun = null;
					}

					//Find the closing tag.
					var closeLocation = line.IndexOf("</a>", iCurrentPosition + lengthOfTag);
					if (closeLocation > -1)
					{
						var link = line.Substring(iCurrentPosition + lengthOfTag, closeLocation - (iCurrentPosition + lengthOfTag));
						var hyperLink = new Hyperlink();
						var run = new Run();
						run.Text = link;
						hyperLink.Inlines.Add(run);
						hyperLink.NavigateUri = new Uri(link);
						para.Inlines.Add(hyperLink);
						iCurrentPosition = closeLocation + 4;
						continue;
					}
				}
				if (type == RunType.None)
				{
					if (currentRun == null)
					{
						currentRun = new Run();
						currentRun.Text = line[iCurrentPosition].ToString();
					}
					else
					{
						currentRun.Text += line[iCurrentPosition].ToString();
					}
				}
				if (type != RunType.End && type != RunType.None)
				{
					appliedRunTypes.Enqueue(type);
					if (currentRun != null && !string.IsNullOrEmpty(currentRun.Text))
					{
						para.Inlines.Add(currentRun);
					}
					currentRun = new Run();
					ApplyTypesToRun(ref currentRun, appliedRunTypes);
				}
				if (type == RunType.End)
				{
					appliedRunTypes.Dequeue();
					if (currentRun != null)
					{
						para.Inlines.Add(currentRun);
						currentRun = null;
					}
				}
				iCurrentPosition += lengthOfTag;
			}
			if (!string.IsNullOrEmpty(currentRun?.Text))
			{
				para.Inlines.Add(currentRun);
			}
		}

		private void ApplyTypesToRun(ref Run run, Queue<RunType> types)
		{
			foreach (var appliedType in types)
			{
				ApplyTypeToRun(ref run, appliedType);
			}
		}

		private void ApplyTypeToRun(ref Run run, RunType type)
		{
			switch (type)
			{
				case RunType.End:
					break;
				case RunType.Red:
					run.Foreground = new SolidColorBrush(Windows.UI.Colors.Red);
					break;
				case RunType.Green:
					run.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 141, 198, 63));
					break;
				case RunType.Blue:
					run.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 68, 174, 223));
					break;
				case RunType.Yellow:
					run.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 222, 0));
					break;
				case RunType.Olive:
					run.Foreground = new SolidColorBrush(Colors.Olive);
					break;
				case RunType.Lime:
					run.Foreground = new SolidColorBrush(Color.FromArgb(255, 192, 255, 192));
					break;
				case RunType.Orange:
					run.Foreground = new SolidColorBrush(Color.FromArgb(255, 247, 148, 28));
					break;
				case RunType.Pink:
					run.Foreground = new SolidColorBrush(Color.FromArgb(255, 244, 154, 193));
					break;
				case RunType.Italics:
					run.FontStyle = Windows.UI.Text.FontStyle.Italic;
					break;
				case RunType.Bold:
					run.FontWeight = Windows.UI.Text.FontWeights.Bold;
					break;
				case RunType.Quote:
					run.FontSize += 2;
					break;
				case RunType.Sample:
					run.FontSize -= 2;
					break;
				case RunType.Underline:
					//TODO: Underline
					break;
				case RunType.Strike:
					//TODO: Strike
					break;
				case RunType.Spoiler:
					//TODO: Spoiler
					break;
				case RunType.Code:
					//TODO: Code
					break;
				default:
					break;
			}
		}

		private Tuple<RunType, int> FindRunTypeAtPosition(string line, int position)
		{
			//Possible tag
			if (line[position] == '<')
			{
				if (position + 1 < line.Length)
				{
					if (line[position + 1] != '/')
					{
						if (line.IndexOf("<i>", position) == position)
						{
							return new Tuple<RunType, int>(RunType.Italics, 3);
						}
						if (line.IndexOf("<b>", position) == position)
						{
							return new Tuple<RunType, int>(RunType.Bold, 3);
						}
						//It's a style tag
						if (line.IndexOf("<span class=\"jt_", position) == position)
						{
							foreach (var tagToFind in FindTags)
							{
								if (line.IndexOf(tagToFind.TagName, position + 16) == position + 16)
								{
									return new Tuple<RunType, int>(tagToFind.Type, line.IndexOf('>', position + 16) + 1 - position);
								}
							}
						}
						if (line.IndexOf("<a target=\"_blank\" rel=\"nofollow\" href=\"", position) == position)
						{
							return new Tuple<RunType, int>(RunType.Hyperlink, line.IndexOf('>', position + 40) + 1 - position);
						}
					}

					if (line.IndexOf("</i>", position) == position || line.IndexOf("</b>", position) == position)
					{
						return new Tuple<RunType, int>(RunType.End, 4);
					}
					if (line.IndexOf("</span>", position) == position)
					{
						return new Tuple<RunType, int>(RunType.End, 7);
					}
				}
			}

			return new Tuple<RunType, int>(RunType.None, 1);
		}


		#region Flyout Events
		async private void OpenImageInBrowserClicked(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(this.imageUrlForContextMenu)) return;
			await Launcher.LaunchUriAsync(new Uri(this.imageUrlForContextMenu));
			(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-OpenImageInBrowserClicked");
		}

		private void CopyImageLinkClicked(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(this.imageUrlForContextMenu)) return;
			var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
			dataPackage.SetText(this.imageUrlForContextMenu);
			Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
			(new Microsoft.ApplicationInsights.TelemetryClient()).TrackEvent("Chatty-CopyImageLinkClicked");
		}
		#endregion

		#region Private Helpers
		async private Task ResizeWebView()
		{
			//await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
			//{
			//	try
			//	{
			//		await this.bodyWebView.InvokeScriptAsync("eval", new string[] { $"SetViewSize({this.container.ActualWidth});" });
			//		var result = await this.bodyWebView.InvokeScriptAsync("eval", new string[] { "GetViewSize();" });
			//		int viewHeight;
			//		if (int.TryParse(result, out viewHeight))
			//		{
			//			System.Diagnostics.Debug.WriteLine("WebView Height is {0}", viewHeight);
			//			this.bodyWebView.MinHeight = this.bodyWebView.Height = viewHeight;
			//			this.bodyWebView.UpdateLayout();
			//			if (this.Resized != null)
			//			{
			//				this.Resized(this, EventArgs.Empty);
			//			}
			//		}
			//	}
			//	catch (Exception ex)
			//	{
			//		//If we happen to call Close while this thread is waiting for the UI to become available, the InvokeScript calls are going to fail.
			//		//This could be safeguarded with locks, but I think the easier way to handle it is to just swallow the exception that gets thrown.
			//		//Rather than block up the UI thread with all that synchronizing.
			//		//This is bad, mmkay?  Mmmkay.
			//		System.Diagnostics.Debug.WriteLine("Exception occurred while resizing {0}", ex);
			//	}
			//});
		}

		async private void RichPostView_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			await this.ResizeWebView();
		}
		#endregion
	}
}